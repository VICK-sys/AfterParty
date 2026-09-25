using System.Collections.Generic;
using System.Linq;
using FridayNightFunkin;

public class NoteBehaviour
{
    public FNFSong.FNFSection section;
    public FNFSong.FNFNote noteData;
    private readonly List<decimal> note;
    public int count;
    public double StrumTime { get; }

    public NoteBehaviour(FNFSong.FNFSection section, FNFSong.FNFNote noteData)
    {
        this.section = section;
        this.noteData = noteData;
        note = noteData.ConvertToNote();
        StrumTime = (double)note[0];
    }

    public void GenerateNote()
    {
        if (count > 0) return;
        Song song = Song.instance;
        GenerateNote(song, song.SongPosition - Player.visualOffset, song.FunkinRenderDistance);
    }

    public void GenerateNote(Song song, double position, double renderDistance)
    {
        if (count > 0 || position < StrumTime - renderDistance) return;
        count++;
        if (position > StrumTime + FunkinRules.HitWindow) return;
        song.GenNote(section, note);
    }
}

public sealed class NoteSchedule
{
    private List<NoteBehaviour> notes;
    private int next;

    public void Reset(List<NoteBehaviour> entries)
    {
        NoteBehaviour[] sorted = entries.OrderBy(note => note.StrumTime).ToArray();
        entries.Clear();
        entries.AddRange(sorted);
        notes = entries;
        next = 0;
    }

    public void Advance(Song song)
    {
        if (notes == null || next >= notes.Count) return;
        double position = song.SongPosition - Player.visualOffset;
        double renderDistance = song.FunkinRenderDistance;
        while (next < notes.Count)
        {
            NoteBehaviour note = notes[next];
            if (note.count == 0 && position < note.StrumTime - renderDistance) break;
            note.GenerateNote(song, position, renderDistance);
            next++;
        }
    }
}
