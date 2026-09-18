public interface IVanillaCharacterStage
{
    void Hit(int side, int direction, double time);
    void Sing(int side, int direction, bool miss);
    void Hold(int side);
    void Combo(int count, bool dropped);
    void PlayAnimation(string target, string animation);
    void BeginDeath();
    void PlayDeath(string animation);
}
