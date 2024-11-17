using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;

namespace PerformanceCalculator.StarRatings
{
    [Command(Name = "starratings", Description = "Computes all possible star ratings of a beatmap")]
    public class StarRatingsCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Argument(0, Name = "path", Description = "Required. A beatmap file (.osu), beatmap ID, or a folder containing .osu files to compute the difficulty for.")]
        public string Path { get; }

        public override void Execute()
        {
            var resultSet = new ResultSet();

            if (Directory.Exists(Path))
            {
                foreach (string file in Directory.GetFiles(Path, "*.osu", SearchOption.AllDirectories))
                {
                    try
                    {
                        var beatmap = new ProcessorWorkingBeatmap(file);
                        var results = processBeatmap(beatmap);
                        foreach (var r in results)
                            resultSet.Results.Add(r);
                    }
                    catch (Exception e)
                    {
                        resultSet.Errors.Add($"Processing beatmap \"{file}\" failed:\n{e.Message}");
                    }
                }
            }
            else
            {
                var results = processBeatmap(ProcessorWorkingBeatmap.FromFileOrId(Path));
                foreach (var r in results)
                    resultSet.Results.Add(r);
            }

            string json = JsonConvert.SerializeObject(resultSet);
            Console.WriteLine(json);
        }

        private List<Result> processBeatmap(WorkingBeatmap beatmap)
        {
            string[][] modsCombinations = [
                [],
                ["EZ"],
                ["HR"],
                ["HT"],
                ["DT"],
                ["FL"],
                ["EZ", "HT"],
                ["EZ", "DT"],
                ["EZ", "FL"],
                ["EZ", "HT", "FL"],
                ["EZ", "DT", "FL"],
                ["HR", "HT"],
                ["HR", "DT"],
                ["HR", "FL"],
                ["HR", "HT", "FL"],
                ["HR", "DT", "FL"],
                ["HT", "FL"],
                ["DT", "FL"]
            ];

            // Get the ruleset
            var ruleset = LegacyHelper.GetRulesetFromLegacyID(beatmap.BeatmapInfo.Ruleset.OnlineID);
            List<Result> results = new List<Result>();

            foreach (var modsInput in modsCombinations)
            {
                var mods = getMods(ruleset, modsInput);
                var attributes = ruleset.CreateDifficultyCalculator(beatmap).Calculate(mods);
                var result = new Result
                {
                    RulesetId = ruleset.RulesetInfo.OnlineID,
                    BeatmapId = beatmap.BeatmapInfo.OnlineID,
                    Beatmap = beatmap.BeatmapInfo.ToString(),
                    Mods = mods.Select(m => new APIMod(m)).ToList(),
                    Attributes = attributes
                };
                results.Add(result);
            }


            return results;
        }

        private Mod[] getMods(Ruleset ruleset, string[] modsInput)
        {
            var mods = new List<Mod>();
            var availableMods = ruleset.CreateAllMods().ToList();

            foreach (var modString in modsInput)
            {
                Mod newMod = availableMods.FirstOrDefault(m => string.Equals(m.Acronym, modString, StringComparison.CurrentCultureIgnoreCase));
                if (newMod != null)
                    mods.Add(newMod);
                else
                    throw new ArgumentException($"Invalid mod provided: {modString}");
            }

            return mods.ToArray();
        }

        private class ResultSet
        {
            [JsonProperty("errors")]
            public List<string> Errors { get; set; } = new List<string>();

            [JsonProperty("results")]
            public List<Result> Results { get; set; } = new List<Result>();
        }

        private class Result
        {
            [JsonProperty("ruleset_id")]
            public int RulesetId { get; set; }

            [JsonProperty("beatmap_id")]
            public int BeatmapId { get; set; }

            [JsonProperty("beatmap")]
            public string Beatmap { get; set; }

            [JsonProperty("mods")]
            public List<APIMod> Mods { get; set; }

            [JsonProperty("attributes")]
            public DifficultyAttributes Attributes { get; set; }
        }
    }
}