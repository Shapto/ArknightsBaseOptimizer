using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// One base skill (buff) definition from the game data.
    /// </summary>
    public class BaseSkill
    {
        public string BuffId { get; init; } = "";
        public string Name { get; init; } = "";
        public RoomType Room { get; init; }
        public string Description { get; init; } = "";
        public string Language { get; init; } = "";
        public List<SkillEffect> Effects { get; init; } = new();
    }
}
