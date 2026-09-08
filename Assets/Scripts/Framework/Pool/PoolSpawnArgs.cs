using UnityEngine;

namespace Framework.Pool
{
    public readonly struct PoolSpawnArgs
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Transform Parent { get; }

        public PoolSpawnArgs(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            Position = position;
            Rotation = rotation;
            Parent = parent;
        }
    }
}
