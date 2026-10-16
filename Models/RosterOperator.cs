using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// An owned operator together with the base skills unlocked at their current promotion and level.
    /// </summary>
    public class RosterOperator
    {
        public required RosterEntry Entry { get; init; }
        public required List<BaseSkill> ActiveSkills { get; init; }
        public required List<string> Factions { get; init; }
        public required string CharId { get; init; }
        public int PreviewElite { get; init; }
        public bool PromotionChangesSkills { get; init; }

        public string Name => Entry.Name;

        /// <summary>
        /// True if this operator has at least one active skill for the given room.
        /// </summary>
        public bool HasSkillFor(RoomType room)
        {
            return ActiveSkills.Any(skill => skill.Room == room);
        }

        /// <summary>
        /// All effects of the given type across this operator's active skills.
        /// </summary>
        public IEnumerable<SkillEffect> EffectsOfType(EffectType effectType)
        {
            return ActiveSkills
                .SelectMany(skill => skill.Effects)
                .Where(skillEffect => skillEffect.Type == effectType);
        }
    }
}
