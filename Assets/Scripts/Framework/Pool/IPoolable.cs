using UnityEngine;

namespace Framework.Pool
{
    /// <summary>A view contract that allows any MonoBehaviour hierarchy to participate in pooling.</summary>
    public interface IPoolable : IPoolLifecycle
    {
        GameObject PoolObject { get; }
    }
}
