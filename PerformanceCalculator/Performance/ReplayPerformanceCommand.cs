// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace PerformanceCalculator.Performance
{
    [Command(Name = "replay", Description = "Computes the performance (pp) of a replay.")]
    public class ReplayPerformanceCommand : ApiCommand
    {
        [UsedImplicitly]
        [FileExists]
        [Argument(0, "replay", "The replay file to process.")]
        public string Replay { get; }

        public override void Execute()
        {
            var scoreDecoder = new ProcessorScoreDecoder(lookupBeatmap);

            Score score;
            using (var stream = File.OpenRead(Replay))
                score = scoreDecoder.Parse(stream);

            // At this point the beatmap will have been cached locally due to the lookup during decode, so this is practically free.
            var workingBeatmap = ProcessorWorkingBeatmap.FromFileOrId(score.ScoreInfo.BeatmapInfo!.OnlineID.ToString());

            var ruleset = score.ScoreInfo.Ruleset.CreateInstance();
            var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);

            Mod[] mods = score.ScoreInfo.Mods;
            if (score.ScoreInfo.IsLegacyScore)
                mods = LegacyHelper.ConvertToLegacyDifficultyAdjustmentMods(ruleset, mods);

            var difficultyAttributes = difficultyCalculator.Calculate(mods);
            var performanceCalculator = score.ScoreInfo.Ruleset.CreateInstance().CreatePerformanceCalculator();
            var performanceAttributes = performanceCalculator?.Calculate(score.ScoreInfo, difficultyAttributes);

            OutputPerformance(score.ScoreInfo, performanceAttributes, difficultyAttributes);
        }

        private WorkingBeatmap lookupBeatmap(string md5Hash)
        {
            try
            {
                APIBeatmap apiBeatmap = GetJsonFromApi<APIBeatmap>($"beatmaps/lookup?checksum={md5Hash}");
                return ProcessorWorkingBeatmap.FromFileOrId(apiBeatmap.OnlineID.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"The beatmap could not be looked up: {ex.Message}");
                throw;
            }
        }

        private class ProcessorScoreDecoder : LegacyScoreDecoder
        {
            private readonly Func<string, WorkingBeatmap> lookupBeatmap;

            public ProcessorScoreDecoder(Func<string, WorkingBeatmap> lookupBeatmap)
            {
                this.lookupBeatmap = lookupBeatmap;
            }

            protected override Ruleset GetRuleset(int rulesetId) => LegacyHelper.GetRulesetFromLegacyID(rulesetId);

            protected override WorkingBeatmap GetBeatmap(string md5Hash) => lookupBeatmap(md5Hash);
        }
    }
}
