#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Pool;
using InGame.Ball;
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
            public string error;
            public string scope;
        }

        private readonly Result result = new Result
        {
            scope = "Temporary inactive sources plus in-memory PoolConfig instances in an isolated LocalPhysicsMode.Physics3D scene. No saved Scene, Prefab, PoolConfig, or ScriptableObject asset is edited."
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

                resolver = new ContainerBuilder().Build();
                factory = new PoolFactory(container, resolver, true);
                factory.Initialize(false);

                Assert(factory.TryRent(ballConfig, new PoolSpawnArgs(new Vector3(-3f, 0f, 0f), Quaternion.identity), out PoolLease ballLease),
                    "Ball rent failed.");
                Assert(factory.TryRent(obstacleConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease obstacleLease),
                    "Obstacle rent failed.");

                BallView ball = ballLease.Value as BallView;
                ObstacleView obstacle = obstacleLease.Value as ObstacleView;
                Assert(ball != null && obstacle != null, "PoolFactory did not return the expected BallView and ObstacleView.");
                Rigidbody ballBody = ball.GetComponent<Rigidbody>();
                Rigidbody obstacleBody = obstacle.GetComponent<Rigidbody>();
                Assert(ballBody != null && obstacleBody != null && ball.GetComponent<Collider>() != null && obstacle.GetComponent<Collider>() != null,
                    "Production views did not retain required root Rigidbody and Collider components.");
                ballInspector.AssertUnchanged(ballBody, Assert, "Ball first rent");
                obstacleInspector.AssertUnchanged(obstacleBody, Assert, "Obstacle first rent");
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
                Assert(!ball.Controller.TryLaunch(ballEpoch - 1u, Vector3.right), "Ball accepted a stale epoch launch.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.zero), "Ball accepted a zero launch velocity.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, new Vector3(float.NaN, 0f, 0f)), "Ball accepted a non-finite launch velocity.");
                bool originalKinematic = ballBody.isKinematic;
                try
                {
                    ballBody.isKinematic = true;
                    Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.right), "Ball accepted a kinematic launch.");
                }
                finally
                {
                    // This is a test-only mutation; Inspector preservation is checked after return.
                    ballBody.isKinematic = originalKinematic;
                }

                Vector3 obstacleStart = obstacleBody.position;
                Assert(ball.Controller.TryLaunch(ballEpoch, new Vector3(12f, 0f, 0f)), "Ball rejected a valid launch.");
                Assert(ball.Controller.HasLaunched && !ballBody.IsSleeping(), "Valid Ball launch did not mark launched and wake the body.");
                Assert(!ball.Controller.TryLaunch(ballEpoch, Vector3.right), "Ball accepted a duplicate launch in the same epoch.");

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
                Assert(result.obstacleDisplacement > 0.01f,
                    "Collision did not physically move the dynamic Obstacle.");

                Assert(ball.Controller.TryReturn(ballEpoch) && obstacle.Controller.TryReturn(obstacleEpoch), "Current rental return failed.");
                Assert(!ball.gameObject.activeSelf && !obstacle.gameObject.activeSelf &&
                       IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity) &&
                       IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity) &&
                       ballBody.IsSleeping() && obstacleBody.IsSleeping(),
                    "Return did not deactivate, clear velocity, and sleep both bodies.");
                ballInspector.AssertUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertUnchanged(obstacleBody, Assert, "Obstacle");

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
                Assert(!ballLease.Return() && !obstacleLease.Return() && !nextBall.Controller.TryLaunch(ballEpoch, Vector3.right) &&
                       nextBallLease.IsValid && nextObstacleLease.IsValid,
                    "An old lease or epoch disturbed the newer rental.");

                BallController activeBallController = nextBall.Controller;
                ObstacleController activeObstacleController = nextObstacle.Controller;
                BallModel activeBallModel = nextBall.OwnedModel;
                ObstacleModel activeObstacleModel = nextObstacle.OwnedModel;
                uint activeBallEpoch = activeBallController.RentalEpoch;
                uint activeObstacleEpoch = activeObstacleController.RentalEpoch;
                Assert(activeBallController.TryLaunch(activeBallEpoch, new Vector3(3f, 1f, 0f)),
                    "Ball launch before active PoolFactory disposal failed.");
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
                ballInspector.AssertUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertUnchanged(obstacleBody, Assert, "Obstacle");
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
            public readonly float mass;
            public readonly bool useGravity;
            public readonly bool isKinematic;
            public readonly RigidbodyConstraints constraints;
            public readonly CollisionDetectionMode collisionDetection;
            public readonly RigidbodyInterpolation interpolation;
            public readonly float linearDamping;
            public readonly float angularDamping;
            public readonly bool detectCollisions;

            public RigidbodyState(Rigidbody body)
            {
                mass = body.mass;
                useGravity = body.useGravity;
                isKinematic = body.isKinematic;
                constraints = body.constraints;
                collisionDetection = body.collisionDetectionMode;
                interpolation = body.interpolation;
                linearDamping = body.linearDamping;
                angularDamping = body.angularDamping;
                detectCollisions = body.detectCollisions;
            }

            public void AssertUnchanged(Rigidbody body, Action<bool, string> assert, string label)
            {
                assert(Mathf.Approximately(mass, body.mass) && useGravity == body.useGravity && isKinematic == body.isKinematic &&
                       constraints == body.constraints && collisionDetection == body.collisionDetectionMode && interpolation == body.interpolation &&
                       Mathf.Approximately(linearDamping, body.linearDamping) && Mathf.Approximately(angularDamping, body.angularDamping) &&
                       detectCollisions == body.detectCollisions,
                    label + " Rigidbody Inspector-authored properties changed during its pool lifecycle.");
            }
        }
    }
}
#endif
