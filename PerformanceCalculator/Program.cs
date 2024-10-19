// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text;
using McMaster.Extensions.CommandLineUtils;
using osu.Framework.Logging;
using osu.Game.Beatmaps.Formats;
using osu.Game.Online;
using PerformanceCalculator.Difficulty;
using PerformanceCalculator.Leaderboard;
using PerformanceCalculator.Performance;
using PerformanceCalculator.Profile;
using PerformanceCalculator.Simulate;

namespace PerformanceCalculator
{
    [Command("dotnet PerformanceCalculator.dll")]
    [Subcommand(typeof(DifficultyCommand))]
    [Subcommand(typeof(ModsCommand))]
    [Subcommand(typeof(PerformanceListingCommand))]
    [Subcommand(typeof(ProfileCommand))]
    [Subcommand(typeof(SimulateListingCommand))]
    [Subcommand(typeof(MassSimulateCommand))]
    [Subcommand(typeof(LeaderboardCommand))]
    [Subcommand(typeof(LegacyScoreAttributesCommand))]
    [Subcommand(typeof(LegacyScoreConversionCommand))]
    [HelpOption("-?|-h|--help")]
    public class Program
    {
        public static readonly EndpointConfiguration ENDPOINT_CONFIGURATION = new ProductionEndpointConfiguration();

        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Logger.Enabled = false;

            LegacyDifficultyCalculatorBeatmapDecoder.Register();

            CommandLineApplication.Execute<Program>(args);
        }

        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a subcommand.");
            // string[] args = { "masssimulate", "/home/helix/Documents/huismetbenen/pp-recalculations/releases/test-release/masssimulate/13480282.json", "/home/helix/Documents/huismetbenen/pp-recalculations/releases/xexxar-release/cache" };
            // CommandLineApplication.Execute<Program>(args);
            // return 1;
            app.ShowHelp();
            return 1;
        }
    }
}
