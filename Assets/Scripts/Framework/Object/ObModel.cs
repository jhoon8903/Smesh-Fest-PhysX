using System;
using Framework.Observer;

namespace Framework.Object
{
    /// <summary>
    /// Base class for a world-object state model. Derived models own their state and call
    /// <see cref="NotifyChanged"/> only after a meaningful state change. It has no reset or disposal policy.
    /// </summary>
    public class ObModel
    {
        private readonly Observable<ObModel> _changed = new();

        /// <summary>The number of change listeners currently observing this model.</summary>
        public int ObserverCount => _changed.Count;

        /// <summary>Adds a change listener. Duplicate listeners are ignored.</summary>
        public bool Subscribe(Action<ObModel> listener) => _changed.Subscribe(listener);

        /// <summary>Removes a previously added change listener.</summary>
        public bool Unsubscribe(Action<ObModel> listener) => _changed.Unsubscribe(listener);

        /// <summary>
        /// Publishes the current model to its listeners. This is intended for derived-model state mutations only.
        /// Publishing recursively is not supported by <see cref="Observable{T}"/>.
        /// </summary>
        protected void NotifyChanged() => _changed.Publish(this);
    }
}
