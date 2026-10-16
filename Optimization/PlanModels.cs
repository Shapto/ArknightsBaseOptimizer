using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Optimization
{
    public class PlanModels
    {
        /// <summary>
        /// Which number a button ranks setups by.
        /// </summary>
        public enum OptimizationGoal
        {
            Lmd,
            Experience,
            Balanced
        }

        /// <summary>
        /// The three kinds of production room the search fills.
        /// </summary>
        public enum ProductionRoomKind
        {
            TradingPost,
            GoldFactory,
            ExperienceFactory
        }

        /// <summary>
        /// One possible team for a room kind and what it produces per check-in. Value is orders for Trading Posts and items for Factories.
        /// </summary>
        public record RankedTeam(ProductionRoomKind Kind, List<RosterOperator> Members, double Value, int Capacity);

        /// <summary>
        /// One Factory in a shift: what it makes and who works there.
        /// </summary>
        public record PlannedFactory(FactoryFormula Formula, List<RosterOperator> Members);

        /// <summary>
        /// Who works where during one shift (one check-in interval).
        /// </summary>
        public class ShiftPlan
        {
            public List<RosterOperator> ControlCenter { get; } = new();
            public List<List<RosterOperator>> TradingPosts { get; } = new();
            public List<PlannedFactory> Factories { get; } = new();
            public List<RosterOperator> PowerPlants { get; } = new();

            public Dictionary<RoomType, List<RosterOperator>> SupportRooms { get; } = new();

            /// <summary>
            /// Everyone working this shift, in any room.
            /// </summary>
            public List<RosterOperator> AllWorkingOperators()
            {
                return ControlCenter
                    .Concat(TradingPosts.SelectMany(tradingPost => tradingPost))
                    .Concat(Factories.SelectMany(factory => factory.Members))
                    .Concat(PowerPlants)
                    .Concat(SupportRooms.Values.SelectMany(occupants => occupants))
                    .ToList();
            }

            /// <summary>
            /// The occupants of a side room (Reception, Office, Workshop, Training), created empty on first use.
            /// </summary>
            public List<RosterOperator> GetSupportRoom(RoomType room)
            {
                if (!SupportRooms.TryGetValue(room, out List<RosterOperator>? occupants))
                {
                    occupants = new List<RosterOperator>();
                    SupportRooms[room] = occupants;
                }
                return occupants;
            }

            /// <summary>
            /// A copy with its own room lists, so changing the copy doesn't change this plan.
            /// </summary>
            public ShiftPlan Clone()
            {
                ShiftPlan copy = new();
                copy.ControlCenter.AddRange(ControlCenter);
                copy.PowerPlants.AddRange(PowerPlants);
                foreach (List<RosterOperator> tradingPost in TradingPosts)
                {
                    copy.TradingPosts.Add(new List<RosterOperator>(tradingPost));
                }
                foreach (PlannedFactory factory in Factories)
                {
                    copy.Factories.Add(new PlannedFactory(factory.Formula, new List<RosterOperator>(factory.Members)));
                }
                foreach ((RoomType room, List<RosterOperator> occupants) in SupportRooms)
                {
                    copy.SupportRooms[room] = new List<RosterOperator>(occupants);
                }
                return copy;
            }
        }

        /// <summary>
        /// A shift plan together with what each of its rooms produced.
        /// </summary>
        public class ShiftResult
        {
            public required ShiftPlan Plan { get; init; }
            public required List<TradingPostResult> TradingResults { get; init; }
            public required List<FactoryResult> FactoryResults { get; init; }

            public double Experience => FactoryResults.Sum(factoryResult => factoryResult.Experience);
            public double GoldProduced => FactoryResults.Sum(factoryResult => factoryResult.GoldBars);
            public double LmdIfGoldAvailable => TradingResults.Sum(tradingResult => tradingResult.LmdEarned);
            public double GoldNeeded => TradingResults.Sum(tradingResult => tradingResult.GoldUsed);
        }

        /// <summary>
        /// A full two-shift plan. Shifts alternate each check-in, so per-check-in numbers are the average of both.
        /// </summary>
        public class BasePlanResult
        {
            public required ShiftResult ShiftA { get; init; }
            public required ShiftResult ShiftB { get; init; }
            public required int GoldFactoryCount { get; init; }

            public double ExperiencePerCheckIn => (ShiftA.Experience + ShiftB.Experience) / 2;
            public double GoldProducedPerCheckIn => (ShiftA.GoldProduced + ShiftB.GoldProduced) / 2;
            public double GoldNeededPerCheckIn => (ShiftA.GoldNeeded + ShiftB.GoldNeeded) / 2;
            public double GoldSupplyRatio => GoldNeededPerCheckIn <= 0 ? 0 : Math.Min(1, GoldProducedPerCheckIn / GoldNeededPerCheckIn);

            /// <summary>
            /// LMD actually earned: Trading Posts can only sell the gold the Factories make.
            /// </summary>
            public double LmdPerCheckIn => (ShiftA.LmdIfGoldAvailable + ShiftB.LmdIfGoldAvailable) / 2 * GoldSupplyRatio;
        }

        /// <summary>
        /// The best plan for each button.
        /// </summary>
        public record OptimizationResults(BasePlanResult Lmd, BasePlanResult Experience, BasePlanResult Balanced);
    }
}
