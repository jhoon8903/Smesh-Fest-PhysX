using System;
using UnityEngine;

namespace Framework.Object
{
    public abstract class ObView : MonoBehaviour
    {
        private bool _destroying;

        internal event Action Enabled;
        internal event Action Disabled;
        internal event Action Destroying;
        internal bool IsDestroying => _destroying;

        protected virtual void OnEnable()
        {
            if (!_destroying) Enabled?.Invoke();
        }

        protected virtual void OnDisable()
        {
            if (!_destroying) Disabled?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            NotifyDestroying();
        }

        protected void NotifyDestroying()
        {
            if (_destroying) return;
            _destroying = true;
            try
            {
                Destroying?.Invoke();
            }
            finally
            {
                Enabled = null;
                Disabled = null;
                Destroying = null;
            }
        }
    }
}
