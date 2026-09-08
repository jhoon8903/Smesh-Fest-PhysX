using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Pool
{
    public sealed class PoolContainer : MonoBehaviour
    {
        [SerializeField] private PoolConfig[] configs = Array.Empty<PoolConfig>();

        public IReadOnlyList<PoolConfig> Configs => configs;
    }
}
