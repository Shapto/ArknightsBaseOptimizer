using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Models
{
    /// <summary>
    /// The base facility a skill applies to. Names match the "room" values in the JSON (case-insensitive).
    /// </summary>
    public enum RoomType
    {
        Control,
        Trading,
        Manufacture,
        Power,
        Dormitory,
        Meeting,
        Hire,
        Training,
        Workshop,
        Recycle
    }
}
