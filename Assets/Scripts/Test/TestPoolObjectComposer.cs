#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using Framework.Pool;

namespace Framework.Test
{
    /// <summary>Explicit test-only composition hook for pool tests that do not own MVC objects.</summary>
    public sealed class TestPoolObjectComposer : IPoolObjectComposer
    {
        private readonly Action<IPoolable> _compose;

        public TestPoolObjectComposer(Action<IPoolable> compose = null) => _compose = compose;

        public void Compose(IPoolable owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            _compose?.Invoke(owner);
        }
    }
}
#endif
