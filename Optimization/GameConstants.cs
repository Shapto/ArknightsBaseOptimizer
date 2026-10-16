using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Optimization
{

    /// <summary>
    /// Fixed numbers for level 3 rooms, from building_data.json and the combo sheet.
    /// </summary>
    public static class GameConstants
    {
        public const int FactoryBaseCapacity = 54;
        public const int TradingPostBaseOrderLimit = 10;
        public const double ProductivityPerWorkingOperator = 1.0;
        public const double LevelThreeRoomMoraleDrain = -0.1;
        public const double ControlCenterMoraleDrain = -0.25;
        public const double BaseMoraleDrainPerHour = 1.0;
        public const double MaximumMorale = 24.0;

        public const double BattleRecordHours = 3.0;
        public const int BattleRecordWeight = 5;
        public const int ExperiencePerBattleRecord = 2000;
        public const double GoldBarHours = 1.2;
        public const int GoldBarWeight = 2;

        // Level 3 Trading Post order mix: 20% 4-gold (2000 LMD, 276 min), 50% 3-gold (1500, 210 min), 30% 2-gold (1000, 144 min).
        public const double AverageOrderLmd = 1450.0;
        public const double AverageOrderMinutes = 203.4;
        public const double AverageOrderGold = 2.9;

        public const string AbyssalHunterFaction = "abyssal";

        // Closure's special orders (from the combo sheet): replace every LMD order.
        public const double SpecialOrderMinutes = 144.0;
        public const double SpecialOrderLmd = 1200.0;
        public const double SpecialOrderGold = 2.0;

        public const int PowerPlantCount = 3;

        // Level 3 Trading Post order types, in the order 4-gold, 3-gold, 2-gold.
        public static readonly double[] OrderMinutes = { 276, 210, 144 };
        public static readonly double[] OrderLmd = { 2000, 1500, 1000 };
        public static readonly double[] OrderGold = { 4, 3, 2 };
        public static readonly double[] BaseOrderMix = { 0.20, 0.50, 0.30 };
    }
}
