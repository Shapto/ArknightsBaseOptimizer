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
    /// Tries every team of 1–3 candidates for a room kind and ranks them by output per check-in.
    /// </summary>
    public static class TeamGenerator
    {
        /// <summary>
        /// All teams for this room kind, best first. Ties go to smaller teams (saves operators), then to more storage.
        /// </summary>
        public static List<RankedTeam> GenerateRankedTeams(List<RosterOperator> roster, ProductionRoomKind kind, BaseContext context)
        {
            List<RosterOperator> candidates = roster
                .Where(rosterOperator => IsCandidate(rosterOperator, kind))
                .ToList();

            return EnumerateTeams(candidates)
                .Select(members => EvaluateTeam(members, kind, context))
                .OrderByDescending(team => Math.Round(team.Value, 6))
                .ThenBy(team => team.Members.Count)
                .ThenByDescending(team => team.Capacity)
                .ToList();
        }

        /// <summary>
        /// The recipe a room kind makes; Trading Posts use Any.
        /// </summary>
        public static FactoryFormula GetFormula(ProductionRoomKind kind)
        {
            return kind switch { ProductionRoomKind.ExperienceFactory => FactoryFormula.BattleRecord, ProductionRoomKind.GoldFactory => FactoryFormula.PreciousMetal, _ => FactoryFormula.Any };
        }

        /// <summary>
        /// Scores one team: LMD earned for a Trading Post, items produced for a Factory.
        /// </summary>
        private static RankedTeam EvaluateTeam(List<RosterOperator> members, ProductionRoomKind kind, BaseContext context)
        {
            if (kind == ProductionRoomKind.TradingPost)
            {
                TradingPostResult tradingResult = TradingPostCalculator.Calculate(members, context);
                return new RankedTeam(kind, members, tradingResult.LmdEarned, tradingResult.OrderLimit);
            }

            FactoryResult factoryResult = FactoryCalculator.Calculate(members, GetFormula(kind), context);
            return new RankedTeam(kind, members, factoryResult.ItemsProduced, factoryResult.ItemCapacity);
        }

        /// <summary>
        /// True if the operator has a positive productivity, capacity or order-limit skill for this room kind.
        /// </summary>
        public static bool IsCandidate(RosterOperator rosterOperator, ProductionRoomKind kind)
        {
            RoomType room = kind == ProductionRoomKind.TradingPost ? RoomType.Trading : RoomType.Manufacture;
            EffectType[] usefulTypes = kind == ProductionRoomKind.TradingPost ? new[] { EffectType.TradingEfficiency, EffectType.TradingOrderLimit } : new[] { EffectType.FactoryProductivity, EffectType.FactoryCapacity };
            FactoryFormula formula = GetFormula(kind);

            return rosterOperator.ActiveSkills
                .Where(skill => skill.Room == room)
                .SelectMany(skill => skill.Effects)
                .Any(skillEffect => usefulTypes.Contains(skillEffect.Type) && skillEffect.Value > 0 && RoomMath.FormulaMatches(skillEffect.Formula, formula));
        }

        /// <summary>
        /// Every team of 1, 2 or 3 different candidates.
        /// </summary>
        private static IEnumerable<List<RosterOperator>> EnumerateTeams(List<RosterOperator> candidates)
        {
            for (int firstIndex = 0; firstIndex < candidates.Count; firstIndex++)
            {
                yield return new List<RosterOperator> { candidates[firstIndex] };
                for (int secondIndex = firstIndex + 1; secondIndex < candidates.Count; secondIndex++)
                {
                    yield return new List<RosterOperator> { candidates[firstIndex], candidates[secondIndex] };
                    for (int thirdIndex = secondIndex + 1; thirdIndex < candidates.Count; thirdIndex++)
                    {
                        yield return new List<RosterOperator> { candidates[firstIndex], candidates[secondIndex], candidates[thirdIndex] };
                    }
                }
            }
        }
    }
}
