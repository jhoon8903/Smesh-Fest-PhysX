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
        private enum Phase : byte { Idle, Waiting, Fading, Terminal }

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Material fadeMaterial;

        private ILoopEvents loopEvents;
        private GroundFadeConfig settings;
        private IPoolable owner;
        private PoolLease lease;
        private Color originalBaseColor;
        private MaterialPropertyBlock originalRendererPropertyBlock;
        private MaterialPropertyBlock originalIndexPropertyBlock;
        private MaterialPropertyBlock workingRendererPropertyBlock;
        private MaterialPropertyBlock workingIndexPropertyBlock;
        private bool fadeUsesIndexPropertyBlock;
        private Phase phase;
        private float elapsed;
        private bool subscribed;
        private bool initialized;

        public bool IsFading => phase == Phase.Waiting || phase == Phase.Fading;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(ILoopEvents injectedLoopEvents, GroundFadeConfig injectedSettings)
        {
            if (loopEvents != null || settings != null)
                throw new InvalidOperationException("GroundFadeReturn was already configured.");
            loopEvents = injectedLoopEvents ?? throw new ArgumentNullException(nameof(injectedLoopEvents));
            settings = injectedSettings ?? throw new ArgumentNullException(nameof(injectedSettings));
            settings.Validate();
        }

        public void OnPoolCreated(IPoolable poolOwner)
        {
            owner = poolOwner ?? throw new ArgumentNullException(nameof(poolOwner));
            if (targetRenderer == null || fadeMaterial == null)
                throw new InvalidOperationException("GroundFadeReturn requires an explicit Renderer and fade Material.");
            if (targetRenderer.transform != transform || targetRenderer.sharedMaterials.Length != 1)
                throw new InvalidOperationException("GroundFadeReturn requires exactly one Renderer material slot on its pooled root.");
            ValidateFadeMaterial();
            fadeMaterial.SetShaderPassEnabled("ShadowCaster", true);
            if (!fadeMaterial.GetShaderPassEnabled("ShadowCaster"))
                throw new InvalidOperationException("GroundFadeReturn could not enable the fade Material ShadowCaster pass.");
            if (targetRenderer.sharedMaterial != fadeMaterial)
                throw new InvalidOperationException("GroundFadeReturn requires the Renderer to already use its configured fade Material.");
            originalRendererPropertyBlock = new MaterialPropertyBlock();
            originalIndexPropertyBlock = new MaterialPropertyBlock();
            workingRendererPropertyBlock = new MaterialPropertyBlock();
            workingIndexPropertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(originalRendererPropertyBlock);
            targetRenderer.GetPropertyBlock(originalIndexPropertyBlock, 0);
            fadeUsesIndexPropertyBlock = !originalIndexPropertyBlock.isEmpty;
            originalBaseColor = fadeUsesIndexPropertyBlock && originalIndexPropertyBlock.HasColor("_BaseColor")
                ? originalIndexPropertyBlock.GetColor("_BaseColor")
                : !fadeUsesIndexPropertyBlock && originalRendererPropertyBlock.HasColor("_BaseColor")
                    ? originalRendererPropertyBlock.GetColor("_BaseColor")
                    : fadeMaterial.GetColor("_BaseColor");
            initialized = true;
        }

        public void OnPoolRent(PoolLease rentalLease)
        {
            if (!initialized || owner == null || !ReferenceEquals(owner.PoolObject, gameObject))
                throw new InvalidOperationException("GroundFadeReturn must be created by its owning pool before rental.");
            if (!rentalLease.IsValid)
                throw new InvalidOperationException("GroundFadeReturn requires the current PoolLease.");

            Unsubscribe();
            RestoreVisual();
            lease = rentalLease;
            phase = Phase.Idle;
            elapsed = 0f;
        }

        public void OnPoolReturn() => ResetRental();
        public void OnPoolDestroy() => ResetRental();

        private void OnDestroy()
        {
            Unsubscribe();
            lease = default;
            elapsed = 0f;
            phase = Phase.Terminal;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (phase != Phase.Idle || !lease.IsValid || collision.collider == null
                || !collision.collider.TryGetComponent(out GroundSurface _))
                return;
            phase = Phase.Waiting;
            elapsed = 0f;
            Subscribe();
        }

        private void HandleUpdate(float deltaTime)
        {
            if (!IsFading || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                return;

            elapsed += deltaTime;
            if (phase == Phase.Waiting)
            {
                if (elapsed < settings.DelaySeconds)
                    return;
                phase = Phase.Fading;
                elapsed -= settings.DelaySeconds;
            }

            ApplyFade(Mathf.Clamp01(1f - elapsed / settings.FadeSeconds));
            if (elapsed >= settings.FadeSeconds)
                CompleteReturn();
        }

        private void ApplyFade(float alpha)
        {
            MaterialPropertyBlock workingBlock;
            if (fadeUsesIndexPropertyBlock)
            {
                workingIndexPropertyBlock.Clear();
                targetRenderer.SetPropertyBlock(originalIndexPropertyBlock, 0);
                targetRenderer.GetPropertyBlock(workingIndexPropertyBlock, 0);
                workingBlock = workingIndexPropertyBlock;
            }
            else
            {
                workingRendererPropertyBlock.Clear();
                targetRenderer.SetPropertyBlock(originalRendererPropertyBlock);
                targetRenderer.GetPropertyBlock(workingRendererPropertyBlock);
                workingBlock = workingRendererPropertyBlock;
            }
            Color fadeColor = originalBaseColor;
            fadeColor.a *= alpha;
            workingBlock.SetColor("_BaseColor", fadeColor);
            if (fadeUsesIndexPropertyBlock)
                targetRenderer.SetPropertyBlock(workingBlock, 0);
            else
                targetRenderer.SetPropertyBlock(workingBlock);
        }

        private void CompleteReturn()
        {
            if (phase == Phase.Terminal)
                return;
            phase = Phase.Terminal;
            Unsubscribe();
            PoolLease currentLease = lease;
            currentLease.Return();
        }

        private void ResetRental()
        {
            Unsubscribe();
            RestoreVisual();
            lease = default;
            phase = Phase.Idle;
            elapsed = 0f;
        }

        private void RestoreVisual()
        {
            if (!initialized || targetRenderer == null)
                return;
            targetRenderer.SetPropertyBlock(originalRendererPropertyBlock.isEmpty ? null : originalRendererPropertyBlock);
            targetRenderer.SetPropertyBlock(originalIndexPropertyBlock.isEmpty ? null : originalIndexPropertyBlock, 0);
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            subscribed = false;
            if (loopEvents != null)
                loopEvents.UpdateTick -= HandleUpdate;
        }

        private void Subscribe()
        {
            if (subscribed)
                return;
            loopEvents.UpdateTick += HandleUpdate;
            subscribed = true;
        }

        private void ValidateFadeMaterial()
        {
            if (fadeMaterial.shader == null || !fadeMaterial.HasProperty("_BaseColor")
                || !fadeMaterial.HasProperty("_Surface") || !fadeMaterial.HasProperty("_ZWrite")
                || !fadeMaterial.HasProperty("_BlendModePreserveSpecular")
                || !fadeMaterial.HasProperty("_SrcBlend") || !fadeMaterial.HasProperty("_DstBlend")
                || !Mathf.Approximately(fadeMaterial.GetFloat("_Surface"), 1f)
                || !Mathf.Approximately(fadeMaterial.GetFloat("_ZWrite"), 0f)
                || !Mathf.Approximately(fadeMaterial.GetFloat("_BlendModePreserveSpecular"), 0f)
                || !Mathf.Approximately(fadeMaterial.GetFloat("_SrcBlend"), 5f)
                || !Mathf.Approximately(fadeMaterial.GetFloat("_DstBlend"), 10f)
                || fadeMaterial.renderQueue < (int)RenderQueue.Transparent
                || !fadeMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
                || fadeMaterial.GetTag("RenderType", false, string.Empty) != "Transparent")
                throw new InvalidOperationException("GroundFadeReturn requires an explicit URP/Lit transparent fade Material.");
        }
    }
}
