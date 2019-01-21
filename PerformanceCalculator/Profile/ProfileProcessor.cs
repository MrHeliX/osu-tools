// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-tools/master/LICENCE

using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using Newtonsoft.Json;

namespace PerformanceCalculator.Profile
{
    public class ProfileProcessor : IProcessor
    {
        private readonly ProfileCommand command;
        public ProfileProcessor(ProfileCommand command)
        {
            this.command = command;
        }

        public void Execute()
        {
            //initializing pp-information-holding sorted list
            var sortedPP = new SortedDictionary<double, PPInfo>();
            //initialize the information from the top 100 plays, held in a dynamic
            dynamic playData;
            string baseUrl = "https://osu.ppy.sh/";
            //gets top 100 plays
            string userBestUrl = baseUrl + "api/get_user_best?k=" + command.Key + "&u=" + command.ProfileName + "&m=" + command.Ruleset + "&limit=100&type=username";

            var ruleset = getRuleset(command.Ruleset ?? 0);

            //get data for all 100 top plays
            using (var readStream = apiReader(userBestUrl))
            {
                var json = readStream.ReadToEnd();
                playData = JsonConvert.DeserializeObject<dynamic>(json);
            }

            for (int i = 0; i < 100; i++)
            {
                ProcessorWorkingBeatmap workingBeatmap;

                string beatmapID = playData[i].beatmap_id;
                string beatmapAccessUrl = baseUrl + "osu/" + beatmapID;

                if (command.CachePath != null)
                {
                    string cachePath = Path.Combine(command.CachePath, beatmapID + ".txt");

                    if (!File.Exists(cachePath))
                    {
                        using (var writeStream = new StreamWriter(cachePath, true))
                        {
                            using (var readStream = apiReader(beatmapAccessUrl))
                            {
                                var text = readStream.ReadToEnd();
                                writeStream.Write(text);
                            }
                        }
                    }

                    workingBeatmap = new ProcessorWorkingBeatmap(cachePath);
                }
                else
                {
                    using (var readStream = apiReader(beatmapAccessUrl))
                    {
                        workingBeatmap = new ProcessorWorkingBeatmap(readStream);
                    }
                }

                //Stats Calculation
                double countmiss = playData[i].countmiss;
                double count50 = playData[i].count50;
                double count100 = playData[i].count100;
                double count300 = playData[i].count300;
                double totalHits = countmiss + count50 + count100 + count300;
                double accuracy = 0;

                if (command.Ruleset == 0 || command.Ruleset == null)
                {
                    accuracy = (count50 + (2 * count100) + (6 * count300)) / (6 * totalHits);
                }
                else if (command.Ruleset == 1)
                {
                    accuracy = ((0.5 * count100) + count300) / totalHits;
                }

                var maxCombo = (int)playData[i].maxcombo;

                var statistics = new Dictionary<HitResult, int>
                {
                    {HitResult.Great, (int)count300},
                    {HitResult.Good, (int)count100},
                    {HitResult.Meh, (int)count50},
                    {HitResult.Miss, (int)countmiss}
                };

                IEnumerable<Mod> mods = ruleset.ConvertLegacyMods((LegacyMods)playData[i].enabled_mods);

                Mod[] finalMods = mods.ToArray();

                var scoreInfo = new ScoreInfo
                {
                    Accuracy = accuracy,
                    MaxCombo = maxCombo,
                    Mods = finalMods,
                    Statistics = statistics
                };

                workingBeatmap.Mods.Value = finalMods;

                double pp = ruleset.CreatePerformanceCalculator(workingBeatmap, scoreInfo).Calculate();
                var outputInfo = new PPInfo
                {
                    LivePP = (double)playData[i].pp,
                    BeatmapName = workingBeatmap.BeatmapInfo.ToString(),
                    ModsAbbreviated = finalMods.Length > 0
                    ? finalMods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}")
                    : "None"
                };
                sortedPP.Add(pp, outputInfo);
            }

            double livePPNet = 0;
            double ppNet = 0;
            int w = 0;
            foreach (KeyValuePair<double, PPInfo> kvp in sortedPP.Reverse())
            {
                ppNet += Math.Pow(0.95, w) * kvp.Key;
                livePPNet += Math.Pow(0.95, w) * kvp.Value.LivePP;

                writeAttribute(w + 1 + ".Beatmap", kvp.Value.BeatmapName);
                writeAttribute("Mods", kvp.Value.ModsAbbreviated);
                writeAttribute("old/new pp", kvp.Value.LivePP.ToString(CultureInfo.InvariantCulture) + " / " + kvp.Key.ToString(CultureInfo.InvariantCulture));
                w++;
            }

            if (command.Bonus)
            {
                //get user data (used for bonus pp calculation)
                var userUrl = baseUrl + "api/get_user?k=" + command.Key + "&u=" + command.ProfileName + "&m=" + command.Ruleset + "&type=username";
                dynamic userData;
                using (var readStream = apiReader(userUrl))
                {
                    var json = readStream.ReadToEnd();
                    userData = JsonConvert.DeserializeObject<dynamic>(json);
                }

                double bonusPP = 0;
                //inactive players have 0pp to take them out of the leaderboard
                if (userData[0].pp_raw == 0)
                    command.Console.WriteLine("The player has 0 pp or is inactive, so bonus pp cannot be calculated");
                //calculate bonus pp as difference of user pp and sum of other pps
                else
                {
                    bonusPP = userData[0].pp_raw - livePPNet;
                    livePPNet = userData[0].pp_raw;
                }
                //add on bonus pp
                ppNet += bonusPP;
            }
            writeAttribute("Top 100 Listed Above. Old/New Net PP", livePPNet.ToString(CultureInfo.InvariantCulture) + " / " + ppNet.ToString(CultureInfo.InvariantCulture));
        }

        private void writeAttribute(string name, string value) => command.Console.WriteLine($"{name.PadRight(15)}: {value}");

        private StreamReader apiReader(string url)
        {
            var readStream = new StreamReader(WebRequest.Create(url).GetResponse().GetResponseStream());
            return readStream;
        }

        private Ruleset getRuleset(int rulesetId)
        {
            switch (rulesetId)
            {
                default:
                    throw new ArgumentException("Invalid ruleset id provided.");
                case 0:
                    return new OsuRuleset();
                case 1:
                    return new TaikoRuleset();
                case 2:
                    return new CatchRuleset();
                case 3:
                    return new ManiaRuleset();
            }
        }
    }
}
