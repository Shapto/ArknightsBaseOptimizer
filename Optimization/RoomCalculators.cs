using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Optimization
{
    /// <summary>
    /// What one Factory produced between two check-ins.
    /// </summary>
    public record FactoryResult(FactoryFormula Formula, double ItemsProduced, int ItemCapacity, double HoursUntilFull)
    {
        public double Experience => Formula == FactoryFormula.BattleRecord ? ItemsProduced * GameConstants.ExperiencePerBattleRecord : 0;
        public double GoldBars => Formula == FactoryFormula.PreciousMetal ? ItemsProduced : 0;
    }

    /// <summary>
    /// What one Trading Post earned between two check-ins.
    /// </summary>
    public record TradingPostResult(double OrdersCompleted, int OrderLimit, double LmdEarned, double GoldUsed, double HoursUntilFull);

    /// <summary>
    /// Calculates Factory output over a check-in interval, capped by storage.
    /// </summary>
    public static class FactoryCalculator
    {
        /// <summary>
        /// Output of a Factory staffed by these operators making the given recipe.
        /// </summary>
        public static FactoryResult Calculate(IReadOnlyList<RosterOperator> roomOperators, FactoryFormula formula, BaseContext context)
        {
            double recipeHours = formula == FactoryFormula.BattleRecord ? GameConstants.BattleRecordHours : GameConstants.GoldBarHours;
            int recipeWeight = formula == FactoryFormula.BattleRecord ? GameConstants.BattleRecordWeight : GameConstants.GoldBarWeight;

            Dictionary<RosterOperator, double> hoursWorked = roomOperators.ToDictionary(roomOperator => roomOperator, roomOperator => RoomMath.CalculateHoursWorked(roomOperator, roomOperators, formula, context));
            bool othersZeroed = roomOperators.Any(roomOperator => roomOperator.EffectsOfType(EffectType.FactoryZeroOthers).Any());

            // Automation-style skills cancel all normal productivity, Control Center buffs included; only facility-count bonuses remain.
            // EffectType.Unsupported is used as "no productivity type" so only the base speed and per-operator innate bonus are counted.
            double workHours = othersZeroed ? RoomMath.CalculateWorkHours(roomOperators, EffectType.Unsupported, formula, 0, context) : RoomMath.CalculateWorkHours(roomOperators, EffectType.FactoryProductivity, formula, context.ControlCenterFactoryBonus, context);

            double capacityBonus = RoomMath.SumRoomEffect(roomOperators, EffectType.FactoryCapacity, formula, context);
            foreach (RosterOperator owner in roomOperators)
            {
                workHours += SumValues(owner, EffectType.FactoryProductivityPerOperator) * roomOperators.Count / 100 * hoursWorked[owner];
                workHours += SumValues(owner, EffectType.FactoryProductivityPerPowerPlant) * GameConstants.PowerPlantCount / 100 * hoursWorked[owner];
                capacityBonus += SumValues(owner, EffectType.FactoryCapacityPerOperator) * roomOperators.Count;
            }

            if (!othersZeroed)
            {
                workHours += CalculateCapacityBasedWorkHours(roomOperators, formula, capacityBonus, hoursWorked, context);
                workHours += CalculateAbyssalHunterWorkHours(roomOperators, formula, context);
            }

            int itemCapacity = (int)Math.Floor((GameConstants.FactoryBaseCapacity + capacityBonus) / recipeWeight);
            double itemsMade = workHours / recipeHours;
            double itemsPerHour = itemsMade / context.CheckInIntervalHours;
            double hoursUntilFull = itemsPerHour <= 0 ? double.PositiveInfinity : itemCapacity / itemsPerHour;

            return new FactoryResult(formula, Math.Min(itemsMade, itemCapacity), itemCapacity, hoursUntilFull);
        }

        /// <summary>
        /// Vermeil-style "+2% per storage added" and Bubble-style tiered "+1%/+3% per storage from each operator" skills.
        /// </summary>
        private static double CalculateCapacityBasedWorkHours(IReadOnlyList<RosterOperator> roomOperators, FactoryFormula formula, double capacityBonus, Dictionary<RosterOperator, double> hoursWorked, BaseContext context)
        {
            double workHours = 0;
            foreach (RosterOperator owner in roomOperators)
            {
                workHours += SumValues(owner, EffectType.FactoryProductivityPerCapacity) * capacityBonus / 100 * hoursWorked[owner];

                if (!owner.EffectsOfType(EffectType.FactoryProductivityPerCapacityTiered).Any())
                {
                    continue;
                }

                double tieredPercent = 0;
                foreach (RosterOperator member in roomOperators)
                {
                    double memberCapacity = GetOperatorCapacityBonus(member, roomOperators, formula, context);
                    tieredPercent += memberCapacity > 16 ? memberCapacity * 3 : Math.Max(0, memberCapacity);
                }
                workHours += tieredPercent / 100 * hoursWorked[owner];
            }
            return workHours;
        }

        /// <summary>
        /// The storage one operator adds to this Factory, respecting recipe and conditions.
        /// </summary>
        private static double GetOperatorCapacityBonus(RosterOperator member, IReadOnlyList<RosterOperator> roomOperators, FactoryFormula formula, BaseContext context)
        {
            return member.EffectsOfType(EffectType.FactoryCapacity)
                .Where(skillEffect => RoomMath.FormulaMatches(skillEffect.Formula, formula))
                .Sum(skillEffect => skillEffect.Value * RoomMath.GetConditionMultiplier(skillEffect, member, roomOperators, context));
        }

        /// <summary>
        /// Gladiia's special bonus: each Abyssal Hunter gets +10% per Abyssal Hunter in the Factory (max 90%), while Gladiia lasts.
        /// </summary>
        private static double CalculateAbyssalHunterWorkHours(IReadOnlyList<RosterOperator> roomOperators, FactoryFormula formula, BaseContext context)
        {
            List<RosterOperator> abyssalHunters = roomOperators
                .Where(roomOperator => roomOperator.Factions.Contains(GameConstants.AbyssalHunterFaction))
                .ToList();
            if (context.AbyssalHunterBonusHours <= 0 || abyssalHunters.Count == 0)
            {
                return 0;
            }

            double abyssalPercent = Math.Min(90, 10 * abyssalHunters.Count * abyssalHunters.Count);
            double shortestHunterHours = abyssalHunters.Min(hunter => RoomMath.CalculateHoursWorked(hunter, roomOperators, formula, context));
            return abyssalPercent / 100 * Math.Min(context.AbyssalHunterBonusHours, shortestHunterHours);
        }

        /// <summary>
        /// Sum of all effect values of one type on an operator (these special types have no conditions).
        /// </summary>
        private static double SumValues(RosterOperator rosterOperator, EffectType effectType)
        {
            return rosterOperator.EffectsOfType(effectType).Sum(skillEffect => skillEffect.Value);
        }
    }

    /// <summary>
    /// Calculates Trading Post income over a check-in interval, capped by the order limit.
    /// </summary>
    public static class TradingPostCalculator
    {
        /// <summary>
        /// Income of a Trading Post staffed by these operators, including Closure's special orders and Jaye's empty-slot bonus.
        /// </summary>
        public static TradingPostResult Calculate(IReadOnlyList<RosterOperator> roomOperators, BaseContext context)
        {
            double intervalHours = context.CheckInIntervalHours;
            double workHours = RoomMath.CalculateWorkHours(roomOperators, EffectType.TradingEfficiency, FactoryFormula.Any, context.ControlCenterTradingBonus, context);
            int orderLimit = GameConstants.TradingPostBaseOrderLimit + (int)RoomMath.SumRoomEffect(roomOperators, EffectType.TradingOrderLimit, FactoryFormula.Any, context);

            // Jaye E1: -1 order limit per 10% from others (min 1), then +4% per order limit.
            foreach (RosterOperator owner in roomOperators)
            {
                foreach (SkillEffect tradeOff in owner.EffectsOfType(EffectType.TradingOrderLimitTradeOff))
                {
                    double othersPercent = 0;
                    foreach (RosterOperator otherOperator in roomOperators.Where(roomOperator => roomOperator != owner))
                    {
                        othersPercent += otherOperator.EffectsOfType(EffectType.TradingEfficiency)
                            .Where(skillEffect => skillEffect.Scope == EffectScope.Self)
                            .Sum(skillEffect => skillEffect.Value * RoomMath.GetConditionMultiplier(skillEffect, otherOperator, roomOperators, context));
                    }
                    orderLimit = Math.Max(1, orderLimit - (int)Math.Floor(othersPercent / 10));
                    workHours += tradeOff.Value * orderLimit / 100 * RoomMath.CalculateHoursWorked(owner, roomOperators, FactoryFormula.Any, context);
                }
            }

            (double orderMinutes, double orderLmd, double orderGold) = GetOrderAverages(roomOperators, intervalHours);

            double ordersPerHourAtBaseSpeed = 60 / orderMinutes;
            double vacantSlotBonus = roomOperators
                .SelectMany(roomOperator => roomOperator.EffectsOfType(EffectType.TradingEfficiencyPerVacantOrder))
                .Sum(skillEffect => skillEffect.Value) / 100;

            double ordersTaken;
            double hoursUntilFull;
            if (vacantSlotBonus > 0)
            {
                // Jaye E0: speed drops as orders pile up. dN/dt = k * (averageSpeed + bonus * (limit - N)), solved exactly.
                double averageSpeed = workHours / intervalHours;
                double settlingPoint = (averageSpeed + vacantSlotBonus * orderLimit) / vacantSlotBonus;
                double decayRate = vacantSlotBonus * ordersPerHourAtBaseSpeed;
                ordersTaken = settlingPoint * (1 - Math.Exp(-decayRate * intervalHours));
                hoursUntilFull = -Math.Log(1 - orderLimit / settlingPoint) / decayRate;
            }
            else
            {
                ordersTaken = workHours * ordersPerHourAtBaseSpeed;
                double ordersPerHour = ordersTaken / intervalHours;
                hoursUntilFull = ordersPerHour <= 0 ? double.PositiveInfinity : orderLimit / ordersPerHour;
            }

            double ordersCompleted = Math.Min(ordersTaken, orderLimit);
            return new TradingPostResult(ordersCompleted, orderLimit, ordersCompleted * orderLmd, ordersCompleted * orderGold, hoursUntilFull);
        }

        /// <summary>
        /// Average time, LMD and gold per order. Closure replaces every order with her special order; otherwise
        /// "better orders" skills shift the 4/3/2-gold mix after their ramp-up, and Tequila adds LMD to 4-gold orders.
        /// </summary>
        private static (double Minutes, double Lmd, double Gold) GetOrderAverages(IReadOnlyList<RosterOperator> roomOperators, double intervalHours)
        {
            if (roomOperators.Any(roomOperator => roomOperator.EffectsOfType(EffectType.TradingSpecialOrders).Any()))
            {
                return (GameConstants.SpecialOrderMinutes, GameConstants.SpecialOrderLmd, GameConstants.SpecialOrderGold);
            }

            List<double> qualityLevels = roomOperators
                .SelectMany(roomOperator => roomOperator.EffectsOfType(EffectType.TradingOrderQuality))
                .Select(skillEffect => skillEffect.Value)
                .ToList();
            (double[] targetMix, double rampHours) = GetTargetOrderMix(qualityLevels);

            // The improved mix only applies after the ramp-up; over the interval, count it as active for (interval - ramp / 2) hours.
            double rampedShare = rampHours <= 0 ? 1 : Math.Max(0, 1 - rampHours / (2 * intervalHours));
            double fourGoldBonus = roomOperators
                .SelectMany(roomOperator => roomOperator.EffectsOfType(EffectType.TradingGoldBonus))
                .Sum(skillEffect => skillEffect.Value);

            double averageMinutes = 0;
            double averageLmd = 0;
            double averageGold = 0;
            for (int orderType = 0; orderType < GameConstants.BaseOrderMix.Length; orderType++)
            {
                double share = rampedShare * targetMix[orderType] + (1 - rampedShare) * GameConstants.BaseOrderMix[orderType];
                double bonusLmd = orderType == 0 ? fourGoldBonus : 0;
                averageMinutes += share * GameConstants.OrderMinutes[orderType];
                averageLmd += share * (GameConstants.OrderLmd[orderType] + bonusLmd);
                averageGold += share * GameConstants.OrderGold[orderType];
            }
            return (averageMinutes, averageLmd, averageGold);
        }

        /// <summary>
        /// Order mix (4/3/2-gold shares) and ramp-up hours for the room's "better orders" skills, from the combo sheet:
        /// one "slightly" 55/30/15, two "slightly" 65/22/13, any "increased" 85/10/5.
        /// </summary>
        private static (double[] Mix, double RampHours) GetTargetOrderMix(List<double> qualityLevels)
        {
            if (qualityLevels.Any(level => level >= 2))
            {
                return (new[] { 0.85, 0.10, 0.05 }, 5);
            }
            if (qualityLevels.Count >= 2)
            {
                return (new[] { 0.65, 0.22, 0.13 }, 3);
            }
            if (qualityLevels.Count == 1)
            {
                return (new[] { 0.55, 0.30, 0.15 }, 3);
            }
            return (GameConstants.BaseOrderMix, 0);
        }
    }
}
