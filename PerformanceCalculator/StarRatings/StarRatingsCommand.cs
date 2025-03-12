using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Taiko;

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
                finally
                {
                    // File.Delete(Path.Combine("cache", $"{inputBeatmap.BeatmapID}.osu"));
                }
            });

            string output = JsonConvert.SerializeObject(resultSet);
            Console.WriteLine(output);
        }

        private Result processBeatmap(WorkingBeatmap beatmap)
        {
            // Get the ruleset
            var ruleset = LegacyHelper.GetRulesetFromLegacyID(beatmap.BeatmapInfo.Ruleset.OnlineID);
            List<ResultModsStarRating> starRatingsResults = new List<ResultModsStarRating>();

            string[][] modsCombinations = getModCombinations(ruleset);
            foreach (var modsInput in modsCombinations)
            {
                var mods = getMods(ruleset, modsInput);

                var task = Task.Run(() =>
                {
                    return ruleset.CreateDifficultyCalculator(beatmap).Calculate(mods);
                });

                if (!task.Wait(TimeSpan.FromSeconds(15)))
                    throw new Exception("Timed out");

                var attributes = task.GetResultSafely();

                if (!modsInput.Any(m => m == "HD"))
                {
                    var starRatingResult = new ResultModsStarRating
                    {
                        Mods = modsInput.Length == 0 ? "NM" : string.Join("", modsInput),
                        StarRating = attributes.StarRating
                    };

                    starRatingsResults.Add(starRatingResult);
                }


                if (ruleset is OsuRuleset && attributes is OsuDifficultyAttributes attributes1)
                {
                    var modsFlashlight = modsInput.Append("FL");
                    var starRatingsResultWithFlashlight = new ResultModsStarRating
                    {
                        Mods = string.Join("", modsFlashlight),
                        StarRating = attributes1.StarRatingWithFlashlight
                    };

                    starRatingsResults.Add(starRatingsResultWithFlashlight);
                }
            }

            var result = new Result
            {
                RulesetId = ruleset.RulesetInfo.OnlineID,
                BeatmapId = beatmap.BeatmapInfo.OnlineID,
                StarRatings = starRatingsResults
            };

            return result;
        }

        private string[][] getModCombinations(Ruleset ruleset)
        {
            switch (ruleset)
            {
                case OsuRuleset:
                    return [
                        [],
                        ["EZ"],
                        ["HR"],
                        ["HT"],
                        ["DT"],
                        ["EZ", "HT"],
                        ["EZ", "DT"],
                        ["HR", "HT"],
                        ["HR", "DT"],
                        ["HD"],
                        ["EZ", "HD"],
                        ["EZ", "HD", "HT"],
                        ["EZ", "HD", "DT"],
                        ["HD", "HR"],
                        ["HD", "HR", "HT"],
                        ["HD", "HR", "DT"],
                        ["HD", "DT"],
                        ["HD", "HT"]
                    ];
                case TaikoRuleset:
                    return [
                        [],
                        ["EZ"],
                        ["HT"],
                        ["HR"],
                        ["DT"],
                        ["EZ", "HT"],
                        ["EZ", "DT"],
                        ["HR", "HT"],
                        ["HR", "DT"],
                    ];
                case CatchRuleset:
                    return [
                        [],
                        ["EZ"],
                        ["HT"],
                        ["HR"],
                        ["DT"],
                        ["EZ", "HT"],
                        ["EZ", "DT"],
                        ["HR", "HT"],
                        ["HR", "DT"]
                    ];
                case ManiaRuleset:
                    return [
                        [],
                        ["HT"],
                        ["DT"]
                    ];
                default: return [];
            }
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