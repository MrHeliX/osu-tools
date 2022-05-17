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
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

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
                var accuracy = getAccuracy(statistics, gamemode);

                var scoreInfo = new ScoreInfo(workingBeatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = accuracy,
                    MaxCombo = combo,
                    Statistics = statistics,
                    Mods = mods,
                    TotalScore = 0
                };

                scoreInfo.SetCount300(statistics[HitResult.Great]);
                scoreInfo.SetCount100(statistics[HitResult.Ok]);
                scoreInfo.SetCount50(statistics[HitResult.Meh]);
                scoreInfo.SetCountMiss(statistics[HitResult.Miss]);

                var score = new ProcessorScoreDecoder(workingBeatmap).Parse(scoreInfo);

                var categoryAttribs = new Dictionary<string, double>();
                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                var difficultyAttributes = difficultyCalculator.Calculate(LegacyHelper.ConvertToLegacyDifficultyAdjustmentMods(ruleset, scoreInfo.Mods).ToArray());

                var performanceCalculator = ruleset.CreatePerformanceCalculator();
                var ppAttributes = performanceCalculator?.Calculate(new ScoreInfo(workingBeatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = accuracy,
                    MaxCombo = combo,
                    Statistics = statistics,
                    Mods = mods,
                    TotalScore = 0,
                }, difficultyAttributes);

                var result = new Result
                {
                    Score = new ScoreStatistics
                    {
                        RulesetId = ruleset.RulesetInfo.OnlineID,
                        BeatmapId = workingBeatmap.BeatmapInfo.OnlineID,
                        Beatmap = workingBeatmap.BeatmapInfo.ToString(),
                        Mods = mods.Select(m => new APIMod(m)).ToList(),
                        Score = 0,
                        Accuracy = accuracy * 100,
                        Combo = combo,
                        Statistics = statistics
                    },
                    PerformanceAttributes = ppAttributes,
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
                    /*
                var ppAttributes = ruleset.CreatePerformanceCalculator(workingBeatmap, new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = accuracy,
                    MaxCombo = combo,
                    Statistics = statistics,
                    Mods = mods,
                    TotalScore = 0
                })?.Calculate();

                */

                    double pp = ppAttributes.Total;

                    /*
                    string filenameWithoutExtension = Scores.Split(".txt")[0];

                    List<string> newLines = new List<string>
                    {
                        "**********",
                        workingBeatmap.BeatmapInfo.ToString(),
                        getPlayInfo(scoreInfo, beatmap),
                        GetAttribute("Mods", mods.Length > 0
                        ? mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}")
                        : "None")
                    };

                    foreach (var kvp in categoryAttribs)
                        newLines.Add(GetAttribute(kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture)));

                    newLines.Add(GetAttribute("pp", pp.ToString(CultureInfo.InvariantCulture)));

                    File.AppendAllLines(filenameWithoutExtension + "-result.txt", newLines.ToArray());
                    */


                    document.Children.Add(new Span("**********"), "\n");

                    AddSectionHeader(document, "Basic score info");

                    document.Children.Add(
                        FormatDocumentLine("beatmap", $"{result.Score.BeatmapId} - {result.Score.Beatmap}"),
                        FormatDocumentLine("score", result.Score.Score.ToString(CultureInfo.InvariantCulture)),
                        FormatDocumentLine("accuracy", result.Score.Accuracy.ToString("N2", CultureInfo.InvariantCulture)),
                        FormatDocumentLine("combo", result.Score.Combo.ToString(CultureInfo.InvariantCulture)),
                        FormatDocumentLine("mods", result.Score.Mods.Count > 0 ? result.Score.Mods.Select(m => m.ToString()).Aggregate((c, n) => $"{c}, {n}") : "None")
                    );

                    AddSectionHeader(document, "Hit statistics");

                    foreach (var stat in result.Score.Statistics)
                        document.Children.Add(FormatDocumentLine(stat.Key.ToString().ToLowerInvariant(), stat.Value.ToString(CultureInfo.InvariantCulture)));

                    AddSectionHeader(document, "Performance attributes");

                    var ppAttributeValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(result.PerformanceAttributes)) ?? new Dictionary<string, object>();
                    foreach (var attrib in ppAttributeValues)
                        document.Children.Add(FormatDocumentLine(attrib.Key.Humanize().ToLower(), FormattableString.Invariant($"{attrib.Value:N2}")));

                    AddSectionHeader(document, "Difficulty attributes");

                    var diffAttributeValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(result.DifficultyAttributes)) ?? new Dictionary<string, object>();
                    foreach (var attrib in diffAttributeValues)
                        document.Children.Add(FormatDocumentLine(attrib.Key.Humanize(), FormattableString.Invariant($"{attrib.Value:N2}")));

                    OutputDocument(document);

                    /*
                    document.Children.Add(new Span(workingBeatmap.BeatmapInfo.ToString()), "\n");

                    document.Children.Add(new Span(getPlayInfo(scoreInfo, beatmap)), "\n");

                    document.Children.Add(new Span(GetAttribute("Mods", mods.Length > 0
                        ? mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}")
                        : "None")), "\n");

                    foreach (var stat in result.Score.Statistics)
                        document.Children.Add(FormatDocumentLine(stat.Key.ToString().ToLowerInvariant(), stat.Value.ToString(CultureInfo.InvariantCulture)));

                    foreach (var kvp in categoryAttribs)
                        document.Children.Add(new Span(GetAttribute(kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture))), "\n");

                    var ppAttributeValues = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(ppAttributes)) ?? new Dictionary<string, object>();
                    foreach (var ppa in ppAttributeValues)
                        document.Children.Add(new Span(GetAttribute(ppa.Key, ppa.Value?.ToString())), "\n");

                    document.Children.Add(new Span(GetAttribute("pp", pp.ToString(CultureInfo.InvariantCulture))), "\n");
                    */
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

        protected void AddSectionHeader(Document document, string header)
        {
            if (document.Children.Any())
                document.Children.Add(Environment.NewLine);

            document.Children.Add(header);
            document.Children.Add(new Separator());
        }

        protected string FormatDocumentLine(string name, string value) => $"{name.PadRight(20)}: {value}\n";

        private class Result
        {
            [JsonProperty("score")]
            public ScoreStatistics Score { get; set; }

            [JsonProperty("performance_attributes")]
            public PerformanceAttributes PerformanceAttributes { get; set; }

            [JsonProperty("difficulty_attributes")]
            public DifficultyAttributes DifficultyAttributes { get; set; }
        }

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

        private double getAccuracy(Dictionary<HitResult, int> statistics, int gamemode)
        {
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];
            var total = countGreat + countGood + countMeh + countMiss;

            if (gamemode == 0)
                return (double)((6 * countGreat) + (2 * countGood) + countMeh) / (6 * total);
            else if (gamemode == 1)
                return (double)((2 * countGreat) + countGood) / (2 * total);
            else return 0;
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
    }
}
