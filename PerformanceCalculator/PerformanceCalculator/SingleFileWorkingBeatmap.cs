﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;

namespace PerformanceCalculator
{
    /// <summary>
    /// A <see cref="WorkingBeatmap"/> which reads from a .osu file.
    /// </summary>
    public class SingleFileWorkingBeatmap : WorkingBeatmap
    {
        private readonly Beatmap beatmap;

        /// <summary>
        /// Constructs a new <see cref="SingleFileWorkingBeatmap"/> from a .osu file.
        /// </summary>
        /// <param name="file">The .osu file.</param>
        public SingleFileWorkingBeatmap(string file)
            : this(File.OpenRead(file))
        {
        }

        private SingleFileWorkingBeatmap(Stream stream)
            : this(new StreamReader(stream))
        {
            stream.Dispose();
        }

        private SingleFileWorkingBeatmap(StreamReader streamReader)
            : this(Decoder.GetDecoder<Beatmap>(streamReader).Decode(streamReader))
        {
        }

        private SingleFileWorkingBeatmap(Beatmap beatmap)
            : base(beatmap.BeatmapInfo)
        {
            this.beatmap = beatmap;
        }

        protected override IBeatmap GetBeatmap() => beatmap;
        protected override Texture GetBackground() => null;
        protected override Track GetTrack() => null;
    }
}
