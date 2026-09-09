namespace Framework.Loop
{
    public interface IGamePause
    {
        bool IsPaused { get; }
        bool Pause(object owner);
        bool Resume(object owner);
    }
}
