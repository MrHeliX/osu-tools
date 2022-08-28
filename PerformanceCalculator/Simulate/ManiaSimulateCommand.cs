// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;

namespace PerformanceCalculator.Simulate
{
    [Command(Name = "mania", Description = "Computes the performance (pp) of a simulated osu!mania play.")]
    public class ManiaSimulateCommand : SimulateCommand
    {
        public override int Score
        {
            get
            {
                Debug.Assert(score != null);
                return score.Value;
            }
        }

        [UsedImplicitly]
        [Option(Template = "-s|--score <score>", Description = "Score. An integer 0-1000000.")]
        private int? score { get; set; }

        [UsedImplicitly]
        [Option(Template = "-X|--misses <misses>", Description = "Number of misses. Defaults to 0.")]
        public override int Misses { get; }

        [UsedImplicitly]
        [Option(Template = "-M|--mehs <mehs>", Description = "Number of mehs. Will override accuracy if used. Otherwise is automatically calculated.")]
        public override int? Mehs { get; }

        [UsedImplicitly]
        [Option(Template = "-O|--oks <oks>", Description = "Number of oks. Will override accuracy if used. Otherwise is automatically calculated.")]
        public override int? Oks { get; }

        [UsedImplicitly]
        [Option(Template = "-G|--goods <goods>", Description = "Number of goods. Will override accuracy if used. Otherwise is automatically calculated.")]
        public override int? Goods { get; }

        [UsedImplicitly]
        [Option(Template = "-GR|--greats <greats>", Description = "Number of greats. Will override accuracy if used. Otherwise is automatically calculated.")]
        public override int? Greats { get; }

        [UsedImplicitly]
        [Option(CommandOptionType.MultipleValue, Template = "-m|--mod <mod>", Description = "One for each mod. The mods to compute the performance with."
                                                                                            + " Values: hr, dt, fl, 4k, 5k, etc...")]
        public override string[] Mods { get; }

        public override Ruleset Ruleset => new ManiaRuleset();

        public override void Execute()
        {
            if (score == null)
            {
                double scoreMultiplier = 1;

                // Cap score depending on difficulty adjustment mods (matters for mania).
                foreach (var mod in GetMods(Ruleset))
                {
                    if (mod.Type == ModType.DifficultyReduction)
                        scoreMultiplier *= mod.ScoreMultiplier;
                }

                score = (int)Math.Round(1000000 * scoreMultiplier);
            }

            base.Execute();
        }

        protected override int GetMaxCombo(IBeatmap beatmap) => 0;

        protected override Dictionary<HitResult, int> GenerateHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countMeh, int? countGood, int? countOk, int? countGreat)
        {
            var totalHits = beatmap.HitObjects.Count;

            // Only total number of hits is considered currently, so specifics don't matter
            return new Dictionary<HitResult, int>
            {
                { HitResult.Perfect, totalHits - (countGreat ?? 0) - (countGood ?? 0) - (countOk ?? 0) - (countMeh ?? 0) - countMiss },
                { HitResult.Great, countGreat ?? 0 },
                { HitResult.Ok, countOk ?? 0 },
                { HitResult.Good, countGood ?? 0 },
                { HitResult.Meh, countMeh ?? 0 },
                { HitResult.Miss, countMiss }
            };
        }

        protected override double GetAccuracy(Dictionary<HitResult, int> statistics)
        {
            var countPerfect = statistics[HitResult.Perfect];
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Good];
            var countOk = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];

            return (double)(300 * (countPerfect + countGreat) + 200 * countGood + 100 * countOk + 50 * countMeh) / (300 * (countPerfect + countGreat + countGood + countOk + countMeh + countMiss));
        }
    }
}
