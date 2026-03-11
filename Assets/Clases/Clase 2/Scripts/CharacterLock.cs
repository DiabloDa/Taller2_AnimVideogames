using UnityEngine;
using UnityEngine.InputSystem;

namespace Clases.Clase_2.Scripts
{
    public class CharacterLock : MonoBehaviour, ICharacterComponent
    {
        public Character ParentCharacter { get; set; }

        [SerializeField] private Camera camera;
        [SerializeField] private LayerMask detectionMask;
        [SerializeField] private float detectionRadius;
        [SerializeField] private float detectionAngle;

        // Input System (PlayerInput -> Behavior: Send Messages) calls methods named On<ActionName>.
        // In your Input Actions, the action name is "MiddleClick", so this method is the one Unity will look for.
        public void OnMiddleClick(InputAction.CallbackContext ctx)
        {
            OnLock(ctx);
        }

        public void OnLock(InputAction.CallbackContext ctx)
        {
            // For "Button"-style actions, performed is typically the reliable phase.
            if (!ctx.performed) return;

#if UNITY_EDITOR
            Debug.Log($"[CharacterLock] Input OK (phase={ctx.phase})");
#endif

            if (ParentCharacter == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[CharacterLock] ParentCharacter is null. Ensure CharacterLock is on the same object/children of a GameObject that has Character.cs.");
#endif
                return;
            }

            Camera cam = camera != null ? camera : Camera.main;
            if (cam == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[CharacterLock] No camera assigned and no Camera.main found (tag a camera as MainCamera or assign the field).");
#endif
                return;
            }

            if (ParentCharacter.LockTarget != null)
            {
                ParentCharacter.LockTarget = null;
#if UNITY_EDITOR
                Debug.Log("[CharacterLock] Unlock");
#endif
                return;
            }

            if (detectionRadius <= 0f)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[CharacterLock] detectionRadius <= 0. Set a positive radius in the inspector.");
#endif
                return;
            }

#if UNITY_EDITOR
            if (detectionMask.value == 0)
            {
                Debug.LogWarning("[CharacterLock] detectionMask is Nothing. Choose a layer mask that matches your targets' layers.");
            }
#endif

            Collider[] detectedObjects = Physics.OverlapSphere(transform.position, detectionRadius, detectionMask);
            if (detectedObjects == null || detectedObjects.Length == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[CharacterLock] No colliders found. radius={detectionRadius}, mask={detectionMask.value}. Check target colliders and layers.");
#endif
                return;
            }

            Transform bestTarget = null;
            float bestAngle = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;

            Vector3 cameraForward = cam.transform.forward;

            for (int i = 0; i < detectedObjects.Length; i++)
            {
                Collider obj = detectedObjects[i];
                if (obj == null) continue;

                // Aim towards the collider center (usually better than pivot position)
                Vector3 toTarget = obj.bounds.center - cam.transform.position;
                float angle = Vector3.Angle(cameraForward, toTarget);
                if (angle > detectionAngle) continue;

                float distance = Vector3.Distance(transform.position, obj.bounds.center);
                if (angle < bestAngle || (Mathf.Approximately(angle, bestAngle) && distance < bestDistance))
                {
                    bestAngle = angle;
                    bestDistance = distance;
                    bestTarget = obj.transform;
                }
            }

            if (bestTarget != null)
            {
                ParentCharacter.LockTarget = bestTarget;
#if UNITY_EDITOR
                Debug.Log($"[CharacterLock] Lock -> {bestTarget.name}");
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[CharacterLock] Colliders found ({detectedObjects.Length}) but none passed the angle filter. detectionAngle={detectionAngle} degrees.");
#endif
            }

        }

        #if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

        }
        #endif
    }
}