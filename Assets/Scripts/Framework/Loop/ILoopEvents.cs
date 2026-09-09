using System;

namespace Framework.Loop
{
    public interface ILoopEvents
    {
        event Action<float> UpdateTick;
        event Action<float> FixedTick;
        event Action<float> LateTick;
    }
}
