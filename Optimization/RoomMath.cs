using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Optimization
{
    public static class RoomMath
    {
        /// <summary>
        /// How many times an effect applies for its owner in this room: 0 = inactive, 1 = active, n = "per X" effects.
        /// </summary>
        public static double GetConditionMultiplier(SkillEffect skillEffect, RosterOperator owner, IReadOnlyList<RosterOperator> roomOperators, BaseContext context)
        {
            EffectCondition? condition = skillEffect.Condition;
            if (condition == null)
            {
                return 1;
            }

            List<RosterOperator> otherOperators = roomOperators.Where(roomOperator => roomOperator != owner).ToList();

            if (condition.PartnerInRoom != null)
            {
                return otherOperators.Any(otherOperator => IsNamed(otherOperator, condition.PartnerInRoom)) ? 1 : 0;
            }
            if (condition.OperatorInBase != null)
            {
                return context.IsInBase(condition.OperatorInBase) ? 1 : 0;
            }
            if (condition.FactionInRoom != null)
            {
                return otherOperators.Any(otherOperator => otherOperator.Factions.Contains(condition.FactionInRoom)) ? 1 : 0;
            }
            if (condition.PerFactionInRoom != null)
            {
                return otherOperators.Count(otherOperator => otherOperator.Factions.Contains(condition.PerFactionInRoom));
            }
            if (condition.PerFactionInBase != null)
            {
                return Math.Min(5, context.CountInBase(condition.PerFactionInBase, owner));
            }
            if (condition.PerFacilityWithFaction != null)
            {
                return Math.Min(5, context.CountInBase(condition.PerFacilityWithFaction, null));
            }
            if (condition.PerSkillTypeInRoom != null)
            {
                return roomOperators
                    .SelectMany(roomOperator => roomOperator.ActiveSkills)
                    .Count(skill => skill.Name.Contains(condition.PerSkillTypeInRoom, StringComparison.OrdinalIgnoreCase));
            }
            if (condition.PerOtherOperatorInRoom)
            {
                return otherOperators.Count;
            }
            if (IsScaling(skillEffect))
            {
                return 1;
            }

            // Power Plant and Control Center conditions are not modelled for production rooms.
            return 0;
        }

        /// <summary>
        /// Hours this operator works before running out of morale, capped at the check-in interval.
        /// Młynar's relay lowers drain for everyone; Waai Fu ignores morale effects from other operators in the room.
        /// </summary>
        public static double CalculateHoursWorked(RosterOperator worker, IReadOnlyList<RosterOperator> roomOperators, FactoryFormula formula, BaseContext context)
        {
            double drainPerHour = GameConstants.BaseMoraleDrainPerHour + GameConstants.LevelThreeRoomMoraleDrain + GameConstants.ControlCenterMoraleDrain - context.ControlCenterMoraleRelay;
            bool ignoresOthers = worker.EffectsOfType(EffectType.IgnoreRoomMoraleEffects).Any();

            foreach (RosterOperator roomOperator in roomOperators)
            {
                if (ignoresOthers && roomOperator != worker)
                {
                    continue;
                }
                foreach (SkillEffect skillEffect in roomOperator.EffectsOfType(EffectType.MoraleDrain))
                {
                    bool appliesToWorker = skillEffect.Scope == EffectScope.AllInRoom || (skillEffect.Scope == EffectScope.Self && roomOperator == worker);
                    if (!appliesToWorker || !FormulaMatches(skillEffect.Formula, formula))
                    {
                        continue;
                    }
                    drainPerHour += skillEffect.Value * GetConditionMultiplier(skillEffect, roomOperator, roomOperators, context);
                }
            }

            return drainPerHour <= 0 ? context.CheckInIntervalHours : Math.Min(context.CheckInIntervalHours, GameConstants.MaximumMorale / drainPerHour);
        }

        /// <summary>
        /// Total work done over the interval, in hours at 100% speed. Bonuses only count while their owner (and partner) still has morale.
        /// </summary>
        public static double CalculateWorkHours(IReadOnlyList<RosterOperator> roomOperators, EffectType productivityType, FactoryFormula formula, double controlCenterBonus, BaseContext context)
        {
            Dictionary<RosterOperator, double> hoursWorked = roomOperators.ToDictionary(roomOperator => roomOperator, roomOperator => CalculateHoursWorked(roomOperator, roomOperators, formula, context));
            double roomHours = hoursWorked.Count == 0 ? 0 : hoursWorked.Values.Max();

            double workHours = roomHours * (1 + controlCenterBonus / 100);
            Dictionary<RosterOperator, double> flatPercentByOperator = new();

            foreach (RosterOperator owner in roomOperators)
            {
                double ownerHours = hoursWorked[owner];
                workHours += ownerHours * GameConstants.ProductivityPerWorkingOperator / 100;
                double flatPercent = 0;

                foreach (SkillEffect skillEffect in owner.EffectsOfType(productivityType))
                {
                    if (!FormulaMatches(skillEffect.Formula, formula) || IsScaling(skillEffect))
                    {
                        continue;
                    }

                    double percent = skillEffect.Value * GetConditionMultiplier(skillEffect, owner, roomOperators, context);
                    double effectHours = GetPartnerLimitedHours(skillEffect, ownerHours, hoursWorked);

                    if (skillEffect.Scope == EffectScope.OthersInRoom)
                    {
                        foreach (RosterOperator otherOperator in roomOperators.Where(roomOperator => roomOperator != owner))
                        {
                            workHours += percent / 100 * Math.Min(effectHours, hoursWorked[otherOperator]);
                        }
                        continue;
                    }

                    if (skillEffect.Scope == EffectScope.Self)
                    {
                        workHours += percent / 100 * effectHours;
                        flatPercent += percent;
                    }
                }

                flatPercentByOperator[owner] = flatPercent;
            }

            // "+5% per 5% from others" skills need everyone else's total first.
            foreach (RosterOperator owner in roomOperators)
            {
                foreach (SkillEffect skillEffect in owner.EffectsOfType(productivityType).Where(IsScaling))
                {
                    double othersPercent = flatPercentByOperator
                        .Where(pair => pair.Key != owner)
                        .Sum(pair => pair.Value);
                    double percent = Math.Min(skillEffect.Value, Math.Floor(othersPercent / 5) * 5);
                    workHours += percent / 100 * hoursWorked[owner];
                }
            }

            return workHours;
        }

        /// <summary>
        /// Sums a flat effect (capacity, order limit) across the room, respecting formula and conditions.
        /// </summary>
        public static double SumRoomEffect(IReadOnlyList<RosterOperator> roomOperators, EffectType effectType, FactoryFormula formula, BaseContext context)
        {
            return roomOperators
                .SelectMany(owner => owner.EffectsOfType(effectType).Select(skillEffect => (Owner: owner, SkillEffect: skillEffect)))
                .Where(pair => FormulaMatches(pair.SkillEffect.Formula, formula))
                .Sum(pair => pair.SkillEffect.Value * GetConditionMultiplier(pair.SkillEffect, pair.Owner, roomOperators, context));
        }

        /// <summary>
        /// True if an effect limited to one recipe applies to the recipe the room is making.
        /// </summary>
        public static bool FormulaMatches(FactoryFormula effectFormula, FactoryFormula roomFormula)
        {
            return effectFormula == FactoryFormula.Any || effectFormula == roomFormula;
        }

        /// <summary>
        /// True for "+5% per 5% from others" style effects.
        /// </summary>
        private static bool IsScaling(SkillEffect skillEffect)
        {
            return skillEffect.Condition != null && (skillEffect.Condition.ScalesWithRoomEfficiency || skillEffect.Condition.ScalesWithRoomProductivity);
        }

        /// <summary>
        /// A partner bonus only lasts while both the owner and the partner are still working.
        /// </summary>
        private static double GetPartnerLimitedHours(SkillEffect skillEffect, double ownerHours, Dictionary<RosterOperator, double> hoursWorked)
        {
            string? partnerName = skillEffect.Condition?.PartnerInRoom;
            if (partnerName == null)
            {
                return ownerHours;
            }

            RosterOperator? partner = hoursWorked.Keys.FirstOrDefault(roomOperator => IsNamed(roomOperator, partnerName));
            return partner == null ? 0 : Math.Min(ownerHours, hoursWorked[partner]);
        }

        /// <summary>
        /// Exact, case-insensitive name match ("Exusiai" does not match "Exusiai the New Covenant").
        /// </summary>
        private static bool IsNamed(RosterOperator rosterOperator, string operatorName)
        {
            return string.Equals(rosterOperator.Name, operatorName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
