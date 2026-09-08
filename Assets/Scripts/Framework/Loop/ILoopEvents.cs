using System;

namespace Framework.Loop
{
    /// <summary>
    /// Synchronous main-thread loop notifications that receive caller-supplied delta values.
    /// </summary>
    /// <remarks>
    /// Stopping a dispatcher preserves subscriptions and lets an active dispatch finish.
    /// Disposing a dispatcher performs final subscription cleanup.
    /// </remarks>
    public interface ILoopEvents
    {
        event Action<float> UpdateTick;
        event Action<float> FixedTick;
        event Action<float> LateTick;
    }
}
