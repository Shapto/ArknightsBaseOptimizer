using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ArknightsBaseOptimizer.Optimization.PlanModels;

namespace ArknightsBaseOptimizer.Optimization
{
    /// <summary>
    /// Places operators that other skills need "in the base" (Ines for Hoederer, Ulpianus for Underflow),
    /// preferring the side room their own skill belongs to, and removes them when nobody needs them.
    /// </summary>
    public static class PartnerPlacement
    {
        /// <summary>
        /// Side rooms in the order partners are tried, with their seat counts. Reception comes first, so operators with
        /// skills in several side rooms (like Ines: Reception and Office) go to the Reception Room.
        /// </summary>
        private static readonly (RoomType Room, int Seats)[] SupportRoomPriority =
        {
        (RoomType.Meeting, 2),
        (RoomType.Hire, 1),
        (RoomType.Workshop, 1),
        (RoomType.Training, 1)
    };

        /// <summary>
        /// Puts the partner in the highest-priority side room matching one of their own skills if a seat is free,
        /// otherwise in a free Power Plant. Returns false if there's nowhere to put them.
        /// </summary>
        public static bool Place(ShiftPlan shift, RosterOperator partner)
        {
            foreach ((RoomType room, int seats) in SupportRoomPriority)
            {
                bool hasSkillThere = partner.ActiveSkills.Any(skill => skill.Room == room);
                List<RosterOperator> occupants = shift.GetSupportRoom(room);
                if (hasSkillThere && occupants.Count < seats)
                {
                    occupants.Add(partner);
                    return true;
                }
            }

            if (shift.PowerPlants.Count < GameConstants.PowerPlantCount)
            {
                shift.PowerPlants.Add(partner);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes partners from Power Plants and side rooms when no working operator needs them any more.
        /// </summary>
        public static void RemoveUnneeded(ShiftPlan shift)
        {
            HashSet<string> requiredNames = GetRequiredNames(shift);
            shift.PowerPlants.RemoveAll(partner => !requiredNames.Contains(partner.Name));
            foreach (List<RosterOperator> occupants in shift.SupportRooms.Values)
            {
                occupants.RemoveAll(partner => !requiredNames.Contains(partner.Name));
            }
        }

        /// <summary>
        /// Names that working operators' skills need somewhere in the base.
        /// </summary>
        public static HashSet<string> GetRequiredNames(ShiftPlan shift)
        {
            return shift.AllWorkingOperators()
                .SelectMany(rosterOperator => rosterOperator.ActiveSkills)
                .SelectMany(skill => skill.Effects)
                .Select(skillEffect => skillEffect.Condition?.OperatorInBase)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
}
