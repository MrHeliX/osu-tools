﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
            // string[] args = { "simulate", "osu", ".\\cache\\812010.osu", "-m", "HD", "-m", "NC", "-X", "0", "-M", "0", "-G", "16", "-c", "1434" };
            // string[] args = { "profile", "2330619", "26755add1b76d9eb677383c87a3ca9c7294026f9" };
            // CommandLineApplication.Execute<Program>(args);
            // return 1;
            
            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
            
        }
    }
}
