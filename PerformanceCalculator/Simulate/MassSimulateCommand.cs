using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using Alba.CsConsoleFormat;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
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

        public string[] Mods { get; set; }

        public override void Execute()
        {
            var ruleset = new OsuRuleset();

            var scoreLines = File.ReadAllLines(Scores);

            var document = new Document();

            foreach (var line in scoreLines)
            {
                var splitLine = line.Split('\\');
                var map_id = splitLine[0];
                Mods = splitLine[1] == "" ? null : splitLine[1].Split('.');
                var mods = getMods(ruleset).ToArray();
                var combo = int.Parse(splitLine[2]);
                var count_good = int.Parse(splitLine[3]);
                var count_meh = int.Parse(splitLine[4]);
                var count_miss = int.Parse(splitLine[5]);

                var workingBeatmap = new ProcessorWorkingBeatmap("./cache/" + map_id + ".osu");
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
                document.Children.Add(new Span(workingBeatmap.BeatmapInfo.ToString()), "\n");

                document.Children.Add(new Span(getPlayInfo(scoreInfo, beatmap)), "\n");

                document.Children.Add(new Span(GetAttribute("Mods", mods.Length > 0
                    ? mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}")
                    : "None")), "\n");

                foreach (var kvp in categoryAttribs)
                    document.Children.Add(new Span(GetAttribute(kvp.Key, kvp.Value.ToString(CultureInfo.InvariantCulture))), "\n");

                document.Children.Add(new Span(GetAttribute("pp", pp.ToString(CultureInfo.InvariantCulture))), "\n");
                
            }

            OutputDocument(document);
        }

        private List<Mod> getMods(Ruleset ruleset)
        {
            var mods = new List<Mod>();
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
    }
}
