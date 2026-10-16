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
    /// Builds full two-shift base plans from ranked teams and picks the best plan for each button.
    /// </summary>
    public static class BaseOptimizer
    {
        private const int TradingPostCount = 2;
        private const int FactoryCount = 4;
        private const int PowerPlantCount = 3;

        /// <summary>
        /// Tries every gold/EXP Factory split and room fill order, polishes the best plan for each goal, and returns all three.
        /// </summary>
        public static OptimizationResults Optimize(List<RosterOperator> roster, double checkInIntervalHours)
        {
            BaseContext rankingContext = new()
            {
                CheckInIntervalHours = checkInIntervalHours,
                OperatorsInBase = roster,
                ControlCenterTradingBonus = 7,
                ControlCenterFactoryBonus = 2
            };

            Dictionary<ProductionRoomKind, List<RankedTeam>> rankedTeams = Enum.GetValues<ProductionRoomKind>()
                .ToDictionary(kind => kind, kind => TeamGenerator.GenerateRankedTeams(roster, kind, rankingContext));

            List<BasePlanResult> plans = new();
            for (int goldFactoryCount = 0; goldFactoryCount <= FactoryCount; goldFactoryCount++)
            {
                foreach (ProductionRoomKind[] fillOrder in GetFillOrders())
                {
                    plans.Add(BuildPlan(roster, rankedTeams, goldFactoryCount, fillOrder, checkInIntervalHours));
                }
            }

            BasePlanResult bestLmd = PlanImprover.Improve(plans.OrderByDescending(plan => Score(OptimizationGoal.Lmd, plan, 1, 1)).First(), OptimizationGoal.Lmd, 1, 1, roster, checkInIntervalHours);
            BasePlanResult bestExperience = PlanImprover.Improve(plans.OrderByDescending(plan => Score(OptimizationGoal.Experience, plan, 1, 1)).First(), OptimizationGoal.Experience, 1, 1, roster, checkInIntervalHours);

            double maximumLmd = Math.Max(1, bestLmd.LmdPerCheckIn);
            double maximumExperience = Math.Max(1, bestExperience.ExperiencePerCheckIn);
            BasePlanResult bestBalanced = PlanImprover.Improve(plans.OrderByDescending(plan => Score(OptimizationGoal.Balanced, plan, maximumLmd, maximumExperience)).First(), OptimizationGoal.Balanced, maximumLmd, maximumExperience, roster, checkInIntervalHours);

            return new OptimizationResults(bestLmd, bestExperience, bestBalanced);
        }

        /// <summary>
        /// One number to rank plans by for a goal. LMD and EXP use the other resource as a tiny tie-breaker;
        /// Balanced ranks by the weaker of the two percentages, then by their total.
        /// </summary>
        public static double Score(OptimizationGoal goal, BasePlanResult plan, double maximumLmd, double maximumExperience)
        {
            double lmdShare = plan.LmdPerCheckIn / maximumLmd;
            double experienceShare = plan.ExperiencePerCheckIn / maximumExperience;
            return goal switch { OptimizationGoal.Lmd => plan.LmdPerCheckIn + plan.ExperiencePerCheckIn * 1e-6, OptimizationGoal.Experience => plan.ExperiencePerCheckIn + plan.LmdPerCheckIn * 1e-6, _ => Math.Min(lmdShare, experienceShare) + 1e-3 * (lmdShare + experienceShare) };
        }

        /// <summary>
        /// Evaluates both shifts and wraps them into one plan result.
        /// </summary>
        public static BasePlanResult EvaluatePlan(ShiftPlan shiftA, ShiftPlan shiftB, int goldFactoryCount, double checkInIntervalHours)
        {
            return new BasePlanResult
            {
                ShiftA = EvaluateShift(shiftA, checkInIntervalHours),
                ShiftB = EvaluateShift(shiftB, checkInIntervalHours),
                GoldFactoryCount = goldFactoryCount
            };
        }

        /// <summary>
        /// Fills both shifts: Control Center buffers, then each room kind in the given order, then partners and morale seats.
        /// </summary>
        private static BasePlanResult BuildPlan(List<RosterOperator> roster, Dictionary<ProductionRoomKind, List<RankedTeam>> rankedTeams, int goldFactoryCount, ProductionRoomKind[] fillOrder, double checkInIntervalHours)
        {
            HashSet<RosterOperator> usedOperators = new();
            ShiftPlan shiftA = new();
            ShiftPlan shiftB = new();
            ControlCenterPlanner.ReserveBuffers(roster, shiftA, shiftB, usedOperators, goldFactoryCount > 0);

            Dictionary<ProductionRoomKind, int> roomsPerShift = new()
            {
                [ProductionRoomKind.TradingPost] = TradingPostCount,
                [ProductionRoomKind.GoldFactory] = goldFactoryCount,
                [ProductionRoomKind.ExperienceFactory] = FactoryCount - goldFactoryCount
            };

            foreach (ProductionRoomKind kind in fillOrder)
            {
                int roomCount = roomsPerShift[kind] * 2;
                int teamCount = kind == ProductionRoomKind.TradingPost && goldFactoryCount == 0 ? 0 : roomCount;
                List<RankedTeam> pickedTeams = PickDisjointTeams(rankedTeams[kind], teamCount, usedOperators);

                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    ShiftPlan targetShift = roomIndex % 2 == 0 ? shiftA : shiftB;
                    List<RosterOperator> members = roomIndex < pickedTeams.Count ? pickedTeams[roomIndex].Members : new List<RosterOperator>();
                    AddRoom(targetShift, kind, members);
                }
            }

            foreach (ShiftPlan shift in new[] { shiftA, shiftB })
            {
                ParkRequiredOperators(roster, shift, usedOperators);
                ControlCenterPlanner.FillMoraleSeats(roster, shift, usedOperators);
            }

            return EvaluatePlan(shiftA, shiftB, goldFactoryCount, checkInIntervalHours);
        }

        /// <summary>
        /// Takes the best-ranked teams that share no operator with each other or with anyone already placed.
        /// </summary>
        private static List<RankedTeam> PickDisjointTeams(List<RankedTeam> rankedTeams, int teamCount, HashSet<RosterOperator> usedOperators)
        {
            List<RankedTeam> pickedTeams = new();
            foreach (RankedTeam team in rankedTeams)
            {
                if (pickedTeams.Count == teamCount)
                {
                    break;
                }
                if (team.Members.Any(usedOperators.Contains))
                {
                    continue;
                }
                pickedTeams.Add(team);
                usedOperators.UnionWith(team.Members);
            }
            return pickedTeams;
        }

        /// <summary>
        /// Adds a Trading Post or Factory with these members to a shift.
        /// </summary>
        private static void AddRoom(ShiftPlan shift, ProductionRoomKind kind, List<RosterOperator> members)
        {
            if (kind == ProductionRoomKind.TradingPost)
            {
                shift.TradingPosts.Add(members);
                return;
            }
            shift.Factories.Add(new PlannedFactory(TeamGenerator.GetFormula(kind), members));
        }

        /// <summary>
        /// Places operators that working skills need "in the base", in their own side room if possible, otherwise a Power Plant.
        /// </summary>
        private static void ParkRequiredOperators(List<RosterOperator> roster, ShiftPlan shift, HashSet<RosterOperator> usedOperators)
        {
            foreach (string requiredName in PartnerPlacement.GetRequiredNames(shift))
            {
                if (shift.AllWorkingOperators().Any(rosterOperator => string.Equals(rosterOperator.Name, requiredName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                RosterOperator? requiredOperator = roster.FirstOrDefault(rosterOperator => string.Equals(rosterOperator.Name, requiredName, StringComparison.OrdinalIgnoreCase));
                if (requiredOperator == null || usedOperators.Contains(requiredOperator))
                {
                    continue;
                }
                if (PartnerPlacement.Place(shift, requiredOperator))
                {
                    usedOperators.Add(requiredOperator);
                }
            }
        }

        /// <summary>
        /// Calculates every room in a shift using that shift's real Control Center buffs and who is actually in the base.
        /// </summary>
        private static ShiftResult EvaluateShift(ShiftPlan shift, double checkInIntervalHours)
        {
            BaseContext context = new()
            {
                CheckInIntervalHours = checkInIntervalHours,
                OperatorsInBase = shift.AllWorkingOperators(),
                ControlCenterTradingBonus = ControlCenterPlanner.GetBestBuff(shift.ControlCenter, EffectType.TradingEfficiency, EffectScope.AllTradingPosts),
                ControlCenterFactoryBonus = ControlCenterPlanner.GetBestBuff(shift.ControlCenter, EffectType.FactoryProductivity, EffectScope.AllFactories),
                ControlCenterMoraleRelay = ControlCenterPlanner.GetMoraleRelay(shift.ControlCenter)
            };

            return new ShiftResult
            {
                Plan = shift,
                TradingResults = shift.TradingPosts
                    .Select(tradingPost => TradingPostCalculator.Calculate(tradingPost, context))
                    .ToList(),
                FactoryResults = shift.Factories
                    .Select(factory => FactoryCalculator.Calculate(factory.Members, factory.Formula, context))
                    .ToList()
            };
        }

        /// <summary>
        /// All 6 orders in which the three room kinds can be filled.
        /// </summary>
        private static IEnumerable<ProductionRoomKind[]> GetFillOrders()
        {
            ProductionRoomKind[] kinds = Enum.GetValues<ProductionRoomKind>();
            foreach (ProductionRoomKind firstKind in kinds)
            {
                foreach (ProductionRoomKind secondKind in kinds.Where(kind => kind != firstKind))
                {
                    foreach (ProductionRoomKind thirdKind in kinds.Where(kind => kind != firstKind && kind != secondKind))
                    {
                        yield return new[] { firstKind, secondKind, thirdKind };
                    }
                }
            }
        }
    }
}
