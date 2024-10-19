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
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
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

    [JsonProperty("slider_tail_hit")]
    public int SliderTailHit { get; set; }
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

        public Ruleset GetRuleset(int gamemode)
        {
            switch (gamemode)
            {
                case 0: return new OsuRuleset();
                case 1: return new TaikoRuleset();
                case 2: return new CatchRuleset();
                case 3: return new ManiaRuleset();
                default: return new OsuRuleset();
            }
        }

        public override void Execute()
        {
            var sr = new StreamReader(Scores);
            var json = sr.ReadToEnd();
            var scoresData = JsonConvert.DeserializeObject<List<InputScore>>(json);

            if (scoresData.Count == 0)
                return;

            int gamemode = scoresData[0].Gamemode;
            Ruleset ruleset = GetRuleset(gamemode);

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

                var beatmapMaxCombo = GetMaxCombo(beatmap, gamemode);
                Dictionary<HitResult, int> statistics = new Dictionary<HitResult, int>
                {
                    { HitResult.Perfect, score.Statistics.Perfect },
                    { HitResult.Great, score.Statistics.Great },
                    { HitResult.Good, score.Statistics.Good },
                    { HitResult.Ok, score.Statistics.Ok },
                    { HitResult.Meh, score.Statistics.Meh },
                    { HitResult.Miss, score.Statistics.Miss },
                    { HitResult.LargeTickHit, score.Statistics.LargeTickHit },
                    { HitResult.SliderTailHit, score.Statistics.SliderTailHit }
                };

                ScoreInfo scoreInfo = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Accuracy = GetAccuracy(gamemode, score.Statistics),
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

        public int GetMaxCombo(IBeatmap beatmap, int gamemode)
        {
            switch (gamemode)
            {
                case 0: return beatmap.GetMaxCombo();
                case 1: return beatmap.HitObjects.OfType<Hit>().Count();
                case 2: return beatmap.HitObjects.Count(h => h is Fruit) + beatmap.HitObjects.OfType<JuiceStream>().SelectMany(j => j.NestedHitObjects).Count(h => !(h is TinyDroplet));
                case 3: return 0;
                default: return beatmap.GetMaxCombo();
            }
        }

        public double GetAccuracy(int gamemode, InputStatistics statistics)
        {
            if (gamemode == 2 || gamemode == 3) return 0;

            int totalHits = statistics.Perfect + statistics.Great + statistics.Good + statistics.Ok + statistics.Meh + statistics.Miss;
            switch (gamemode)
            {
                case 0: return (double)((6 * statistics.Great) + (2 * statistics.Ok) + statistics.Meh) / (6 * totalHits);
                case 1: return (double)((2 * statistics.Great) + statistics.Ok) / (2 * totalHits);
                default: return 0;
            }
        }
    }
}