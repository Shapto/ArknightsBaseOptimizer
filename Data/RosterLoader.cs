using ArknightsBaseOptimizer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArknightsBaseOptimizer.Data
{
    /// <summary>
    /// Loads roster.json and base_skills.json and combines them into operators with their currently unlocked skills.
    /// </summary>
    public static class RosterLoader
    {
        private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Loads both data files and returns your roster with active skills resolved.
        /// With assumeFullyPromoted, skills are resolved as if everyone were at max promotion and level for their rarity.
        /// </summary>
        public static List<RosterOperator> LoadRoster(bool assumeFullyPromoted = false)
        {
            BaseSkillDatabase database = ReadJsonFile<BaseSkillDatabase>("base_skills.json");
            List<RosterEntry> rosterEntries = ReadJsonFile<List<RosterEntry>>("roster.json");

            Dictionary<string, OperatorSkillData> operatorsByName = database.Operators.ToDictionary(operatorData => operatorData.Name, StringComparer.OrdinalIgnoreCase);

            List<string> unknownNames = rosterEntries
                .Where(entry => !operatorsByName.ContainsKey(entry.Name))
                .Select(entry => entry.Name)
                .ToList();
            if (unknownNames.Count > 0)
            {
                throw new InvalidDataException($"roster.json has names not found in base_skills.json: {string.Join(", ", unknownNames)}");
            }

            List<RosterOperator> roster = new();
            foreach (RosterEntry entry in rosterEntries)
            {
                OperatorSkillData operatorData = operatorsByName[entry.Name];
                RosterEntry skillEntry = assumeFullyPromoted ? CreateFullyPromotedEntry(entry) : entry;
                List<BaseSkill> activeSkills = ResolveActiveSkills(skillEntry, operatorData, database.Skills);
                List<BaseSkill> currentSkills = assumeFullyPromoted ? ResolveActiveSkills(entry, operatorData, database.Skills) : activeSkills;

                roster.Add(new RosterOperator
                {
                    Entry = entry,
                    CharId = operatorData.CharId,
                    Factions = operatorData.Factions,
                    ActiveSkills = activeSkills,
                    PreviewElite = skillEntry.Elite,
                    PromotionChangesSkills = !activeSkills.Select(skill => skill.BuffId).SequenceEqual(currentSkills.Select(skill => skill.BuffId))
                });
            }
            return roster;
        }

        /// <summary>
        /// The same operator at the highest promotion their rarity allows (4–6★ E2, 3★ E1, 1–2★ E0) and max level.
        /// </summary>
        private static RosterEntry CreateFullyPromotedEntry(RosterEntry entry)
        {
            int maximumElite = entry.Rarity >= 4 ? 2 : entry.Rarity == 3 ? 1 : 0;
            return new RosterEntry { Name = entry.Name, Rarity = entry.Rarity, Elite = maximumElite, Level = 999 };
        }

        /// <summary>
        /// For each slot, picks the latest unlock the operator has reached; slots with nothing reached are skipped.
        /// </summary>
        private static List<BaseSkill> ResolveActiveSkills(RosterEntry entry, OperatorSkillData operatorData, Dictionary<string, BaseSkill> skillsById)
        {
            List<BaseSkill> activeSkills = new();
            foreach (SkillSlot slot in operatorData.Slots)
            {
                SkillUnlock? reachedUnlock = slot.Unlocks.LastOrDefault(unlock => IsReached(entry, unlock));
                if (reachedUnlock != null)
                {
                    activeSkills.Add(skillsById[reachedUnlock.BuffId]);
                }
            }
            return activeSkills;
        }

        /// <summary>
        /// True if the operator's promotion and level meet the unlock requirement (E2 Lv1 beats E1 Lv80).
        /// </summary>
        private static bool IsReached(RosterEntry entry, SkillUnlock unlock)
        {
            return entry.Elite > unlock.Elite || (entry.Elite == unlock.Elite && entry.Level >= unlock.Level);
        }

        /// <summary>
        /// Reads and deserializes a JSON file from the Data folder in the output directory.
        /// </summary>
        private static T ReadJsonFile<T>(string fileName)
        {
            string filePath = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
            string jsonText = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(jsonText, serializerOptions) ?? throw new InvalidDataException($"{fileName} is empty or invalid");
        }
    }
}
