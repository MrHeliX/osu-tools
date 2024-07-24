using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using Alba.CsConsoleFormat;
using Humanizer;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Scoring;

namespace PerformanceCalculator.Simulate
{
    [Command(Name = "masssimulate", Description = "Calculate the performance (pp) of 500 simulated scores.")]
    public class MassSimulateCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Required, FileExists]
        [Argument(0, Name = "scores", Description = "Required. The scores to recalculate (.txt).")]
        public string Scores { get; }

        [UsedImplicitly]
        [Option(Template = "-j|--json", Description = "Output results as JSON.")]
        public bool OutputJson { get; }

        public int Mods { get; set; }

        public override void Execute()
        {
            var osuRuleset = new OsuRuleset();
            var taikoRuleset = new TaikoRuleset();

            var scoreLines = File.ReadAllLines(Scores);
            var gamemode = int.Parse(scoreLines[0].Split('\\')[0]);
            var ruleset = LegacyHelper.GetRulesetFromLegacyID(gamemode);

            var document = new Document();
            var lines = new List<string>();

            foreach (var line in scoreLines)
            {
                var splitLine = line.Split('\\');
                var map_id = splitLine[1];
                Mods = int.Parse(splitLine[2]);

                Mod[] mods = ruleset.ConvertFromLegacyMods((LegacyMods)Mods).ToArray();

                var combo = int.Parse(splitLine[3]);
                var count_good = int.Parse(splitLine[4]);
                var count_meh = int.Parse(splitLine[5]);
                var count_miss = int.Parse(splitLine[6]);

                var cachePath = "../xexxar-release/cache/" + map_id + ".osu";
                if (!File.Exists(cachePath))
                {
                    Console.WriteLine($"Downloading {map_id}.osu...");
                    new FileWebRequest(cachePath, $"https://osu.ppy.sh/osu/{map_id}").Perform();
                }

                var workingBeatmap = new ProcessorWorkingBeatmap(cachePath);
                var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

                var statistics = generateHitResults(beatmap, count_miss, count_meh, count_good);
                var accuracy = getAccuracy(statistics);

                var scoreInfo = new ScoreInfo
                {
                    Accuracy = accuracy,
                    MaxCombo = combo,
                    Statistics = statistics,
                    Mods = mods,
                    TotalScore = 0,
                    RulesetID = ruleset.RulesetInfo.ID ?? 0
                };

                var categoryAttribs = new Dictionary<string, double>();
                double pp = ruleset.CreatePerformanceCalculator(workingBeatmap, scoreInfo).Calculate(categoryAttribs);
                var difficultyAttributes = ruleset.CreateDifficultyCalculator(workingBeatmap).Calculate(LegacyHelper.TrimNonDifficultyAdjustmentMods(ruleset, mods).ToArray());

                var result = new Result
                {
                    Score = new ScoreStatistics
                    {
                        RulesetId = ruleset.RulesetInfo.OnlineID,
                        BeatmapId = workingBeatmap.BeatmapInfo.OnlineID ?? 0,
                        Beatmap = workingBeatmap.BeatmapInfo.ToString(),
                        Mods = mods.Select(m => new APIMod(m)).ToList(),
                        Score = 0,
                        Accuracy = accuracy * 100,
                        Combo = combo,
                        Statistics = statistics
                    },
                    Pp = pp,
                    PerformanceAttributes = categoryAttribs.ToDictionary(k => k.Key.ToLowerInvariant().Underscore(), k => k.Value),
                    DifficultyAttributes = difficultyAttributes
                };

                if (OutputJson)
                {
                    string json = JsonConvert.SerializeObject(result);
                    lines.Add(json);

                    if (OutputFile != null)
                        File.WriteAllText(OutputFile, json);
                }
                else
                {
                    document.Children.Add(new Span("**********"), "\n");
                    document.Children.Add(new Span(workingBeatmap.BeatmapInfo.ToString()), "\n");

                    document.Children.Add(new Span(getPlayInfo(scoreInfo, beatmap)), "\n");

                    document.Children.Add(new Span(GetAttribute("Mods", mods.Length > 0
                        ? mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}")
                        : "None")), "\n");

                    foreach (var kvp in categoryAttribs)
                        document.Children.Add(new Span(GetAttribute(kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture))), "\n");

                    document.Children.Add(new Span(GetAttribute("pp", pp.ToString(CultureInfo.InvariantCulture))), "\n");
                }                
            }

            if (OutputJson)
            {
                var output = $"{{ \"response\": [{string.Join(",", lines)}] }}";
                Console.WriteLine(output);
            }
            else
            {
                OutputDocument(document);
            }
        }

        private List<Mod> getMods(Ruleset ruleset)
        {
            var mods = new List<Mod>();
            return mods;
            /*
            if (Mods == null)
                return mods;

            var availableMods = ruleset.GetAllMods().ToList();

            foreach (var modString in Mods)
            {
                Mod newMod = availableMods.FirstOrDefault(m => string.Equals(m.Acronym, modString, StringComparison.CurrentCultureIgnoreCase));
                if (newMod == null)
                    throw new ArgumentException($"Invalid mod provided: {modString}");

                mods.Add(newMod);
            }

            return mods;
            */
        }

        private Dictionary<HitResult, int> generateHitResults(IBeatmap beatmap, int countMiss, int? countMeh, int? countGood)
        {
            var totalResultCount = beatmap.HitObjects.Count;
            int countGreat = totalResultCount - (countGood ?? 0) - (countMeh ?? 0) - countMiss;

            return new Dictionary<HitResult, int>
            {
                { HitResult.Great, countGreat },
                { HitResult.Ok, countGood ?? 0 },
                { HitResult.Meh, countMeh ?? 0 },
                { HitResult.Miss, countMiss }
            };
        }

        private double getAccuracy(Dictionary<HitResult, int> statistics)
        {
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];
            var total = countGreat + countGood + countMeh + countMiss;

            return (double)((6 * countGreat) + (2 * countGood) + countMeh) / (6 * total);
        }

        protected string GetAttribute(string name, string value) => $"{name.PadRight(15)}: {value}";

        protected int GetMaxCombo(IBeatmap beatmap) => beatmap.HitObjects.Count + beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

        private string getPlayInfo(ScoreInfo scoreInfo, IBeatmap beatmap)
        {
            var playInfo = new List<string>
            {
                GetAttribute("Accuracy", (scoreInfo.Accuracy * 100).ToString(CultureInfo.InvariantCulture) + "%"),
                GetAttribute("Combo", FormattableString.Invariant($"{scoreInfo.MaxCombo} ({Math.Round(100.0 * scoreInfo.MaxCombo / GetMaxCombo(beatmap), 2)}%)"))
            };

            foreach (var statistic in scoreInfo.Statistics)
            {
                playInfo.Add(GetAttribute(Enum.GetName(typeof(HitResult), statistic.Key), statistic.Value.ToString(CultureInfo.InvariantCulture)));
            }

            return string.Join("\n", playInfo);
        }

        private class Result
        {
            [JsonProperty("score")]
            public ScoreStatistics Score { get; set; }

            [JsonProperty("pp")]
            public double Pp { get; set; }

            [JsonProperty("performance_attributes")]
            public IDictionary<string, double> PerformanceAttributes { get; set; }

            [JsonProperty("difficulty_attributes")]
            public DifficultyAttributes DifficultyAttributes { get; set; }
        }

        /// <summary>
        /// A trimmed down score.
        /// </summary>
        private class ScoreStatistics
        {
            [JsonProperty("ruleset_id")]
            public int RulesetId { get; set; }

            [JsonProperty("beatmap_id")]
            public int BeatmapId { get; set; }

            [JsonProperty("beatmap")]
            public string Beatmap { get; set; }

            [JsonProperty("mods")]
            public List<APIMod> Mods { get; set; }

            [JsonProperty("total_score")]
            public long Score { get; set; }

            [JsonProperty("accuracy")]
            public double Accuracy { get; set; }

            [JsonProperty("combo")]
            public int Combo { get; set; }

            [JsonProperty("statistics")]
            public Dictionary<HitResult, int> Statistics { get; set; }
        }
    }
}
