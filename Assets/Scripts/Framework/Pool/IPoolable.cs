using UnityEngine;

namespace Framework.Pool
{
    public interface IPoolable : IPoolLifecycle
    {
        GameObject PoolObject { get; }
    }
}
