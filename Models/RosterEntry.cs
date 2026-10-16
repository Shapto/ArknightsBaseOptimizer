using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// One operator you own, exactly as written in roster.json.
    /// </summary>
    public class RosterEntry
    {
        public string Name { get; init; } = "";
        public int Rarity { get; init; }
        public int Elite { get; init; }
        public int Level { get; init; }
    }
}
