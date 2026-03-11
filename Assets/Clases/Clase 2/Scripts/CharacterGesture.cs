using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Clases.Clase_2.Scripts
{
    public class CharacterGesture : MonoBehaviour, ICharacterComponent
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Header("Input")]
        [Tooltip("If true, pressing keyboard T will trigger the salute using the new Input System Keyboard device.")]
        [SerializeField] private bool pollKeyboardT = true;

        [Header("Animator Setup")]
        [Tooltip("Animator layer index used for gestures (should be a separate upper-body masked layer).")]
        [SerializeField] private int gestureLayer = 2;

        [Tooltip("Trigger parameter name that starts the salute animation on the gesture layer.")]
        [SerializeField] private string saluteTrigger = "Salute";

        [Tooltip("State name on the gesture layer that plays the salute clip (used to detect when to fade out).")]
        [SerializeField] private string saluteStateName = "Salute";

        [Header("Blend")]
        [SerializeField] private float fadeInSeconds = 0.08f;
        [SerializeField] private float fadeOutSeconds = 0.10f;

        [Header("Rules")]
        [SerializeField] private bool blockWhileFiring = true;
        [SerializeField] private bool blockWhileReloading = true;

        public Character ParentCharacter { get; set; }

        private int _saluteTriggerHash;
        private Coroutine _gestureRoutine;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            _saluteTriggerHash = Animator.StringToHash(saluteTrigger);

            if (animator != null && gestureLayer >= 0 && gestureLayer < animator.layerCount)
                animator.SetLayerWeight(gestureLayer, 0f);
        }

        private void Update()
        {
            if (!pollKeyboardT) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.tKey.wasPressedThisFrame)
                TrySalute();
        }

        // Optional: if you later add an InputAction named "Gesture" (or similar) with PlayerInput SendMessages,
        // Unity will call OnGesture automatically.
        public void OnGesture(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            TrySalute();
        }

        public void TrySalute()
        {
            if (!IsAllowed()) return;
            if (animator == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[CharacterGesture] No Animator found/assigned.", this);
#endif
                return;
            }

            if (gestureLayer < 0 || gestureLayer >= animator.layerCount)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[CharacterGesture] gestureLayer={gestureLayer} is out of range. Animator has {animator.layerCount} layers.", this);
#endif
                return;
            }

            if (_gestureRoutine != null)
                StopCoroutine(_gestureRoutine);

            _gestureRoutine = StartCoroutine(PlaySaluteRoutine());
        }

        private bool IsAllowed()
        {
            if (ParentCharacter == null) return true;

            if (blockWhileFiring && ParentCharacter.IsFiring) return false;
            if (blockWhileReloading && ParentCharacter.IsReloading) return false;

            return true;
        }

        private IEnumerator PlaySaluteRoutine()
        {
            yield return FadeLayerWeight(gestureLayer, 1f, fadeInSeconds);

            animator.ResetTrigger(_saluteTriggerHash);
            animator.SetTrigger(_saluteTriggerHash);

            // Wait until we enter the salute state on the gesture layer (avoid false positives).
            float timeout = 1f;
            while (timeout > 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(gestureLayer);
                if (info.IsName(saluteStateName))
                    break;

                timeout -= Time.deltaTime;
                yield return null;
            }

#if UNITY_EDITOR
            {
                var info = animator.GetCurrentAnimatorStateInfo(gestureLayer);
                if (!info.IsName(saluteStateName))
                    Debug.LogWarning($"[CharacterGesture] Timed out waiting for state '{saluteStateName}' on gesture layer. Check Animator state name & transitions.", this);
            }
#endif

            // Wait until the salute finishes.
            float maxDuration = 5f;
            while (maxDuration > 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(gestureLayer);
                bool inSalute = info.IsName(saluteStateName);

                if (inSalute && !animator.IsInTransition(gestureLayer) && info.normalizedTime >= 1f)
                    break;

                maxDuration -= Time.deltaTime;
                yield return null;
            }

            yield return FadeLayerWeight(gestureLayer, 0f, fadeOutSeconds);
            _gestureRoutine = null;
        }

        private IEnumerator FadeLayerWeight(int layer, float target, float seconds)
        {
            float start = animator.GetLayerWeight(layer);
            if (seconds <= 0f)
            {
                animator.SetLayerWeight(layer, target);
                yield break;
            }

            float time = 0f;
            while (time < seconds)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / seconds);
                animator.SetLayerWeight(layer, Mathf.Lerp(start, target, t));
                yield return null;
            }

            animator.SetLayerWeight(layer, target);
        }
    }
}
