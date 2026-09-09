using System;
using Framework.Loop;
using Framework.Pool;
using InGame.Config;
using InGame.Obstacle;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer;

namespace InGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GroundFadeReturn : MonoBehaviour, IPoolLifecycle
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        private static readonly int Surface = Shader.PropertyToID("_Surface");
        private static readonly int BlendModePreserveSpecular = Shader.PropertyToID("_BlendModePreserveSpecular");
        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

        private enum Phase : byte { Idle, Waiting, Fading, Terminal }

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material fadeMaterial;

        private ILoopEvents _loopEvents;
        private GroundFadeConfig _settings;
        private IPoolable _owner;
        private PoolLease _lease;
        private Color _originalBaseColor;
        private MaterialPropertyBlock _originalRendererPropertyBlock;
        private MaterialPropertyBlock _originalIndexPropertyBlock;
        private MaterialPropertyBlock _workingRendererPropertyBlock;
        private MaterialPropertyBlock _workingIndexPropertyBlock;
        private bool _fadeUsesIndexPropertyBlock;
        private Phase _phase;
        private float _elapsed;
        private bool _subscribed;
        private bool _initialized;

        public bool IsFading => _phase == Phase.Waiting || _phase == Phase.Fading;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(ILoopEvents injectedLoopEvents, GroundFadeConfig injectedSettings)
        {
            if (_loopEvents != null || _settings != null) throw new InvalidOperationException("GroundFadeReturn was already configured.");
            _loopEvents = injectedLoopEvents ?? throw new ArgumentNullException(nameof(injectedLoopEvents));
            _settings = injectedSettings ?? throw new ArgumentNullException(nameof(injectedSettings));
            _settings.Validate();
        }

        public void OnPoolCreated(IPoolable poolOwner)
        {
            _owner = poolOwner ?? throw new ArgumentNullException(nameof(poolOwner));
            if (targetRenderer == null || fadeMaterial == null) throw new InvalidOperationException("GroundFadeReturn requires an explicit Renderer and fade Material.");
            if (targetRenderer.transform != transform || targetRenderer.sharedMaterials.Length != 1) throw new InvalidOperationException("GroundFadeReturn requires exactly one Renderer material slot on its pooled root.");
            ValidateFadeMaterial();
            fadeMaterial.SetShaderPassEnabled("ShadowCaster", true);
            if (!fadeMaterial.GetShaderPassEnabled("ShadowCaster")) throw new InvalidOperationException("GroundFadeReturn could not enable the fade Material ShadowCaster pass.");
            if (targetRenderer.sharedMaterial != fadeMaterial) throw new InvalidOperationException("GroundFadeReturn requires the Renderer to already use its configured fade Material.");
            _originalRendererPropertyBlock = new MaterialPropertyBlock();
            _originalIndexPropertyBlock = new MaterialPropertyBlock();
            _workingRendererPropertyBlock = new MaterialPropertyBlock();
            _workingIndexPropertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_originalRendererPropertyBlock);
            targetRenderer.GetPropertyBlock(_originalIndexPropertyBlock, 0);
            _fadeUsesIndexPropertyBlock = !_originalIndexPropertyBlock.isEmpty;
            _originalBaseColor = _fadeUsesIndexPropertyBlock && _originalIndexPropertyBlock.HasColor(BaseColor)
                ? _originalIndexPropertyBlock.GetColor(BaseColor)
                : !_fadeUsesIndexPropertyBlock && _originalRendererPropertyBlock.HasColor(BaseColor)
                    ? _originalRendererPropertyBlock.GetColor(BaseColor)
                    : fadeMaterial.GetColor(BaseColor);
            _initialized = true;
        }

        public void OnPoolRent(PoolLease rentalLease)
        {
            if (!_initialized || _owner == null || !ReferenceEquals(_owner.PoolObject, gameObject)) throw new InvalidOperationException("GroundFadeReturn must be created by its owning pool before rental.");
            if (!rentalLease.IsValid) throw new InvalidOperationException("GroundFadeReturn requires the current PoolLease.");

            Unsubscribe();
            RestoreVisual();
            _lease = rentalLease;
            _phase = Phase.Idle;
            _elapsed = 0f;
        }

        public void OnPoolReturn() => ResetRental();
        public void OnPoolDestroy() => ResetRental();

        private void OnDestroy()
        {
            Unsubscribe();
            _lease = default;
            _elapsed = 0f;
            _phase = Phase.Terminal;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_phase != Phase.Idle || !_lease.IsValid || collision.collider == null
                || !collision.collider.TryGetComponent(out GroundSurface _))
                return;
            _phase = Phase.Waiting;
            _elapsed = 0f;
            Subscribe();
        }

        private void HandleUpdate(float deltaTime)
        {
            if (!IsFading || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;

            _elapsed += deltaTime;
            if (_phase == Phase.Waiting)
            {
                if (_elapsed < _settings.DelaySeconds) return;
                _phase = Phase.Fading;
                _elapsed -= _settings.DelaySeconds;
            }

            ApplyFade(Mathf.Clamp01(1f - _elapsed / _settings.FadeSeconds));
            if (_elapsed >= _settings.FadeSeconds) CompleteReturn();
        }

        private void ApplyFade(float alpha)
        {
            MaterialPropertyBlock workingBlock;
            if (_fadeUsesIndexPropertyBlock)
            {
                _workingIndexPropertyBlock.Clear();
                targetRenderer.SetPropertyBlock(_originalIndexPropertyBlock, 0);
                targetRenderer.GetPropertyBlock(_workingIndexPropertyBlock, 0);
                workingBlock = _workingIndexPropertyBlock;
            }
            else
            {
                _workingRendererPropertyBlock.Clear();
                targetRenderer.SetPropertyBlock(_originalRendererPropertyBlock);
                targetRenderer.GetPropertyBlock(_workingRendererPropertyBlock);
                workingBlock = _workingRendererPropertyBlock;
            }
            Color fadeColor = _originalBaseColor;
            fadeColor.a *= alpha;
            workingBlock.SetColor(BaseColor, fadeColor);
            if (_fadeUsesIndexPropertyBlock) targetRenderer.SetPropertyBlock(workingBlock, 0);
            else targetRenderer.SetPropertyBlock(workingBlock);
        }

        private void CompleteReturn()
        {
            if (_phase == Phase.Terminal) return;
            _phase = Phase.Terminal;
            Unsubscribe();
            PoolLease currentLease = _lease;
            currentLease.Return();
        }

        private void ResetRental()
        {
            Unsubscribe();
            RestoreVisual();
            _lease = default;
            _phase = Phase.Idle;
            _elapsed = 0f;
        }

        private void RestoreVisual()
        {
            if (!_initialized || targetRenderer == null) return;
            targetRenderer.SetPropertyBlock(_originalRendererPropertyBlock.isEmpty ? null : _originalRendererPropertyBlock);
            targetRenderer.SetPropertyBlock(_originalIndexPropertyBlock.isEmpty ? null : _originalIndexPropertyBlock, 0);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (_loopEvents != null) _loopEvents.UpdateTick -= HandleUpdate;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _loopEvents.UpdateTick += HandleUpdate;
            _subscribed = true;
        }

        private void ValidateFadeMaterial()
        {
            if (fadeMaterial.shader == null || !fadeMaterial.HasProperty(BaseColor)
                || !fadeMaterial.HasProperty(Surface) || !fadeMaterial.HasProperty(ZWrite)
                || !fadeMaterial.HasProperty(BlendModePreserveSpecular)
                || !fadeMaterial.HasProperty(SrcBlend) || !fadeMaterial.HasProperty(DstBlend)
                || !Mathf.Approximately(fadeMaterial.GetFloat(Surface), 1f)
                || !Mathf.Approximately(fadeMaterial.GetFloat(ZWrite), 0f)
                || !Mathf.Approximately(fadeMaterial.GetFloat(BlendModePreserveSpecular), 0f)
                || !Mathf.Approximately(fadeMaterial.GetFloat(SrcBlend), 5f)
                || !Mathf.Approximately(fadeMaterial.GetFloat(DstBlend), 10f)
                || fadeMaterial.renderQueue < (int)RenderQueue.Transparent
                || !fadeMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
                || fadeMaterial.GetTag("RenderType", false, string.Empty) != "Transparent")
                throw new InvalidOperationException("GroundFadeReturn requires an explicit URP/Lit transparent fade Material.");
        }
    }
}
