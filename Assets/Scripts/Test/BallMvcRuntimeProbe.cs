#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Pool;
using InGame.Ball;
using InGame.Config;
using UnityEngine;
using VContainer;

namespace Framework.Test
{
    /// <summary>
    /// Isolated Play Mode verification for the Ball MVC bundle. It deliberately creates no scene object,
    /// prefab, physics, or input dependency.
    /// </summary>
    public sealed class BallMvcRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public string error;
            public string scope;
        }

        private readonly Result result = new Result
        {
            scope = "Isolated PoolFactory plus a temporary BallView source with required Rigidbody and SphereCollider; does not edit a saved Scene, Prefab, PoolConfig asset, or input, and does not simulate physics."
        };

        private GameObject fixtureRoot;
        private PoolFactory factory;
        private IObjectResolver resolver;
        private BallConfig ballSettings;
        private string outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig config,
            PoolConfig hierarchyFirstConfig, string output)
        {
            fixtureRoot = root;
            outputPath = output;
            StartCoroutine(Run(container, config, hierarchyFirstConfig));
        }

        private IEnumerator Run(PoolContainer container, PoolConfig config, PoolConfig hierarchyFirstConfig)
        {
            result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            result.unityVersion = Application.unityVersion;
            try
            {
                ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance(ballSettings);
                resolver = builder.Build();
                factory = new PoolFactory(container, resolver, true);
                factory.Initialize(false);

                IPool pool = factory.GetPool(config);
                Assert(pool.CountAll == 1 && pool.CountInactive == 1 && pool.CountActive == 0,
                    "Prewarm did not leave exactly one inactive BallView instance.");

                PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
                Assert(factory.TryRent(config, args, out PoolLease firstLease), "Initial Ball rent failed.");
                BallView view = firstLease.Value as BallView;
                Assert(view != null, "Pool did not return a BallView.");
                BallModel model = view.OwnedModel;
                BallController controller = view.Controller;
                Assert(model != null && controller != null, "BallView did not create its Model and Controller bundle.");
                uint firstEpoch = model.RentalEpoch;
                Assert(view.Model == model && view.IsObserving &&
                       model.IsRented && controller.IsRented && controller.RentalEpoch == firstEpoch &&
                       controller.IsCurrentRental(firstEpoch) && model.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1,
                    "First rental did not establish one current Ball MVC observation.");

                Assert(controller.TryReturn(firstEpoch), "Current BallController return failed.");
                Assert(!firstLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !view.IsObserving && view.Model == null,
                    "Controller return did not detach the Ball MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease secondLease), "Ball re-rent failed.");
                Assert(ReferenceEquals(secondLease.Value, view) && ReferenceEquals(view.OwnedModel, model) &&
                       ReferenceEquals(view.Controller, controller),
                    "Ball re-rent recreated a View, Model, or Controller instead of reusing the bundle.");

                uint secondEpoch = model.RentalEpoch;
                Assert(secondEpoch != firstEpoch && model.IsRented && controller.IsRented &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       !model.IsCurrentRental(firstEpoch) && !controller.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1 && view.IsObserving && view.Model == model,
                    "Re-rent did not advance the epoch or restore exactly one observation.");
                Assert(!firstLease.Return() && !controller.TryReturn(firstEpoch) && secondLease.IsValid &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       model.ObserverCount == 1,
                    "A stale PoolLease or BallController epoch disturbed the newer rental.");

                Assert(secondLease.Return(), "Current PoolLease return failed.");
                Assert(!secondLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !view.IsObserving && view.Model == null,
                    "PoolLease return did not detach the Ball MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease disposeLease),
                    "Ball rent before active pool disposal failed.");
                uint disposeEpoch = model.RentalEpoch;
                pool.Dispose();
                Assert(!disposeLease.IsValid && !model.IsRented && !controller.IsRented &&
                       !model.IsCurrentRental(disposeEpoch) && !controller.IsCurrentRental(disposeEpoch) &&
                       model.ObserverCount == 0 && !view.IsObserving && view.Model == null,
                    "Active pool disposal left the Ball MVC rental or observer connected.");

                IPool hierarchyFirstPool = factory.GetPool(hierarchyFirstConfig);
                Assert(factory.TryRent(hierarchyFirstConfig, args, out PoolLease hierarchyLease),
                    "Hierarchy-first Ball rent failed.");
                BallView hierarchyView = hierarchyLease.Value as BallView;
                Assert(hierarchyView != null, "Hierarchy-first pool did not return a BallView.");
                BallModel hierarchyModel = hierarchyView.OwnedModel;
                BallController hierarchyController = hierarchyView.Controller;
                uint hierarchyEpoch = hierarchyModel.RentalEpoch;

                DestroyImmediate(hierarchyView.gameObject);
                Assert(!hierarchyModel.IsRented && !hierarchyController.IsRented &&
                       !hierarchyModel.IsCurrentRental(hierarchyEpoch) &&
                       !hierarchyController.IsCurrentRental(hierarchyEpoch) &&
                       hierarchyModel.ObserverCount == 0,
                    "Hierarchy-first destruction left the Ball MVC bundle active before pool disposal.");
                Assert(!hierarchyLease.IsValid && !hierarchyLease.Return() &&
                       !hierarchyController.TryLaunch(hierarchyEpoch, Vector3.right, BallTrajectoryMode.Straight),
                    "A destroyed Ball remained reachable through its lease or controller before pool disposal.");

                hierarchyFirstPool.Dispose();
                Assert(!hierarchyLease.IsValid,
                    "Hierarchy-first pool disposal did not invalidate the destroyed Ball lease.");

                factory.Dispose();
                factory = null;
                result.success = true;
            }
            catch (Exception exception)
            {
                result.success = false;
                result.error = exception.ToString();
            }
            finally
            {
                try { factory?.Dispose(); }
                catch (Exception exception)
                {
                    result.success = false;
                    result.error = (result.error ?? string.Empty) + "\nCleanup: " + exception;
                }
                resolver?.Dispose();
                if (fixtureRoot != null) Destroy(fixtureRoot);
                if (config != null) Destroy(config);
                if (hierarchyFirstConfig != null) Destroy(hierarchyFirstConfig);
                if (ballSettings != null) Destroy(ballSettings);
            }

            yield return null;
            yield return null;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success) Debug.Log("[BallMvcValidation] Passed " + result.assertions + " assertions.");
            else Debug.LogError("[BallMvcValidation] " + result.error);
            Destroy(gameObject);
        }

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
