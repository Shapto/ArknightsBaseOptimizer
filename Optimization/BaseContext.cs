using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Optimization
{
    public class BaseContext
    {
        public double CheckInIntervalHours { get; init; } = 24;
        public List<RosterOperator> OperatorsInBase { get; init; } = new();
        public double ControlCenterTradingBonus { get; init; }
        public double ControlCenterFactoryBonus { get; init; }
        public double AbyssalHunterBonusHours { get; init; }

        public double ControlCenterMoraleRelay { get; init; }

        /// <summary>
        /// True if an operator with this exact name is working somewhere in the base.
        /// </summary>
        public bool IsInBase(string operatorName)
        {
            return OperatorsInBase.Any(rosterOperator => string.Equals(rosterOperator.Name, operatorName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Number of working operators in the base with the given faction, optionally not counting one operator.
        /// </summary>
        public int CountInBase(string faction, RosterOperator? excludedOperator)
        {
            return OperatorsInBase.Count(rosterOperator => rosterOperator != excludedOperator && rosterOperator.Factions.Contains(faction));
        }
    }
}
