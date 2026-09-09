#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Pool;
using InGame.Ball;
using InGame.Cannon;
using InGame.Config;
using InGame.Obstacle;
using InGame.Shot;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Framework.Test
{
    /// <summary>
    /// Play Mode-only evidence for the click-to-launch seam. All objects supplied to Begin are
    /// temporary objects in a LocalPhysicsMode.Physics3D scene.
    /// </summary>
    public sealed class ClickLaunchRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public int simulatedSteps;
            public int collisionContacts;
            public bool detectedObstacleRaycast;
            public bool rejectedOutOfScreenRaycast;
            public bool rejectedDisabledObstacle;
            public bool rejectedInvalidCurveTime;
            public bool rejectedDuplicateAndStaleLaunch;
            public bool straightDisabledGravityAtOrBelowWorldZ;
            public bool collisionEnabledGravity;
            public bool worldZEnabledGravity;
            public bool continuousCollisionModesApplied;
            public bool capacityFailureWasGraceful;
            public bool timedReturnAndRerent;
            public bool authoredPitchPreserved;
            public float barrelHorizontalAngleDegrees;
            public string error;
            public string scope = "Temporary camera, cannon, pooled ball, and obstacle in an isolated LocalPhysicsMode.Physics3D scene. No saved Scene, Prefab, or ScriptableObject asset is changed.";
        }

        private readonly Result result = new Result();
        private GameObject fixtureRoot;
        private Scene ownerScene;
        private PoolFactory factory;
        private IObjectResolver resolver;
        private PoolConfig ballConfig;
        private BallConfig ballSettings;
        private ObstacleConfig obstacleSettings;
        private PhysXConfig physXSettings;
        private string outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig config, CannonView cannon,
            Camera camera, Transform obstacle, Scene scene, string output)
        {
            fixtureRoot = root;
            ballConfig = config;
            ownerScene = scene;
            outputPath = output;
            StartCoroutine(Run(container, cannon, camera, obstacle, scene.GetPhysicsScene()));
        }

        private IEnumerator Run(PoolContainer container, CannonView cannon, Camera camera, Transform obstacle,
            PhysicsScene physicsScene)
        {
            result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            result.unityVersion = Application.unityVersion;
            try
            {
                Assert(physicsScene.IsValid(), "The temporary Physics3D scene is invalid.");
                Assert(cannon != null && camera != null && obstacle != null, "Temporary click-launch fixtures are incomplete.");

                physXSettings = ScriptableObject.CreateInstance<PhysXConfig>();
                Vector2 centerScreen = new Vector2(camera.pixelWidth * .5f, camera.pixelHeight * .5f);
                Assert(ObstacleTargetRaycaster.TryGetTarget(camera, centerScreen, physXSettings,
                        out ObstacleView detectedObstacle, out Vector3 target)
                    && detectedObstacle == obstacle.GetComponent<ObstacleView>(),
                    "The centre-screen ray did not resolve the temporary ObstacleView.");
                result.detectedObstacleRaycast = true;
                Assert(!ObstacleTargetRaycaster.TryGetTarget(camera, new Vector2(-1f, centerScreen.y),
                        physXSettings, out _, out _),
                    "Obstacle raycast accepted an out-of-screen point.");
                result.rejectedOutOfScreenRaycast = true;

                detectedObstacle.enabled = false;
                Assert(!ObstacleTargetRaycaster.TryGetTarget(camera, centerScreen, physXSettings,
                        out _, out _),
                    "Obstacle raycast accepted a disabled ObstacleView.");
                result.rejectedDisabledObstacle = true;
                detectedObstacle.enabled = true;

                Vector3 gravity = Physics.gravity;
                Assert(!BallLaunchVelocity.TryCalculate(cannon.MuzzlePosition, target, BallTrajectoryMode.Curve,
                        0f, 0f, gravity, out _),
                    "BallLaunchVelocity accepted a non-positive Curve flight time.");
                result.rejectedInvalidCurveTime = true;
                const float curveFlightSeconds = .55f;
                Assert(BallLaunchVelocity.TryCalculate(cannon.MuzzlePosition, target, BallTrajectoryMode.Curve,
                        0f, curveFlightSeconds, gravity, out Vector3 curveVelocity),
                    "BallLaunchVelocity rejected a valid fixed-flight-time Curve.");
                Vector3 curveArrival = cannon.MuzzlePosition + curveVelocity * curveFlightSeconds
                    + .5f * gravity * curveFlightSeconds * curveFlightSeconds;
                Assert(Vector3.Distance(curveArrival, target) < .001f,
                    "Fixed-flight-time Curve did not resolve to the target.");

                ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                obstacleSettings = ScriptableObject.CreateInstance<ObstacleConfig>();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance<BallConfig>(ballSettings);
                builder.RegisterInstance<ObstacleConfig>(obstacleSettings);
                resolver = builder.Build();
                resolver.InjectGameObject(obstacle.gameObject);
                factory = new PoolFactory(container, resolver, true);
                factory.Initialize(false);
                ShotDirector director = new ShotDirector(factory, ballConfig, cannon, fixtureRoot.transform,
                    ballSettings, physXSettings);
                Transform pitch = cannon.transform.Find("YawRoot/PitchPivot");
                Assert(pitch != null, "Temporary Cannon pitch fixture is missing.");
                Quaternion authoredPitch = pitch.localRotation;

                // The first fire must rent exactly one Ball and orient the Cannon toward the hit point.
                Assert(director.TryFire(target), "ShotDirector rejected the valid Obstacle raycast target.");
                Assert(director.ActiveShotCount == 1, "A valid fire did not create exactly one active shot.");
                Assert(factory.TryRent(ballConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease capacityLease) == false,
                    "Pool capacity did not reject a second simultaneous ball rent.");
                result.capacityFailureWasGraceful = true;
                Assert(!director.TryFire(target), "ShotDirector accepted a duplicate fire while the one-ball pool was exhausted.");

                BallView ball = FindActiveBall();
                Assert(ball != null && ball.Controller != null, "ShotDirector did not produce an active BallView.");
                Rigidbody body = ball.GetComponent<Rigidbody>();
                uint collisionEpoch = ball.Controller.RentalEpoch;
                Assert(body != null && ball.Controller.HasLaunched, "Active ball was not launched through BallController.");
                Assert(body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
                    "BallConfig did not apply ContinuousDynamic collision detection to the rented Ball.");
                Rigidbody obstacleBody = obstacle.GetComponent<Rigidbody>();
                Assert(obstacleBody != null && obstacleBody.collisionDetectionMode == CollisionDetectionMode.Continuous,
                    "ObstacleConfig did not apply Continuous collision detection to the Obstacle.");
                result.continuousCollisionModesApplied = true;
                Assert(!body.useGravity && body.position.z <= ballSettings.GravityActivationWorldZ,
                    "Straight launch did not keep gravity disabled at or below the world-Z threshold.");
                result.straightDisabledGravityAtOrBelowWorldZ = true;
                Vector3 velocity = body.linearVelocity;
                Assert(BallLaunchVelocity.TryCalculate(cannon.MuzzlePosition, target, BallTrajectoryMode.Straight,
                        ballSettings.LaunchSpeed, ballSettings.CurveFlightSeconds, physXSettings.Gravity,
                        out Vector3 expectedVelocity),
                    "The valid target unexpectedly has no Straight launch velocity.");
                Assert(Vector3.Angle(expectedVelocity, velocity) < .5f,
                    "ShotDirector velocity differs from BallLaunchVelocity output.");
                Vector3 barrelHorizontal = Vector3.ProjectOnPlane(cannon.BarrelDirection, Vector3.up);
                Vector3 targetHorizontal = Vector3.ProjectOnPlane(target - cannon.MuzzlePosition, Vector3.up);
                result.barrelHorizontalAngleDegrees = Vector3.Angle(barrelHorizontal, targetHorizontal);
                Assert(result.barrelHorizontalAngleDegrees < 1f, "Cannon barrel did not align horizontally with the target direction.");
                Assert(Quaternion.Angle(authoredPitch, pitch.localRotation) < .01f,
                    "Cannon aiming changed the authored child pitch rotation.");
                result.authoredPitchPreserved = true;

                ClickLaunchCollisionRecorder recorder = obstacle.GetComponent<ClickLaunchCollisionRecorder>();
                Assert(recorder != null, "Temporary obstacle has no collision recorder.");
                for (int i = 0; i < 100 && recorder.contacts == 0; i++)
                {
                    physicsScene.Simulate(.02f);
                    director.Tick(.02f);
                    result.simulatedSteps++;
                }
                result.collisionContacts = recorder.contacts;
                Assert(recorder.contacts > 0, "The launched ball did not collide with the temporary PhysX obstacle.");
                Assert(body.useGravity, "A direct Obstacle collision did not enable Straight Ball gravity.");
                result.collisionEnabledGravity = true;

                // Return the collision path, then verify the independent world-Z fallback path on a fresh Straight rental.
                director.Tick(ballSettings.MaxLifetimeSeconds + .1f);
                Assert(director.ActiveShotCount == 0, "Timed shot recycling did not return the active ball.");
                Assert(director.TryFire(target), "Timed return did not make the ball available for re-rent.");
                result.timedReturnAndRerent = true;
                ball = FindActiveBall();
                body = ball != null ? ball.GetComponent<Rigidbody>() : null;
                Assert(ball != null && body != null && ball.Controller.HasLaunched && !body.useGravity,
                    "Separate Straight rental did not begin with gravity disabled.");
                uint fallbackEpoch = ball.Controller.RentalEpoch;
                body.position = new Vector3(body.position.x, body.position.y, ballSettings.GravityActivationWorldZ);
                director.TickPhysics();
                Assert(!body.useGravity, "Ball gravity enabled at the world-Z threshold instead of after it.");
                Assert(!ball.Controller.TryActivateGravityFromObstacleCollision(collisionEpoch) && !body.useGravity,
                    "A stale collision epoch enabled gravity for the current rental.");
                body.position = new Vector3(body.position.x, body.position.y, ballSettings.GravityActivationWorldZ + .01f);
                director.TickPhysics();
                Assert(body.useGravity, "ShotDirector did not enable Ball gravity after world Z crossed the threshold.");
                result.worldZEnabledGravity = true;
                Assert(!ball.Controller.TryLaunch(ball.Controller.RentalEpoch - 1u, Vector3.right,
                           BallTrajectoryMode.Straight) &&
                       !ball.Controller.TryLaunch(ball.Controller.RentalEpoch, Vector3.right,
                           BallTrajectoryMode.Straight),
                    "A stale or duplicate launch affected the re-rented ball.");
                result.rejectedDuplicateAndStaleLaunch = true;
                director.Tick(ballSettings.MaxLifetimeSeconds + .1f);
                result.success = true;
            }
            catch (Exception exception)
            {
                result.success = false;
                result.error = exception.ToString();
            }
            finally
            {
                try { factory?.Dispose(); } catch (Exception exception) { RecordCleanupFailure("PoolFactory", exception); }
                try { resolver?.Dispose(); } catch (Exception exception) { RecordCleanupFailure("Resolver", exception); }
                try { if (fixtureRoot != null) Destroy(fixtureRoot); } catch (Exception exception) { RecordCleanupFailure("Fixture", exception); }
                try { if (ballConfig != null) Destroy(ballConfig); } catch (Exception exception) { RecordCleanupFailure("PoolConfig", exception); }
                try { if (ballSettings != null) Destroy(ballSettings); } catch (Exception exception) { RecordCleanupFailure("BallConfig", exception); }
                try { if (obstacleSettings != null) Destroy(obstacleSettings); } catch (Exception exception) { RecordCleanupFailure("ObstacleConfig", exception); }
                try { if (physXSettings != null) Destroy(physXSettings); } catch (Exception exception) { RecordCleanupFailure("PhysXConfig", exception); }
            }

            yield return null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
                if (result.success) Debug.Log("[ClickLaunchValidation] Passed " + result.assertions + " assertions.");
                else Debug.LogError("[ClickLaunchValidation] " + result.error);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ClickLaunchValidation] Evidence write failed: " + exception);
            }
            finally
            {
                if (ownerScene.IsValid()) SceneManager.UnloadSceneAsync(ownerScene);
                Destroy(gameObject);
            }
        }

        private BallView FindActiveBall()
        {
            BallView[] balls = fixtureRoot.GetComponentsInChildren<BallView>(true);
            for (int i = 0; i < balls.Length; i++)
                if (balls[i].gameObject.activeInHierarchy) return balls[i];
            return null;
        }

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void RecordCleanupFailure(string label, Exception exception)
        {
            result.success = false;
            result.error = (result.error ?? string.Empty) + "\nCleanup " + label + ": " + exception;
        }
    }

    public sealed class ClickLaunchCollisionRecorder : MonoBehaviour
    {
        public int contacts { get; private set; }
        private void OnCollisionEnter(Collision collision) => contacts++;
    }
}
#endif
