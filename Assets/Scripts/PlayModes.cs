public static class PlayModes
{
    // These IDs are part of saved score keys. Keep Autoplay at 4 and retire ID 3.
    public const int Boyfriend = 1;
    public const int Opponent = 2;
    public const int Autoplay = 4;
    public const int Count = 3;

    public static int FromIndex(int index)
    {
        switch (index)
        {
            case 1: return Opponent;
            case 2: return Autoplay;
            default: return Boyfriend;
        }
    }

    public static int Normalize(int mode)
    {
        return mode == Opponent || mode == Autoplay ? mode : Boyfriend;
    }

    public static string Label(int mode)
    {
        switch (mode)
        {
            case Opponent: return "OPPONENT";
            case Autoplay: return "AUTOPLAY";
            default: return "BOYFRIEND";
        }
    }
}
