using System.Collections.Generic;

public sealed class Song
{
    public static Song instance;
    public double Position;
    public double Distance = 1600;
    public int PositionReads;
    public int DistanceReads;
    public readonly List<(FridayNightFunkin.FNFSong.FNFSection section, List<decimal> data)> Generated =
        new List<(FridayNightFunkin.FNFSong.FNFSection, List<decimal>)>();
    public double SongPosition { get { PositionReads++; return Position; } }
    public double FunkinRenderDistance { get { DistanceReads++; return Distance; } }
    public void GenNote(FridayNightFunkin.FNFSong.FNFSection section, List<decimal> data) => Generated.Add((section, data));
}

public static class Player
{
    public static float visualOffset;
}

namespace FridayNightFunkin
{
    public sealed class FNFSong
    {
        public sealed class FNFSection
        {
            public bool MustHitSection;
        }

        public sealed class FNFNote
        {
            private readonly List<decimal> data;
            public FNFNote(List<decimal> data) => this.data = data;
            public List<decimal> ConvertToNote() => data;
        }
    }
}
