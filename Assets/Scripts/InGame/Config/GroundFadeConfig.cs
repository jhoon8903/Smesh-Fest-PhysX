using System;
using UnityEngine;

namespace InGame.Config
{
    [CreateAssetMenu(menuName = "Smesh Fest/Ground Fade Config", fileName = "GroundFadeConfig")]
    public sealed class GroundFadeConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float delaySeconds = 1f;
        [SerializeField, Min(0.0001f)] private float fadeSeconds = 1f;

        public float DelaySeconds => delaySeconds;
        public float FadeSeconds => fadeSeconds;

        public void Validate()
        {
            if (!IsFinite(delaySeconds) || delaySeconds < 0f)
                throw new InvalidOperationException("Ground fade delay must be finite and non-negative.");
            if (!IsFinite(fadeSeconds) || fadeSeconds <= 0f)
                throw new InvalidOperationException("Ground fade duration must be finite and greater than zero.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
