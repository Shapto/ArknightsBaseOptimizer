using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// One step of a skill slot: which skill it becomes at which promotion and level.
    /// </summary>
    public class SkillUnlock
    {
        public string BuffId { get; init; } = "";
        public int Elite { get; init; }
        public int Level { get; init; }
    }

    /// <summary>
    /// One of an operator's base skill slots. Later unlocks in the list replace earlier ones (α becomes β).
    /// </summary>
    public class SkillSlot
    {
        public List<SkillUnlock> Unlocks { get; init; } = new();
    }

    /// <summary>
    /// An operator's base skill slots as stored in base_skills.json, independent of whether you own them.
    /// </summary>
    public class OperatorSkillData
    {
        public string CharId { get; init; } = "";
        public string Name { get; init; } = "";
        public int Rarity { get; init; }
        public List<SkillSlot> Slots { get; init; } = new();
        public List<string> Factions { get; init; } = new();
    }

    /// <summary>
    /// The whole base_skills.json file: every operator's slots plus every skill definition keyed by buff ID.
    /// </summary>
    public class BaseSkillDatabase
    {
        public List<OperatorSkillData> Operators { get; init; } = new();
        public Dictionary<string, BaseSkill> Skills { get; init; } = new();
    }
}
