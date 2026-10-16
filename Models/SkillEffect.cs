using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// What an effect changes.
    /// </summary>
    public enum EffectType
    {
        TradingEfficiency,
        TradingOrderLimit,
        TradingGoldBonus,
        TradingSpecialOrders,
        TradingEfficiencyPerVacantOrder,
        TradingOrderLimitTradeOff,
        TradingOrderQuality,
        FactoryProductivity,
        FactoryCapacity,
        FactoryZeroOthers,
        FactoryProductivityPerOperator,
        FactoryCapacityPerOperator,
        FactoryProductivityPerPowerPlant,
        FactoryProductivityPerCapacity,
        FactoryProductivityPerCapacityTiered,
        PowerRecovery,
        MoraleDrain,
        MoraleRecovery,
        ControlCenterMoraleRelay,
        IgnoreRoomMoraleEffects,
        Unsupported
    }

    /// <summary>
    /// Who an effect applies to: the operator's own contribution, their room, or every facility of a type.
    /// </summary>
    public enum EffectScope
    {
        Self,
        OthersInRoom,
        AllInRoom,
        AllTradingPosts,
        AllFactories,
        ControlCenter,
        AllDormitories,
        PinusSylvestrisInFactories
    }

    /// <summary>
    /// Which factory recipe an effect is limited to.
    /// </summary>
    public enum FactoryFormula
    {
        Any,
        BattleRecord,
        PreciousMetal,
        Originium
    }

    /// <summary>
    /// Requirements that must hold for an effect to apply. Every property is optional; null or false means "no requirement".
    /// </summary>
    public class EffectCondition
    {
        public string? PartnerInRoom { get; init; }
        public string? OperatorInBase { get; init; }
        public string? FactionInRoom { get; init; }
        public string? PerFactionInRoom { get; init; }
        public string? PerFactionInBase { get; init; }
        public string? PerFactionInTradingPost { get; init; }
        public string? PerFacilityWithFaction { get; init; }
        public string? FactionInOtherPowerPlant { get; init; }
        public string? PerSkillTypeInRoom { get; init; }
        public bool PerOtherOperatorInRoom { get; init; }
        public bool ScalesWithRoomEfficiency { get; init; }
        public bool ScalesWithRoomProductivity { get; init; }
        public bool WorkPlatformInOtherPowerPlant { get; init; }
        public int? WorkPlatformsInPowerPlants { get; init; }
    }

    /// <summary>
    /// One numeric effect of a base skill, e.g. +30% Trading Post order efficiency.
    /// </summary>
    public class SkillEffect
    {
        public EffectType Type { get; init; }
        public double Value { get; init; }
        public EffectScope Scope { get; init; }
        public FactoryFormula Formula { get; init; }
        public EffectCondition? Condition { get; init; }
        public string? Note { get; init; }
    }
}
