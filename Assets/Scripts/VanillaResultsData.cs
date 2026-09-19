using System;

[Serializable]
public sealed class VanillaResultsData
{
    public string title;
    public string difficulty;
    public string characterId;
    public bool storyMode;
    public bool newHighscore;
    public bool rankImproved;
    public bool naughty = VanillaPreferences.Naughtyness;
    public int score;
    public int sick;
    public int good;
    public int bad;
    public int shit;
    public int missed;
    public int maxCombo;
    public int totalNotes;
    public int totalNotesHit;
    public double Clear => totalNotes <= 0 ? 0 : Math.Max(0, Math.Min(1, (sick + good - missed) / (double)totalNotes));
    public int ClearPercent => (int)Math.Floor(Clear * 100);
    public int Rank => totalNotes <= 0 ? 0 : sick == totalNotes ? 5 : Clear >= 1 ? 4 : Clear >= 0.9 ? 3 : Clear >= 0.8 ? 2 : Clear >= 0.6 ? 1 : 0;
    public string Character => characterId != null && characterId.StartsWith("pico", StringComparison.Ordinal) ? "pico" : "bf";
    public float CharacterDelay => (Rank == 3 ? 97 : 95) / 24f;
    public float MusicDelay => new[] { 2, 3, 5, 0, 95, 95 }[Rank] / 24f;
    public float FlashDelay => new[] { 186, 107, 109, 122, 129, 129 }[Rank] / 24f;
    public float HighscoreDelay => new[] { 207, 127, 129, 140, 140, 140 }[Rank] / 24f;

    public static VanillaResultsData Capture(PlayerStat stats, int total)
    {
        return new VanillaResultsData
        {
            score = stats.currentScore, sick = stats.totalSicks, good = stats.totalGoods,
            bad = stats.totalBads, shit = stats.totalShits,
            missed = stats.missedHits + Math.Max(0, total - stats.totalNoteHits),
            totalNotes = total, totalNotesHit = stats.hitNotes, maxCombo = stats.highestCombo
        };
    }

    public void Add(VanillaResultsData other)
    {
        score += other.score;
        sick += other.sick;
        good += other.good;
        bad += other.bad;
        shit += other.shit;
        missed += other.missed;
        totalNotes += other.totalNotes;
        totalNotesHit += other.totalNotesHit;
        maxCombo = Math.Max(maxCombo, other.maxCombo);
    }
}
