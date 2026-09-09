using System;
using System.Collections.Generic;
using Framework.Pool;
using UnityEngine;

namespace InGame.Level
{
    /// <summary>Immutable-at-runtime authoring data for one ordered obstacle layout.</summary>
    [CreateAssetMenu(menuName = "Smesh Fest/Level Config")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private PoolConfig poolConfig;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private Quaternion localRotation = Quaternion.identity;
            [SerializeField] private Vector3 localScale = Vector3.one;

            public PoolConfig PoolConfig => poolConfig;
            public Vector3 LocalPosition => localPosition;
            public Quaternion LocalRotation => localRotation;
            public Vector3 LocalScale => localScale;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int Count => entries != null ? entries.Length : 0;
        public Entry GetEntry(int index)
        {
            if (entries == null || index < 0 || index >= entries.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return entries[index];
        }

        /// <summary>Checks serialized content only. It never substitutes missing data.</summary>
        public void Validate()
        {
            if (entries == null || entries.Length == 0)
                throw new InvalidOperationException("LevelConfig requires at least one ordered entry.");

            var requiredByPool = new Dictionary<PoolConfig, int>();
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null)
                    throw new InvalidOperationException($"LevelConfig entry {i} is missing.");
                if (entry.PoolConfig == null)
                    throw new InvalidOperationException($"LevelConfig entry {i} requires a PoolConfig.");

                entry.PoolConfig.Validate();
                if (!IsFinite(entry.LocalPosition))
                    throw new InvalidOperationException($"LevelConfig entry {i} has a non-finite local position.");
                float rotationMagnitude = SqrMagnitude(entry.LocalRotation);
                if (!IsFinite(entry.LocalRotation) || rotationMagnitude < 0.999f
                    || rotationMagnitude > 1.001f)
                    throw new InvalidOperationException($"LevelConfig entry {i} requires a normalized finite local rotation.");
                if (!IsFinite(entry.LocalScale) || entry.LocalScale.x == 0f
                    || entry.LocalScale.y == 0f || entry.LocalScale.z == 0f)
                    throw new InvalidOperationException($"LevelConfig entry {i} requires finite, non-zero local scale components.");

                requiredByPool.TryGetValue(entry.PoolConfig, out int requiredCount);
                requiredByPool[entry.PoolConfig] = requiredCount + 1;
            }

            foreach (KeyValuePair<PoolConfig, int> required in requiredByPool)
            {
                if (required.Value > required.Key.MaxPool)
                    throw new InvalidOperationException(
                        $"LevelConfig requires {required.Value} rentals from '{required.Key.name}', but its PoolConfig maximum is {required.Key.MaxPool}.");
            }
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y)
                                                          && IsFinite(value.z) && IsFinite(value.w);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SqrMagnitude(Quaternion value) => value.x * value.x + value.y * value.y
                                                               + value.z * value.z + value.w * value.w;
    }
}
