using UnityEngine;
using UnityEngine.InputSystem;

namespace Clases.Clase_2.Scripts
{
    /// <summary>
    /// Toggleable stealth mode (sigilo).
    /// Designed to work with PlayerInput (Input System) using "Send Messages":
    /// - Action "Interact" (default binding: Keyboard E) calls OnInteract.
    /// Optionally also supports polling keyboard E.
    /// </summary>
    public class CharacterStealth : MonoBehaviour, ICharacterComponent
    {
        [Header("State")]
        [SerializeField] private bool startInStealth;

        [Header("Movement")]
        [Tooltip("Applied to Character.MoveInputMultiplier when sigilo is active.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float stealthMoveMultiplier = 0.45f;

        [Header("Movement (fallback when root motion is zero)")]
        [Tooltip("If the current crouch/walk animation clip has no loop or no root motion, Animator.deltaPosition can become ~0 and the character appears stuck. When enabled, we apply a small manual translation while in stealth if there is move input.")]
        [SerializeField] private bool manualMoveFallback = true;

        [Tooltip("Animator float parameter used for strafe (-1..1).")]
        [SerializeField] private string speedXParameter = "SpeedX";

        [Tooltip("Animator float parameter used for forward/back (-1..1).")]
        [SerializeField] private string speedYParameter = "SpeedY";

        [Tooltip("World units per second when using the manual fallback while in stealth.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float stealthManualSpeed = 1.25f;

        [Tooltip("If squared root motion deltaPosition is below this, we consider it 'no movement' and allow fallback.")]
        [Min(0f)]
        [SerializeField] private float rootMotionEpsilonSqr = 0.000001f;

        [Tooltip("Minimum squared input magnitude before applying fallback.")]
        [Min(0f)]
        [SerializeField] private float inputEpsilonSqr = 0.0025f;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [Tooltip("Bool parameter to drive crouch/stealth locomotion in your Animator Controller.")]
        [SerializeField] private string stealthBoolParameter = "Crouch";

        [Header("Input (fallback)")]
        [Tooltip("If true, pressing keyboard E toggles stealth even without PlayerInput actions wired.")]
        [SerializeField] private bool pollKeyboardE;

        public Character ParentCharacter { get; set; }

        public bool IsStealthActive => ParentCharacter != null && ParentCharacter.IsStealth;

        private int _stealthBoolHash;
        private bool _animHasStealthBool;
        private float _rootMotionMultiplier = 1f;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            _stealthBoolHash = Animator.StringToHash(stealthBoolParameter);
            _animHasStealthBool = animator != null && HasBoolParameter(animator, _stealthBoolHash);
        }

        private void Start()
        {
            if (startInStealth)
                SetStealth(true);
            else
                SetStealth(false);
        }

        private void Update()
        {
            if (!pollKeyboardE) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.eKey.wasPressedThisFrame)
                ToggleStealth();
        }

        // Uses existing "Interact" action (bound to E in your Input Actions).
        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleStealth();
        }

        // Preferred when PlayerInput is set to "Invoke Unity Events" and you create an action named "Stealth".
        public void OnStealth(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleStealth();
        }

        // Optional: if you later decide to bind stealth to the existing "Crouch" action.
        public void OnCrouch(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleStealth();
        }

        public void ToggleStealth()
        {
            bool next = ParentCharacter == null || !ParentCharacter.IsStealth;
            SetStealth(next);
        }

        public void SetStealth(bool enabled)
        {
            if (ParentCharacter == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[CharacterStealth] ParentCharacter is null. Ensure this component is on the Character object or its children.", this);
#endif
                return;
            }

            ParentCharacter.IsStealth = enabled;
            ParentCharacter.MoveInputMultiplier = enabled ? stealthMoveMultiplier : 1f;
            _rootMotionMultiplier = enabled ? stealthMoveMultiplier : 1f;

            if (animator != null && _animHasStealthBool)
                animator.SetBool(_stealthBoolHash, enabled);
        }

        private void OnAnimatorMove()
        {
            // If the character is driven by Animator root motion, scale translation while in stealth.
            // We intentionally do NOT apply animator.deltaRotation because character facing is handled elsewhere.
            if (animator == null) return;

            float multiplier = _rootMotionMultiplier;
            if (multiplier <= 0f) return;

            Vector3 scaledDelta = animator.deltaPosition * multiplier;
            bool appliedRootMotion = false;

            if (scaledDelta.sqrMagnitude > rootMotionEpsilonSqr)
            {
                transform.position += scaledDelta;
                appliedRootMotion = true;
            }

            // Fallback: if crouch clips are not looped or don't contain root motion, deltaPosition can become ~0.
            // When in stealth and there is input, move manually so the character doesn't appear to hit a "limit".
            if (!appliedRootMotion && manualMoveFallback && IsStealthActive)
            {
                float sx = 0f;
                float sy = 0f;

                if (!string.IsNullOrWhiteSpace(speedXParameter))
                    sx = animator.GetFloat(speedXParameter);
                if (!string.IsNullOrWhiteSpace(speedYParameter))
                    sy = animator.GetFloat(speedYParameter);

                Vector3 localInput = new Vector3(sx, 0f, sy);
                if (localInput.sqrMagnitude > 1f)
                    localInput.Normalize();

                if (localInput.sqrMagnitude > inputEpsilonSqr)
                {
                    Vector3 worldDir = (transform.right * localInput.x) + (transform.forward * localInput.z);
                    transform.position += worldDir * (stealthManualSpeed * Time.deltaTime);
                }
            }
        }

        private static bool HasBoolParameter(Animator animator, int nameHash)
        {
            if (animator == null) return false;

            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == nameHash)
                    return true;
            }

            return false;
        }
    }
}
