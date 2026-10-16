using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ArknightsBaseOptimizer.Optimization.PlanModels;

namespace ArknightsBaseOptimizer.Optimization
{
    /// <summary>
    /// Polishes a plan: first removes the least useful workers until each shift's non-Control-Center workers fit in the dorms,
    /// then tries swapping in unused operators seat by seat, keeping any change that improves the score.
    /// </summary>
    public static class PlanImprover
    {
        private const int BedCount = 20;
        private const int RoomSeats = 3;
        private const int MaximumSwapRounds = 5;

        /// <summary>
        /// Returns an improved copy of the plan for the given goal; the original plan is left untouched.
        /// </summary>
        public static BasePlanResult Improve(BasePlanResult startingPlan, OptimizationGoal goal, double maximumLmd, double maximumExperience, List<RosterOperator> roster, double checkInIntervalHours)
        {
            ShiftPlan shiftA = startingPlan.ShiftA.Plan.Clone();
            ShiftPlan shiftB = startingPlan.ShiftB.Plan.Clone();
            int goldFactoryCount = startingPlan.GoldFactoryCount;

            double ScorePlan() => BaseOptimizer.Score(goal, BaseOptimizer.EvaluatePlan(shiftA, shiftB, goldFactoryCount, checkInIntervalHours), maximumLmd, maximumExperience);

            double currentScore = ScorePlan();
            foreach (ShiftPlan shift in new[] { shiftA, shiftB })
            {
                while (CountBedNeeders(shift) > BedCount)
                {
                    currentScore = RemoveCheapestOperator(shift, ScorePlan);
                }
            }

            Dictionary<ProductionRoomKind, List<RosterOperator>> candidatesByKind = Enum.GetValues<ProductionRoomKind>()
                .ToDictionary(kind => kind, kind => roster.Where(rosterOperator => TeamGenerator.IsCandidate(rosterOperator, kind)).ToList());

            for (int round = 0; round < MaximumSwapRounds; round++)
            {
                foreach (ShiftPlan shift in new[] { shiftA, shiftB })
                {
                    RefreshPartners(shift, roster, shiftA, shiftB);
                }
                currentScore = ScorePlan();

                bool improvedThisRound = false;
                foreach (ShiftPlan shift in new[] { shiftA, shiftB })
                {
                    foreach ((List<RosterOperator> room, ProductionRoomKind kind) in GetProductionRooms(shift))
                    {
                        improvedThisRound |= TryImproveRoom(room, candidatesByKind[kind], shift, shiftA, shiftB, ScorePlan, ref currentScore);
                    }
                }
                if (!improvedThisRound)
                {
                    break;
                }
            }

            PruneDoNothingWorkers(shiftA, shiftB, () => BaseOptimizer.EvaluatePlan(shiftA, shiftB, goldFactoryCount, checkInIntervalHours));

            foreach (ShiftPlan shift in new[] { shiftA, shiftB })
            {
                RefreshPartners(shift, roster, shiftA, shiftB);
            }
            return BaseOptimizer.EvaluatePlan(shiftA, shiftB, goldFactoryCount, checkInIntervalHours);
        }

        /// <summary>
        /// Removes production workers whose contribution is negligible: the plan keeps at least 99.5% of its LMD, EXP and
        /// gold made, measured against the plan before any removals so small losses can't add up. Clears out stragglers like
        /// Texas without Lappland (who only adds the +1% every worker gives) and frees their beds.
        /// </summary>
        private static void PruneDoNothingWorkers(ShiftPlan shiftA, ShiftPlan shiftB, Func<BasePlanResult> evaluatePlan)
        {
            const double allowedLossShare = 0.005;
            BasePlanResult original = evaluatePlan();
            bool removedAny = true;

            while (removedAny)
            {
                removedAny = false;
                foreach (ShiftPlan shift in new[] { shiftA, shiftB })
                {
                    foreach ((List<RosterOperator> room, ProductionRoomKind _) in GetProductionRooms(shift))
                    {
                        for (int seatIndex = room.Count - 1; seatIndex >= 0; seatIndex--)
                        {
                            RosterOperator removedOperator = room[seatIndex];
                            room.RemoveAt(seatIndex);
                            BasePlanResult without = evaluatePlan();

                            bool keepsEnough = KeepsEnough(original.LmdPerCheckIn, without.LmdPerCheckIn, allowedLossShare)
                                && KeepsEnough(original.ExperiencePerCheckIn, without.ExperiencePerCheckIn, allowedLossShare)
                                && KeepsEnough(original.GoldProducedPerCheckIn, without.GoldProducedPerCheckIn, allowedLossShare);

                            if (keepsEnough)
                            {
                                removedAny = true;
                            }
                            else
                            {
                                room.Insert(seatIndex, removedOperator);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// True if the new value is at most the allowed share below the original (a zero original allows no loss).
        /// </summary>
        private static bool KeepsEnough(double originalValue, double newValue, double allowedLossShare)
        {
            return newValue >= originalValue * (1 - allowedLossShare) - 1e-6;
        }

        /// <summary>
        /// Keeps partners in sync with who is working: drops ones nobody needs, then places newly needed ones if they're free and there's a bed.
        /// </summary>
        private static void RefreshPartners(ShiftPlan shift, List<RosterOperator> roster, ShiftPlan shiftA, ShiftPlan shiftB)
        {
            PartnerPlacement.RemoveUnneeded(shift);

            foreach (string requiredName in PartnerPlacement.GetRequiredNames(shift))
            {
                bool alreadyWorking = shift.AllWorkingOperators().Any(rosterOperator => string.Equals(rosterOperator.Name, requiredName, StringComparison.OrdinalIgnoreCase));
                if (alreadyWorking || CountBedNeeders(shift) >= BedCount)
                {
                    continue;
                }

                RosterOperator? partner = roster.FirstOrDefault(rosterOperator => string.Equals(rosterOperator.Name, requiredName, StringComparison.OrdinalIgnoreCase));
                if (partner != null && !IsWorking(partner, shiftA, shiftB))
                {
                    PartnerPlacement.Place(shift, partner);
                }
            }
        }

        /// <summary>
        /// Workers who need a dorm bed: everyone except the Control Center, which stays full and may start tired.
        /// </summary>
        private static int CountBedNeeders(ShiftPlan shift)
        {
            return shift.AllWorkingOperators().Count - shift.ControlCenter.Count;
        }

        /// <summary>
        /// Removes the one non-Control-Center worker whose absence costs the least, and returns the new score.
        /// On ties, Power Plant partners go before production workers.
        /// </summary>
        private static double RemoveCheapestOperator(ShiftPlan shift, Func<double> scorePlan)
        {
            double bestScore = double.NegativeInfinity;
            List<RosterOperator>? bestSeatList = null;
            int bestSeatIndex = -1;

            foreach ((List<RosterOperator> seatList, int seatIndex) in GetRemovalOrder(shift))
            {
                RosterOperator removedOperator = seatList[seatIndex];
                seatList.RemoveAt(seatIndex);
                double scoreWithout = scorePlan();
                seatList.Insert(seatIndex, removedOperator);

                if (scoreWithout > bestScore)
                {
                    bestScore = scoreWithout;
                    bestSeatList = seatList;
                    bestSeatIndex = seatIndex;
                }
            }

            bestSeatList!.RemoveAt(bestSeatIndex);
            return bestScore;
        }

        /// <summary>
        /// Tries every unused candidate in every seat of one room, including one empty seat if a bed is free.
        /// Seat state is re-checked against the room's real size before every attempt. Keeps a swap only if the score goes up.
        /// </summary>
        private static bool TryImproveRoom(List<RosterOperator> room, List<RosterOperator> candidates, ShiftPlan shift, ShiftPlan shiftA, ShiftPlan shiftB, Func<double> scorePlan, ref double currentScore)
        {
            bool improved = false;

            for (int seatIndex = 0; seatIndex < RoomSeats; seatIndex++)
            {
                if (seatIndex > room.Count)
                {
                    break;
                }

                foreach (RosterOperator candidate in candidates)
                {
                    if (IsWorking(candidate, shiftA, shiftB))
                    {
                        continue;
                    }

                    bool fillsEmptySeat = seatIndex >= room.Count;
                    if (fillsEmptySeat && CountBedNeeders(shift) >= BedCount)
                    {
                        break;
                    }

                    RosterOperator? previousOperator = fillsEmptySeat ? null : room[seatIndex];
                    if (fillsEmptySeat)
                    {
                        room.Add(candidate);
                    }
                    else
                    {
                        room[seatIndex] = candidate;
                    }

                    double newScore = scorePlan();
                    if (newScore > currentScore + 1e-9)
                    {
                        currentScore = newScore;
                        improved = true;
                        continue;
                    }

                    if (fillsEmptySeat)
                    {
                        room.RemoveAt(room.Count - 1);
                    }
                    else
                    {
                        room[seatIndex] = previousOperator!;
                    }
                }
            }

            return improved;
        }

        /// <summary>
        /// Every removable seat in the order removal is tried: Power Plants first, then production rooms. The Control Center is never trimmed.
        /// </summary>
        private static List<(List<RosterOperator> SeatList, int SeatIndex)> GetRemovalOrder(ShiftPlan shift)
        {
            List<(List<RosterOperator> SeatList, int SeatIndex)> seats = new();

            for (int seatIndex = 0; seatIndex < shift.PowerPlants.Count; seatIndex++)
            {
                seats.Add((shift.PowerPlants, seatIndex));
            }
            foreach (List<RosterOperator> occupants in shift.SupportRooms.Values)
            {
                for (int seatIndex = 0; seatIndex < occupants.Count; seatIndex++)
                {
                    seats.Add((occupants, seatIndex));
                }
            }
            foreach ((List<RosterOperator> room, ProductionRoomKind _) in GetProductionRooms(shift))
            {
                for (int seatIndex = 0; seatIndex < room.Count; seatIndex++)
                {
                    seats.Add((room, seatIndex));
                }
            }

            return seats;
        }

        /// <summary>
        /// The shift's Trading Posts and Factories, each with the room kind that decides which candidates fit.
        /// </summary>
        private static List<(List<RosterOperator> Room, ProductionRoomKind Kind)> GetProductionRooms(ShiftPlan shift)
        {
            return shift.TradingPosts
                .Select(tradingPost => (tradingPost, ProductionRoomKind.TradingPost))
                .Concat(shift.Factories.Select(factory => (factory.Members, factory.Formula == FactoryFormula.BattleRecord ? ProductionRoomKind.ExperienceFactory : ProductionRoomKind.GoldFactory)))
                .ToList();
        }

        /// <summary>
        /// True if the operator already works in either shift (each operator works one shift and rests the other).
        /// </summary>
        private static bool IsWorking(RosterOperator rosterOperator, ShiftPlan shiftA, ShiftPlan shiftB)
        {
            return shiftA.AllWorkingOperators().Contains(rosterOperator) || shiftB.AllWorkingOperators().Contains(rosterOperator);
        }
    }
}
