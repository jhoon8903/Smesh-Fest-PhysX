#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Pool;
using InGame.Ball;
using InGame.Config;
using InGame.Obstacle;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.Test
{
    public sealed class PhysXCollisionRecorder : MonoBehaviour
    {
        public int contacts;
        private void OnCollisionEnter(Collision collision) => contacts++;
    }

    /// <summary>Play Mode evidence for the pooled Ball/Obstacle PhysX lifecycle in a local PhysicsScene.</summary>
    public sealed class PhysXRuntimeProbe : MonoBehaviour
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
            public float obstacleDisplacement;
            public bool initialBallSleeping;
            public bool initialObstacleSleeping;
            public bool collisionEnabledGravity;
            public bool worldZEnabledGravity;
            public string error;
            public string scope;
        }

        private readonly Result result = new Result
        {
            scope = "Temporary inactive sources plus in-memory PoolConfig, BallConfig, and ObstacleConfig instances in an isolated LocalPhysicsMode.Physics3D scene. No saved Scene, Prefab, PoolConfig, or ScriptableObject asset is edited."
        };

        private GameObject fixtureRoot;
        private PoolFactory factory;
        private IObjectResolver resolver;
        private string outputPath;
        private Scene physicsSceneOwner;

        public void Begin(GameObject root, PoolContainer container, PoolConfig ballConfig, PoolConfig obstacleConfig,
            Scene isolatedScene, string output)
        {
            fixtureRoot = root;
            physicsSceneOwner = isolatedScene;
            outputPath = output;
            StartCoroutine(Run(container, ballConfig, obstacleConfig, isolatedScene.GetPhysicsScene()));
        }

        private IEnumerator Run(PoolContainer container, PoolConfig ballConfig, PoolConfig obstacleConfig, PhysicsScene physicsScene)
        {
            result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            result.unityVersion = Application.unityVersion;
            BallConfig ballSettings = null;
            ObstacleConfig obstacleSettings = null;
            try
            {
                Assert(physicsScene.IsValid(), "The local Physics3D scene did not provide a valid PhysicsScene.");
                AssertKinematicSourceContracts();
                Rigidbody ballSourceBody = ballConfig != null && ballConfig.Prefab != null
                    ? ballConfig.Prefab.GetComponent<Rigidbody>()
                    : null;
                Rigidbody obstacleSourceBody = obstacleConfig != null && obstacleConfig.Prefab != null
                    ? obstacleConfig.Prefab.GetComponent<Rigidbody>()
                    : null;
                Assert(ballSourceBody != null && obstacleSourceBody != null,
                    "The temporary PoolConfig sources did not retain their Rigidbody components.");
                RigidbodyState ballInspector = new RigidbodyState(ballSourceBody);
                RigidbodyState obstacleInspector = new RigidbodyState(obstacleSourceBody);

                ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                obstacleSettings = ScriptableObject.CreateInstance<ObstacleConfig>();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance(ballSettings);
                builder.RegisterInstance(obstacleSettings);
                resolver = builder.Build();
                factory = new PoolFactory(container, resolver, true);
                factory.Initialize(false);

                Assert(factory.TryRent(ballConfig, new PoolSpawnArgs(new Vector3(-3f, 0f, -0.25f), Quaternion.identity), out PoolLease ballLease),
                    "Ball rent failed.");
                Assert(factory.TryRent(obstacleConfig, new PoolSpawnArgs(new Vector3(0f, 0f, -0.25f), Quaternion.identity), out PoolLease obstacleLease),
                    "Obstacle rent failed.");

                BallView ball = ballLease.Value as BallView;
                ObstacleView obstacle = obstacleLease.Value as ObstacleView;
                Assert(ball != null && obstacle != null, "PoolFactory did not return the expected BallView and ObstacleView.");
                Rigidbody ballBody = ball.GetComponent<Rigidbody>();
                Rigidbody obstacleBody = obstacle.GetComponent<Rigidbody>();
                Assert(ballBody != null && obstacleBody != null && ball.GetComponent<Collider>() != null && obstacle.GetComponent<Collider>() != null,
                    "Production views did not retain required root Rigidbody and Collider components.");
                AssertConfiguredPhysics(ballBody, ballSettings, "Ball first rent");
                AssertConfiguredPhysics(obstacleBody, obstacleSettings, "Obstacle first rent");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball first rent");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle first rent");
                Assert(ball.Controller.IsCurrentRental(ball.Controller.RentalEpoch) && obstacle.Controller.IsCurrentRental(obstacle.Controller.RentalEpoch),
                    "The initial rental epochs are not current.");
                Assert(!ballBody.isKinematic && !obstacleBody.isKinematic,
                    "The PhysX lifecycle accepted a non-dynamic Rigidbody.");
                result.initialBallSleeping = ballBody.IsSleeping();
                result.initialObstacleSleeping = obstacleBody.IsSleeping();
                Assert(!ball.Controller.HasLaunched, "Ball rent incorrectly began in a launched state.");
                Assert(IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity),
                    "Ball rent did not clear its linear and angular velocity.");
                Assert(result.initialBallSleeping, "Ball activation did not preserve the pre-launch sleep contract.");
                Assert(IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity),
                    "Obstacle rent did not clear its linear and angular velocity.");
                Assert(!result.initialObstacleSleeping, "Obstacle activation did not wake the reset body.");

                uint ballEpoch = ball.Controller.RentalEpoch;
                uint obstacleEpoch = obstacle.Controller.RentalEpoch;
                Assert(!ball.Controller.TryLaunch(ballEpoch - 1u, Vector3.right, BallTrajectoryMode.Straight),
                    "Ball accepted a stale epoch launch.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.zero, BallTrajectoryMode.Straight),
                    "Ball accepted a zero launch velocity.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, new Vector3(float.NaN, 0f, 0f), BallTrajectoryMode.Straight),
                    "Ball accepted a non-finite launch velocity.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.right, (BallTrajectoryMode)(-1)),
                    "Ball accepted an unknown trajectory mode.");
                bool originalKinematic = ballBody.isKinematic;
                try
                {
                    ballBody.isKinematic = true;
                    Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight),
                        "Ball accepted a kinematic launch.");
                }
                finally
                {
                    // This is a test-only mutation; Inspector preservation is checked after return.
                    ballBody.isKinematic = originalKinematic;
                }

                Vector3 obstacleStart = obstacleBody.position;
                Assert(ball.Controller.TryLaunch(ballEpoch, new Vector3(12f, 0f, 0f), BallTrajectoryMode.Straight),
                    "Ball rejected a valid Straight launch.");
                Assert(ball.Controller.HasLaunched && !ballBody.IsSleeping() && !ballBody.useGravity,
                    "Straight Ball launch did not wake the body with gravity disabled.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight),
                    "Ball accepted a duplicate launch in the same epoch.");

                PhysXCollisionRecorder contact = obstacle.GetComponent<PhysXCollisionRecorder>();
                Assert(contact != null, "Obstacle source did not provide its temporary collision recorder.");
                for (int i = 0; i < 60 && contact.contacts == 0; i++)
                {
                    physicsScene.Simulate(0.02f);
                    result.simulatedSteps++;
                }
                Assert(contact.contacts > 0, "Ball launch did not produce OnCollisionEnter contact with Obstacle.");
                for (int i = 0; i < 5; i++)
                {
                    physicsScene.Simulate(0.02f);
                    result.simulatedSteps++;
                }
                result.collisionContacts = contact.contacts;
                result.obstacleDisplacement = Vector3.Distance(obstacleBody.position, obstacleStart);
                Assert(ballBody.useGravity,
                    "A direct Obstacle collision did not enable Straight Ball gravity.");
                result.collisionEnabledGravity = true;
                Assert(result.obstacleDisplacement > 0.01f,
                    "Collision did not physically move the dynamic Obstacle.");

                Assert(ball.Controller.TryReturn(ballEpoch) && obstacle.Controller.TryReturn(obstacleEpoch), "Current rental return failed.");
                Assert(!ball.gameObject.activeSelf && !obstacle.gameObject.activeSelf &&
                       IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity) &&
                       IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity) &&
                       ballBody.IsSleeping() && obstacleBody.IsSleeping(),
                    "Return did not deactivate, clear velocity, and sleep both bodies.");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle");

                Assert(factory.TryRent(ballConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease nextBallLease),
                    "Ball re-rent failed.");
                Assert(factory.TryRent(obstacleConfig, new PoolSpawnArgs(new Vector3(4f, 0f, 0f), Quaternion.identity), out PoolLease nextObstacleLease),
                    "Obstacle re-rent failed.");
                BallView nextBall = nextBallLease.Value as BallView;
                ObstacleView nextObstacle = nextObstacleLease.Value as ObstacleView;
                Assert(ReferenceEquals(nextBall, ball) && ReferenceEquals(nextObstacle, obstacle), "Re-rent did not reuse the same pooled body instances.");
                Assert(nextBall.Controller.RentalEpoch != ballEpoch && nextObstacle.Controller.RentalEpoch != obstacleEpoch,
                    "Re-rent did not advance the lifecycle epochs.");
                Assert(!nextBall.Controller.HasLaunched && IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity) &&
                       ballBody.IsSleeping() && IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity) &&
                       !obstacleBody.IsSleeping(),
                    "Re-rent did not restore cleared velocity, Ball sleep, and Obstacle wake state.");
                AssertConfiguredPhysics(ballBody, ballSettings, "Ball re-rent");
                AssertConfiguredPhysics(obstacleBody, obstacleSettings, "Obstacle re-rent");
                Assert(!ballLease.Return() && !obstacleLease.Return() &&
                       !nextBall.Controller.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight) &&
                       nextBallLease.IsValid && nextObstacleLease.IsValid,
                    "An old lease or epoch disturbed the newer rental.");

                BallController fallbackBallController = nextBall.Controller;
                uint fallbackBallEpoch = fallbackBallController.RentalEpoch;
                Assert(fallbackBallController.TryLaunch(fallbackBallEpoch, Vector3.right, BallTrajectoryMode.Straight)
                       && !ballBody.useGravity,
                    "Separate Straight rental did not begin with gravity disabled.");
                ballBody.position = new Vector3(ballBody.position.x, ballBody.position.y, ballSettings.GravityActivationWorldZ);
                fallbackBallController.TickPhysics(fallbackBallEpoch, ballSettings.GravityActivationWorldZ);
                Assert(!ballBody.useGravity,
                    "Straight Ball enabled gravity at world Z equal to the configured threshold.");
                Assert(!fallbackBallController.TryActivateGravityFromObstacleCollision(ballEpoch) && !ballBody.useGravity,
                    "A stale collision epoch enabled gravity for the current rental.");
                ballBody.position = new Vector3(ballBody.position.x, ballBody.position.y, ballSettings.GravityActivationWorldZ + 0.01f);
                fallbackBallController.TickPhysics(fallbackBallEpoch, ballSettings.GravityActivationWorldZ);
                Assert(ballBody.useGravity,
                    "Straight Ball did not enable gravity after world Z crossed the configured threshold.");
                result.worldZEnabledGravity = true;

                Assert(nextBall.Controller.TryReturn(fallbackBallEpoch) && nextObstacle.Controller.TryReturn(nextObstacle.Controller.RentalEpoch),
                    "Separate Straight fallback rental return failed.");
                Assert(factory.TryRent(ballConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out nextBallLease)
                       && factory.TryRent(obstacleConfig, new PoolSpawnArgs(new Vector3(4f, 0f, 0f), Quaternion.identity), out nextObstacleLease),
                    "Curve verification re-rent failed.");
                nextBall = nextBallLease.Value as BallView;
                nextObstacle = nextObstacleLease.Value as ObstacleView;
                Assert(nextBall != null && nextObstacle != null, "Curve verification re-rent returned an unexpected view.");

                BallController activeBallController = nextBall.Controller;
                ObstacleController activeObstacleController = nextObstacle.Controller;
                BallModel activeBallModel = nextBall.OwnedModel;
                ObstacleModel activeObstacleModel = nextObstacle.OwnedModel;
                uint activeBallEpoch = activeBallController.RentalEpoch;
                uint activeObstacleEpoch = activeObstacleController.RentalEpoch;
                Assert(activeBallController.TryLaunch(activeBallEpoch, new Vector3(3f, 1f, 0f), BallTrajectoryMode.Curve)
                       && ballBody.useGravity,
                    "Curve Ball launch before active PoolFactory disposal did not enable gravity.");
                obstacleBody.linearVelocity = new Vector3(-2f, 0.5f, 0f);
                obstacleBody.angularVelocity = new Vector3(0f, 1f, 0f);
                obstacleBody.WakeUp();

                factory.Dispose();
                factory = null;
                Assert(!nextBallLease.IsValid && !nextObstacleLease.IsValid &&
                       !activeBallModel.IsRented && !activeObstacleModel.IsRented &&
                       !activeBallController.IsCurrentRental(activeBallEpoch) &&
                       !activeObstacleController.IsCurrentRental(activeObstacleEpoch) &&
                       nextBall.OwnedModel == null && nextBall.Controller == null &&
                       nextObstacle.OwnedModel == null && nextObstacle.Controller == null,
                    "Active PoolFactory disposal left a lease or managed Ball/Obstacle rental alive.");
                Assert(!nextBall.gameObject.activeSelf && !nextObstacle.gameObject.activeSelf &&
                       IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity) &&
                       IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity) &&
                       ballBody.IsSleeping() && obstacleBody.IsSleeping(),
                    "Active PoolFactory disposal did not deactivate, clear, and sleep both bodies.");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle");
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
                catch (Exception exception) { RecordCleanupFailure("PoolFactory", exception); }
                try { resolver?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("Resolver", exception); }
                try { if (ballSettings != null) Destroy(ballSettings); }
                catch (Exception exception) { RecordCleanupFailure("BallConfig", exception); }
                try { if (obstacleSettings != null) Destroy(obstacleSettings); }
                catch (Exception exception) { RecordCleanupFailure("ObstacleConfig", exception); }
                try { if (fixtureRoot != null) Destroy(fixtureRoot); }
                catch (Exception exception) { RecordCleanupFailure("Fixture root", exception); }
                try { if (ballConfig != null) Destroy(ballConfig); }
                catch (Exception exception) { RecordCleanupFailure("Ball PoolConfig", exception); }
                try { if (obstacleConfig != null) Destroy(obstacleConfig); }
                catch (Exception exception) { RecordCleanupFailure("Obstacle PoolConfig", exception); }
            }

            yield return null;
            try
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
                }
                catch (Exception exception)
                {
                    result.success = false;
                    result.error = (result.error ?? string.Empty) + "\nEvidence write: " + exception;
                }

                if (result.success) Debug.Log("[PhysXValidation] Passed " + result.assertions + " assertions.");
                else Debug.LogError("[PhysXValidation] " + result.error);
            }
            finally
            {
                if (physicsSceneOwner.IsValid()) SceneManager.UnloadSceneAsync(physicsSceneOwner);
                Destroy(gameObject);
            }
        }

        private static bool IsZero(Vector3 value) => value.sqrMagnitude < 0.000001f;

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private void RecordCleanupFailure(string label, Exception exception)
        {
            result.success = false;
            result.error = (result.error ?? string.Empty) + "\nCleanup " + label + ": " + exception;
        }

        private void AssertConfiguredPhysics(Rigidbody body, BallConfig settings, string label)
        {
            Assert(Mathf.Approximately(body.mass, settings.Mass) &&
                   Mathf.Approximately(body.linearDamping, settings.LinearDamping) &&
                   Mathf.Approximately(body.angularDamping, settings.AngularDamping) &&
                   body.interpolation == settings.Interpolation &&
                   body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
                label + " did not apply BallConfig-owned Rigidbody settings, including ContinuousDynamic collision detection.");
        }

        private void AssertConfiguredPhysics(Rigidbody body, ObstacleConfig settings, string label)
        {
            Assert(Mathf.Approximately(body.mass, settings.Mass) &&
                   Mathf.Approximately(body.linearDamping, settings.LinearDamping) &&
                   Mathf.Approximately(body.angularDamping, settings.AngularDamping) &&
                   body.interpolation == settings.Interpolation &&
                   body.collisionDetectionMode == CollisionDetectionMode.Continuous,
                label + " did not apply ObstacleConfig-owned Rigidbody settings, including Continuous collision detection.");
        }

        private void AssertKinematicSourceContracts()
        {
            GameObject ballSource = new GameObject("__PhysXValidation_KinematicBall");
            ballSource.transform.SetParent(fixtureRoot.transform, false);
            ballSource.SetActive(false);
            Rigidbody ballBody = ballSource.AddComponent<Rigidbody>();
            ballBody.isKinematic = true;
            ballSource.AddComponent<SphereCollider>();
            BallView ballView = ballSource.AddComponent<BallView>();
            bool ballRejected = false;
            try { ballView.OnPoolCreated(ballView); }
            catch (InvalidOperationException) { ballRejected = true; }
            Assert(ballRejected && ballView.OwnedModel == null && ballView.Controller == null,
                "BallView did not reject a kinematic Rigidbody before creating its MVC bundle.");
            Destroy(ballSource);

            GameObject obstacleSource = new GameObject("__PhysXValidation_KinematicObstacle");
            obstacleSource.transform.SetParent(fixtureRoot.transform, false);
            obstacleSource.SetActive(false);
            Rigidbody obstacleBody = obstacleSource.AddComponent<Rigidbody>();
            obstacleBody.isKinematic = true;
            obstacleSource.AddComponent<BoxCollider>();
            ObstacleView obstacleView = obstacleSource.AddComponent<ObstacleView>();
            bool obstacleRejected = false;
            try { obstacleView.OnPoolCreated(obstacleView); }
            catch (InvalidOperationException) { obstacleRejected = true; }
            Assert(obstacleRejected && obstacleView.OwnedModel == null && obstacleView.Controller == null,
                "ObstacleView did not reject a kinematic Rigidbody before creating its MVC bundle.");
            Destroy(obstacleSource);
        }

        private readonly struct RigidbodyState
        {
            public readonly bool useGravity;
            public readonly bool isKinematic;
            public readonly RigidbodyConstraints constraints;
            public readonly bool detectCollisions;

            public RigidbodyState(Rigidbody body)
            {
                useGravity = body.useGravity;
                isKinematic = body.isKinematic;
                constraints = body.constraints;
                detectCollisions = body.detectCollisions;
            }

            public void AssertNonConfigUnchanged(Rigidbody body, Action<bool, string> assert, string label)
            {
                assert(useGravity == body.useGravity && isKinematic == body.isKinematic &&
                       constraints == body.constraints && detectCollisions == body.detectCollisions,
                    label + " Rigidbody non-Config properties changed during its pool lifecycle.");
            }
        }
    }
}
#endif
