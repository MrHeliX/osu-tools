using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

public class Result
{
    [JsonProperty("score")]
    public ScoreStatistics Score { get; set; }

    [JsonProperty("performance_attributes")]
    public PerformanceAttributes PerformanceAttributes { get; set; }

    [JsonProperty("difficulty_attributes")]
    public DifficultyAttributes DifficultyAttributes { get; set; }
}

public class ScoreStatistics
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
    public long TotalScore { get; set; }

    [JsonProperty("legacy_total_score")]
    public long LegacyTotalScore { get; set; }

    [JsonProperty("accuracy")]
    public double Accuracy { get; set; }

    [JsonProperty("combo")]
    public int Combo { get; set; }

    [JsonProperty("statistics")]
    public Dictionary<HitResult, int> Statistics { get; set; }
}

public class InputMaximumStatistics
{
    [JsonProperty("slider_tail_hit")]
    public int SliderTailHit { get; set; }

    [JsonProperty("large_tick_hit")]
    public int LargeTickHit { get; set; }

    [JsonProperty("small_tick_hit")]
    public int SmallTickHit { get; set; }
}

public class InputStatistics
{
    [JsonProperty("perfect")]
    public int Perfect { get; set; }

    [JsonProperty("great")]
    public int Great { get; set; }

    [JsonProperty("good")]
    public int Good { get; set; }

    [JsonProperty("ok")]
    public int Ok { get; set; }

    [JsonProperty("meh")]
    public int Meh { get; set; }

    [JsonProperty("miss")]
    public int Miss { get; set; }

    [JsonProperty("large_tick_hit")]
    public int LargeTickHit { get; set; }

    [JsonProperty("large_tick_miss")]
    public int LargeTickMiss { get; set; }

    [JsonProperty("slider_tail_hit")]
    public int SliderTailHit { get; set; }

    [JsonProperty("ignore_hit")]
    public int IgnoreHit { get; set; }

    [JsonProperty("ignore_miss")]
    public int IgnoreMiss { get; set; }

    [JsonProperty("large_bonus")]
    public int LargeBonus { get; set; }

    [JsonProperty("small_bonus")]
    public int SmallBonus { get; set; }

    [JsonProperty("small_tick_hit")]
    public int SmallTickHit { get; set; }
}

public class InputScore
{
    [JsonProperty("gamemode")]
    public int Gamemode { get; set; }

    [JsonProperty("beatmap_id")]
    public int BeatmapID { get; set; }

    [JsonProperty("mods")]
    public List<APIMod> Mods { get; set; }

    [JsonProperty("max_combo")]
    public int MaxCombo { get; set; }

    [JsonProperty("statistics")]
    public InputStatistics Statistics { get; set; }

    [JsonProperty("maximum_statistics")]
    public InputMaximumStatistics MaximumStatistics { get; set; }
}

namespace PerformanceCalculator.Simulate
{
    [Command(Name = "masssimulate", Description = "Calculate the performance (pp) of n simulated scores.")]
    public class MassSimulateCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Required, FileExists]
        [Argument(0, Name = "scores", Description = "Required. The scores to calculate (.json).")]
        public string Scores { get; }

        [UsedImplicitly, Required]
        [Argument(1, Name = "cache", Description = "Path to the cache folder.")]
        public string CachePath { get; }

        public string[] Mods { get; set; }

        public override void Execute()
        {
            var sr = new StreamReader(Scores);
            var json = sr.ReadToEnd();
            var scoresData = JsonConvert.DeserializeObject<List<InputScore>>(json);

            if (scoresData.Count == 0)
                return;

            int gamemode = scoresData[0].Gamemode;
            Ruleset ruleset = LegacyHelper.GetRulesetFromLegacyID(gamemode);

            var results = new List<string>();

            scoresData.ForEach(delegate (InputScore score)
            {
                string beatmapPath = $"{CachePath}/{score.BeatmapID}.osu";
                if (!File.Exists(beatmapPath))
                    new FileWebRequest(beatmapPath, $"https://osu.ppy.sh/osu/{score.BeatmapID}").Perform();

                var workingBeatmap = ProcessorWorkingBeatmap.FromFileOrId(beatmapPath);
                Mods = score.Mods.Select(mod => mod.Acronym).ToArray();
                var mods = GetMods(ruleset);
                var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

                Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>
                {
                    { HitResult.Perfect, score.Statistics.Perfect },
                    { HitResult.Great, score.Statistics.Great },
                    { HitResult.Good, score.Statistics.Good },
                    { HitResult.Ok, score.Statistics.Ok },
                    { HitResult.Meh, score.Statistics.Meh },
                    { HitResult.Miss, score.Statistics.Miss },
                    { HitResult.LargeTickHit, score.Statistics.LargeTickHit },
                    { HitResult.SliderTailHit, score.Statistics.SliderTailHit },
                    { HitResult.LargeTickMiss, score.Statistics.LargeTickMiss },
                    { HitResult.IgnoreHit, score.Statistics.IgnoreHit },
                    { HitResult.IgnoreMiss, score.Statistics.IgnoreMiss },
                    { HitResult.LargeBonus, score.Statistics.LargeBonus },
                    { HitResult.SmallBonus, score.Statistics.SmallBonus },
                    { HitResult.SmallTickHit, score.Statistics.SmallTickHit }
                };

                bool isLazerCalculation = !mods.Any(m => m.Acronym == "CL");

                ScoreInfo scoreInfo = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = GetAccuracy(gamemode, score.Statistics, score.MaximumStatistics, isLazerCalculation),
                    MaxCombo = score.MaxCombo,
                    Statistics = statistics,
                    Mods = mods,
                    TotalScore = 0
                };

                var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
                var difficultyAttributes = difficultyCalculator.Calculate(mods);
                var performanceCalculator = ruleset.CreatePerformanceCalculator();
                var performanceAttributes = performanceCalculator?.Calculate(scoreInfo, difficultyAttributes);

                var result = new Result
                {
                    Score = new ScoreStatistics
                    {
                        RulesetId = scoreInfo.RulesetID,
                        BeatmapId = scoreInfo.BeatmapInfo?.OnlineID ?? -1,
                        Beatmap = scoreInfo.BeatmapInfo?.ToString() ?? "Unknown beatmap",
                        Mods = scoreInfo.Mods.Select(m => new APIMod(m)).ToList(),
                        TotalScore = scoreInfo.TotalScore,
                        LegacyTotalScore = scoreInfo.LegacyTotalScore ?? 0,
                        Accuracy = scoreInfo.Accuracy * 100,
                        Combo = scoreInfo.MaxCombo,
                        Statistics = scoreInfo.Statistics
                    },
                    PerformanceAttributes = performanceAttributes,
                    DifficultyAttributes = difficultyAttributes
                };

                string json = JsonConvert.SerializeObject(result, Formatting.Indented);
                results.Add(json);
            });

            var output = $"{{ \"response\": [{string.Join(",", results)}] }}";
            Console.WriteLine(output);
        }

        protected Mod[] GetMods(Ruleset ruleset)
        {
            if (Mods == null)
                return Array.Empty<Mod>();

            var availableMods = ruleset.CreateAllMods().ToList();
            var mods = new List<Mod>();

            foreach (var modString in Mods)
            {
                Mod newMod = availableMods.FirstOrDefault(m => string.Equals(m.Acronym, modString, StringComparison.CurrentCultureIgnoreCase)) ?? throw new ArgumentException($"Invalid mod provided: {modString}");
                mods.Add(newMod);
            }

            return mods.ToArray();
        }

        public double GetAccuracy(int gamemode, InputStatistics statistics, InputMaximumStatistics maximumStatistics, bool isLazerCalculation = false)
        {
            if (gamemode == 2)
            {
                double hits = statistics.Great + statistics.LargeTickHit + statistics.SmallTickHit;
                double total = hits + statistics.Miss + (maximumStatistics.SmallTickHit - statistics.SmallTickHit);
                return hits / total;
            }

            int totalHits = statistics.Perfect + statistics.Great + statistics.Good + statistics.Ok + statistics.Meh + statistics.Miss;

            if (isLazerCalculation)
            {
                switch (gamemode)
                {
                    case 0: return (double)(6 * statistics.Great + 2 * statistics.Ok + statistics.Meh + 3 * statistics.SliderTailHit + 0.6 * statistics.LargeTickHit) / (6 * totalHits + 3 * maximumStatistics.SliderTailHit + 0.6 * maximumStatistics.LargeTickHit);
                    case 1: return (double)(2 * statistics.Great + statistics.Ok) / (2 * totalHits);
                    case 3: return (double)(320 * statistics.Perfect + 300 * statistics.Great + 200 * statistics.Good + 100 * statistics.Ok + 50 * statistics.Meh) / (320 * totalHits);
                    default: return 0;
                }                    
            }

            switch (gamemode)
            {
                case 0: return (double)((6 * statistics.Great) + (2 * statistics.Ok) + statistics.Meh) / (6 * totalHits);
                case 1: return (double)((2 * statistics.Great) + statistics.Ok) / (2 * totalHits);
                case 3: return (double)(300 * (statistics.Perfect + statistics.Great) + 200 * statistics.Good + 100 * statistics.Ok + 50 * statistics.Meh) / (300 * totalHits);
                default: return 0;
            }
        }
    }
}
