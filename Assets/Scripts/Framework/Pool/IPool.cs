using System;

namespace Framework.Pool
{
    public interface IPool : IDisposable
    {
        int CountAll { get; }
        int CountActive { get; }
        int CountInactive { get; }

        bool TryRent(in PoolSpawnArgs args, out PoolLease lease);
        int TrimIdle(bool retainMinimum);
    }
}
