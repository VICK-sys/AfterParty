using System.Collections.Generic;
using FridayNightFunkin;

public class NoteBehaviour
{
    public FNFSong.FNFSection section;
    public FNFSong.FNFNote noteData;
    private readonly List<decimal> note;
    public int count;

    public NoteBehaviour(FNFSong.FNFSection section, FNFSong.FNFNote noteData)
    {
        this.section = section;
        this.noteData = noteData;
        note = noteData.ConvertToNote();
    }

    public void GenerateNote()
    {
        Song song = Song.instance;
        if (count > 0 || song.SongPosition - Player.visualOffset < (double)note[0] - song.FunkinRenderDistance) return;
        count++;
        if (song.SongPosition - Player.visualOffset > (double)note[0] + FunkinRules.HitWindow) return;
        song.GenNote(section, note);
    }
}
