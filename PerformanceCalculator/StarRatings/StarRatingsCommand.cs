using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

public class InputBeatmap
{
    [JsonProperty("beatmap_id")]
    public int BeatmapID { get; set; }

    [JsonProperty("hash")]
    public string Hash { get; set; }
}

namespace PerformanceCalculator.StarRatings
{
    [Command(Name = "starratings", Description = "Computes all possible star ratings of a beatmap")]
    public class StarRatingsCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Argument(0, Name = "beatmaps", Description = "Required. The beatmaps to calculate (.json)")]
        public string Beatmaps { get; }

        public override void Execute()
        {
            var sr = new StreamReader(Beatmaps);
            var json = sr.ReadToEnd();
            var beatmapsData = JsonConvert.DeserializeObject<List<InputBeatmap>>(json);

            if (beatmapsData.Count == 0)
                return;

            var resultSet = new ResultSet();
            beatmapsData.ForEach(delegate (InputBeatmap inputBeatmap)
            {
                try
                {
                    var beatmap = ProcessorWorkingBeatmap.FromFileOrId(inputBeatmap.BeatmapID.ToString(), inputBeatmap.Hash);
                    var results = processBeatmap(beatmap);
                    resultSet.Results.Add(results);
                }
                catch (Exception e)
                {

                }
                finally {
                    // File.Delete(Path.Combine("cache", $"{inputBeatmap.BeatmapID}.osu"));
                }
            });

            string output = JsonConvert.SerializeObject(resultSet);
            Console.WriteLine(output);
        }

        private Result processBeatmap(WorkingBeatmap beatmap)
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
            List<ResultModsStarRating> starRatingsResults = new List<ResultModsStarRating>();

            foreach (var modsInput in modsCombinations)
            {
                var mods = getMods(ruleset, modsInput);

                var task = Task.Run(() => {
                    return ruleset.CreateDifficultyCalculator(beatmap).Calculate(mods);
                });

                if (!task.Wait(TimeSpan.FromSeconds(15)))
                    throw new Exception("Timed out");
                
                var attributes = task.Result;

                var starRatingResult = new ResultModsStarRating
                {
                    Mods = modsInput.Length == 0 ? "NM" : string.Join("", modsInput),
                    StarRating = attributes.StarRating
                };

                starRatingsResults.Add(starRatingResult);
            }

            var result = new Result
            {
                RulesetId = ruleset.RulesetInfo.OnlineID,
                BeatmapId = beatmap.BeatmapInfo.OnlineID,
                StarRatings = starRatingsResults
            };

            return result;
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

            [JsonProperty("results")]
            public List<ResultModsStarRating> StarRatings { get; set; }
        }

        private class ResultModsStarRating
        {
            [JsonProperty("mods")]
            public string Mods { get; set; }

            [JsonProperty("star_rating")]
            public double StarRating { get; set; }
        }
    }
}