using InGame.Config;
using InGame.DI;
using InGame.Obstacle;
using UnityEngine;

namespace InGame.Shot
{
    public static class ObstacleTargetRaycaster
    {
        public static bool TryGetTarget(Camera camera, Vector2 screenPoint, PhysXConfig settings,
            WorldObjectControllerRegistry controllers, out ObstacleController obstacle, out Vector3 target)
        {
            obstacle = null;
            target = default;
            if (camera == null || settings == null || controllers == null || !IsFinite(screenPoint)
                || !camera.pixelRect.Contains(screenPoint))
                return false;

            float maxDistance = settings.PointerRayDistance;
            if (!IsFinite(maxDistance) || maxDistance <= 0f) return false;

            PhysicsScene physicsScene = camera.gameObject.scene.GetPhysicsScene();
            if (!physicsScene.IsValid()) return false;

            Ray ray = camera.ScreenPointToRay(screenPoint);
            if (!physicsScene.Raycast(ray.origin, ray.direction, out RaycastHit hit, maxDistance,
                    settings.PointerCollisionMask.value, settings.PointerTriggerInteraction)
                || hit.collider == null
                || !hit.collider.TryGetComponent(out ObstacleView candidate)
                || !controllers.TryGet(candidate, out ObstacleController candidateController)
                || !candidateController.IsTargetable
                || !IsFinite(hit.point))
                return false;

            obstacle = candidateController;
            target = hit.point;
            return true;
        }

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
