namespace Framework.Loop
{
    /// <summary>
    /// Exposes ownership-based game pause state without reading or changing Unity time.
    /// </summary>
    public interface IGamePause
    {
        bool IsPaused { get; }

        bool Pause(object owner);
        bool Resume(object owner);
    }
}
