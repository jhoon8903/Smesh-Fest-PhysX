using System;
using UnityEngine;

namespace Framework.Pool
{
    [CreateAssetMenu(menuName = "Smesh Fest/Pool Config")]
    public sealed class PoolConfig : ScriptableObject
    {
        [SerializeField] private string poolRootName = "Pool";
        [SerializeField] private MonoBehaviour prefab;
        [SerializeField] private int minPool = 1;
        [SerializeField] private int maxPool = 8;
        [Tooltip("Inactive inventory cleanup delay in real seconds. Retains MinPool. Zero disables timed cleanup.")]
        [SerializeField] private float returnDelaySeconds = 300f;

        public string PoolRootName => poolRootName;
        public MonoBehaviour Prefab => prefab;
        public int MinPool => minPool;
        public int MaxPool => maxPool;
        public float ReturnDelaySeconds => returnDelaySeconds;
        
        public void Validate()
        {
            if (prefab == null) throw new InvalidOperationException("PoolConfig requires a prefab component.");
            if (!(prefab is IPoolable poolable) || poolable.PoolObject != prefab.gameObject) throw new InvalidOperationException("PoolConfig prefab must implement IPoolable for its own GameObject.");
            if (string.IsNullOrWhiteSpace(poolRootName)) throw new InvalidOperationException("PoolConfig requires a non-empty pool root name.");
            if (minPool < 0) throw new InvalidOperationException("PoolConfig minimum pool size cannot be negative.");
            if (maxPool < 1) throw new InvalidOperationException("PoolConfig maximum pool size must be at least one.");
            if (minPool > maxPool) throw new InvalidOperationException("PoolConfig minimum pool size cannot exceed maximum pool size.");
            if (float.IsNaN(returnDelaySeconds) || float.IsInfinity(returnDelaySeconds) || returnDelaySeconds < 0f) throw new InvalidOperationException("PoolConfig return delay must be a finite non-negative value.");
        }
    }
}
