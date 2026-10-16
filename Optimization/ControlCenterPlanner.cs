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
    /// Chooses Control Center operators: the strongest Trading Post and Factory buffers first, then morale operators.
    /// </summary>
    public static class ControlCenterPlanner
    {
        private const int ControlCenterSeats = 5;

        /// <summary>
        /// Gives each shift the best free Factory buffer and, if the plan sells gold, the best Trading Post buffer. Shift A gets first pick.
        /// </summary>
        public static void ReserveBuffers(List<RosterOperator> roster, ShiftPlan shiftA, ShiftPlan shiftB, HashSet<RosterOperator> usedOperators, bool includeTradingBuffer)
        {
            List<(EffectType EffectType, EffectScope Scope)> buffKinds = new() { (EffectType.FactoryProductivity, EffectScope.AllFactories) };
            if (includeTradingBuffer)
            {
                buffKinds.Insert(0, (EffectType.TradingEfficiency, EffectScope.AllTradingPosts));
            }

            foreach ((EffectType effectType, EffectScope scope) in buffKinds)
            {
                List<RosterOperator> buffers = roster
                    .Where(rosterOperator => !usedOperators.Contains(rosterOperator) && GetBuffValue(rosterOperator, effectType, scope) > 0)
                    .OrderByDescending(rosterOperator => GetBuffValue(rosterOperator, effectType, scope))
                    .Take(2)
                    .ToList();

                if (buffers.Count > 0)
                {
                    shiftA.ControlCenter.Add(buffers[0]);
                    usedOperators.Add(buffers[0]);
                }
                if (buffers.Count > 1)
                {
                    shiftB.ControlCenter.Add(buffers[1]);
                    usedOperators.Add(buffers[1]);
                }
            }
        }

        /// <summary>
        /// Fills the remaining Control Center seats with free operators that have an unconditional morale skill there.
        /// </summary>
        public static void FillMoraleSeats(List<RosterOperator> roster, ShiftPlan shift, HashSet<RosterOperator> usedOperators)
        {
            List<RosterOperator> moraleOperators = roster
                .Where(rosterOperator => !usedOperators.Contains(rosterOperator) && HasUnconditionalControlMorale(rosterOperator))
                .ToList();

            foreach (RosterOperator moraleOperator in moraleOperators)
            {
                if (shift.ControlCenter.Count >= ControlCenterSeats)
                {
                    break;
                }
                shift.ControlCenter.Add(moraleOperator);
                usedOperators.Add(moraleOperator);
            }
        }

        /// <summary>
        /// The strongest unconditional Control Center buff of this kind among the given operators (only the highest counts in game).
        /// </summary>
        public static double GetBestBuff(IEnumerable<RosterOperator> controlCenterOperators, EffectType effectType, EffectScope scope)
        {
            return controlCenterOperators
                .Select(rosterOperator => GetBuffValue(rosterOperator, effectType, scope))
                .DefaultIfEmpty(0)
                .Max();
        }

        /// <summary>
        /// This operator's unconditional Control Center buff of the given kind, or 0.
        /// </summary>
        private static double GetBuffValue(RosterOperator rosterOperator, EffectType effectType, EffectScope scope)
        {
            return rosterOperator.ActiveSkills
                .Where(skill => skill.Room == RoomType.Control)
                .SelectMany(skill => skill.Effects)
                .Where(skillEffect => skillEffect.Type == effectType && skillEffect.Scope == scope && skillEffect.Condition == null)
                .Select(skillEffect => skillEffect.Value)
                .DefaultIfEmpty(0)
                .Max();
        }

        /// <summary>
        /// Młynar's relay: with him in the Control Center, each unconditional -0.05 morale skill there (his own included)
        /// also lowers morale drain in Factories and Trading Posts by 0.05.
        /// </summary>
        public static double GetMoraleRelay(IReadOnlyList<RosterOperator> controlCenterOperators)
        {
            bool hasRelay = controlCenterOperators.Any(rosterOperator => rosterOperator.EffectsOfType(EffectType.ControlCenterMoraleRelay).Any());
            return hasRelay ? 0.05 * controlCenterOperators.Count(HasUnconditionalControlMorale) : 0;
        }

        /// <summary>
        /// True if the operator has a Control Center morale skill that needs no partner.
        /// </summary>
        private static bool HasUnconditionalControlMorale(RosterOperator rosterOperator)
        {
            return rosterOperator.ActiveSkills
                .Where(skill => skill.Room == RoomType.Control)
                .SelectMany(skill => skill.Effects)
                .Any(skillEffect => skillEffect.Type == EffectType.MoraleRecovery && skillEffect.Scope == EffectScope.ControlCenter && skillEffect.Condition == null);
        }
    }
}
