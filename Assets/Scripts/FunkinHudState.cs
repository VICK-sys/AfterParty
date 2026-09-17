using System;

public static class FunkinHudRules
{
    public const double BarWidth = 601;
    public const double BarHeight = 19;
    public const double FillWidth = 593;
    public const double FillHeight = 11;
    public const double BarX = (1280 - BarWidth) / 2;

    public static double SmoothHealth(double shown, double health, bool bot)
    {
        return bot ? 200 : shown + 0.15 * (Math.Max(0, Math.Min(200, health)) - shown);
    }

    public static int FillPixels(double health)
    {
        double fraction = Math.Max(0, Math.Min(1, health / 200));
        double interval = FillWidth / 100;
        return (int)Math.Floor((int)(fraction * FillWidth / interval) * interval + 0.5);
    }

    public static double BarY(bool downscroll) => downscroll ? 72 : 648;
    public static double Boundary(double health) => BarX + 4 + FillWidth * (1 - health / 200);
}

public sealed class FunkinHealthIconState
{
    public enum Face { Idle, Losing, Winning }
    public Face Animation { get; private set; }
    public double Width { get; private set; } = 150;
    private double from;
    private double time;
    private double duration;

    public void Reset()
    {
        Animation = Face.Idle;
        Width = 150;
        duration = time = 0;
    }

    public void UpdateFace(double health, bool hasWinning)
    {
        switch (Animation)
        {
            case Face.Idle:
                if (health < 40) Animation = Face.Losing;
                else if (health > 160 && hasWinning) Animation = Face.Winning;
                break;
            case Face.Losing:
                if (health > 40) Animation = Face.Idle;
                break;
            case Face.Winning:
                if (health < 160) Animation = Face.Idle;
                break;
        }
    }

    public void Bop(double stepMilliseconds)
    {
        from = Width + 30;
        Width = (int)from;
        time = 0;
        duration = Math.Min(stepMilliseconds * 0.002, 0.175);
    }

    public void Advance(double elapsed)
    {
        if (duration <= 0) return;
        time += elapsed;
        double progress = Math.Min(time / duration, 1);
        Width = (int)(from + (150 - from) * progress);
        if (progress >= 1) duration = 0;
    }
}

public sealed class FunkinPopupState
{
    public double X;
    public double Y;
    public double VelocityX;
    public double VelocityY;
    public double Gravity;
    public double FadeDelay;
    public double Age;
    public double Alpha => Math.Max(0, Math.Min(1, 1 - (Age - FadeDelay) / 0.2));
    public bool Finished => Age >= FadeDelay + 0.2;

    public void Advance(double elapsed)
    {
        X += VelocityX * elapsed;
        Y += VelocityY * elapsed + Gravity * elapsed * elapsed * 0.5;
        VelocityY += Gravity * elapsed;
        Age += elapsed;
    }
}
