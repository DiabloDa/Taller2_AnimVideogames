using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

namespace Clases.Clase_2.Scripts
{
    /// <summary>
    /// Simple holster/equip system driven by Input System "Send Messages".
    /// By default this listens to action "Next" (bound to keyboard 2 in this project) via method OnNext.
    ///
    /// Behaviour:
    /// - While holstered or transitioning: can't shoot, aim, or reload.
    /// - Plays animator triggers for holster/unholster transitions.
    /// - Hides weapon visually by disabling renderers (no socket re-parent needed).
    /// - Cancels reload and aim when holstering.
    /// </summary>
    public class CharacterWeaponHolster : MonoBehaviour, ICharacterComponent
    {
        [Header("State")]
        [SerializeField] private bool startHolstered;

        [Header("Animator")]
        [SerializeField] private Animator animator;
        [Tooltip("Animator trigger to play when storing the weapon.")]
        [SerializeField] private string holsterTrigger = "Holster";
        [Tooltip("Animator trigger to play when equipping the weapon.")]
        [SerializeField] private string unholsterTrigger = "Unholster";

        [Header("Timing")]
        [Tooltip("Seconds until the weapon mesh is hidden after starting holster animation.")]
        [Min(0f)]
        [SerializeField] private float holsterHideDelay = 0.2f;
        [Tooltip("Total transition duration for holster (blocks gameplay until complete).")]
        [Min(0f)]
        [SerializeField] private float holsterTotalDuration = 0.65f;

        [Tooltip("Seconds until the weapon mesh is shown after starting unholster animation.")]
        [Min(0f)]
        [SerializeField] private float unholsterShowDelay = 0.0f;
        [Tooltip("Total transition duration for unholster (blocks gameplay until complete).")]
        [Min(0f)]
        [SerializeField] private float unholsterTotalDuration = 0.65f;

        [Header("Weapon Visuals")]
        [Tooltip("Optional root to search for weapon Renderers. If null, you can assign weaponRenderers manually.")]
        [SerializeField] private Transform weaponVisualRoot;
        [Tooltip("Renderers to enable/disable for visual hiding. If empty, we auto-fill from weaponVisualRoot.")]
        [SerializeField] private Renderer[] weaponRenderers;

        [Header("Rigging (optional)")]
        [Tooltip("If assigned, these rigs will be disabled (and weight set to 0) while holstered/transitioning.")]
        [SerializeField] private Rig[] rigsToDisable;

        [Tooltip("If true, rig weights will be smoothly blended to 0/prev. If false, weights snap.")]
        [SerializeField] private bool blendRigWeights = true;

        [Min(0f)]
        [SerializeField] private float rigBlendSeconds = 0.08f;

        [Tooltip("Optional: disable a RigBuilder while holstered. Use only if you want to turn off ALL rig constraints.")]
        [SerializeField] private RigBuilder rigBuilderToDisable;

        public Character ParentCharacter { get; set; }

        public bool IsHolstered => ParentCharacter != null && ParentCharacter.IsWeaponHolstered;

        private CharacterGun _gun;
        private CharacterAim _aim;
        private Coroutine _transitionRoutine;

        private struct RigPrev
        {
            public Rig rig;
            public float weight;
            public bool enabled;
        }

        private RigPrev[] _rigPrev;
        private bool _rigPrevCached;
        private bool _prevRigBuilderEnabled;

        private int _holsterHash;
        private int _unholsterHash;
        private bool _hasHolsterTrigger;
        private bool _hasUnholsterTrigger;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInParent<Animator>();

            _gun = GetComponentInChildren<CharacterGun>(true);
            _aim = GetComponentInChildren<CharacterAim>(true);

            RefreshWeaponRenderers();
            CacheAnimatorParameters();

            CacheRigPrev();
        }

        private void Start()
        {
            if (ParentCharacter == null)
            {
                // Character sets ParentCharacter in Awake. If this Start runs before it, try to resolve.
                ParentCharacter = GetComponentInParent<Character>();
            }

            ApplyImmediate(startHolstered);
        }

        private void CacheRigPrev()
        {
            if (_rigPrevCached) return;

            if (rigsToDisable != null && rigsToDisable.Length > 0)
            {
                _rigPrev = new RigPrev[rigsToDisable.Length];
                for (int i = 0; i < rigsToDisable.Length; i++)
                {
                    var r = rigsToDisable[i];
                    _rigPrev[i] = new RigPrev
                    {
                        rig = r,
                        weight = r != null ? r.weight : 0f,
                        enabled = r != null && r.enabled
                    };
                }
            }
            else
            {
                _rigPrev = null;
            }

            if (rigBuilderToDisable != null)
                _prevRigBuilderEnabled = rigBuilderToDisable.enabled;

            _rigPrevCached = true;
        }

        private void ApplyRigStateImmediate(bool enableWeaponRigs)
        {
            CacheRigPrev();

            if (rigBuilderToDisable != null)
                rigBuilderToDisable.enabled = enableWeaponRigs ? _prevRigBuilderEnabled : false;

            if (_rigPrev == null) return;

            for (int i = 0; i < _rigPrev.Length; i++)
            {
                var prev = _rigPrev[i];
                if (prev.rig == null) continue;

                if (enableWeaponRigs)
                {
                    prev.rig.enabled = prev.enabled;
                    prev.rig.weight = prev.weight;
                }
                else
                {
                    prev.rig.weight = 0f;
                    prev.rig.enabled = false;
                }
            }
        }

        private IEnumerator BlendRigWeightsTo(bool enableWeaponRigs)
        {
            CacheRigPrev();

            if (!blendRigWeights || rigBlendSeconds <= 0f || _rigPrev == null)
            {
                ApplyRigStateImmediate(enableWeaponRigs);
                yield break;
            }

            // Enable rigs first when bringing them back so weights can blend in.
            if (enableWeaponRigs)
            {
                if (rigBuilderToDisable != null)
                    rigBuilderToDisable.enabled = _prevRigBuilderEnabled;

                for (int i = 0; i < _rigPrev.Length; i++)
                {
                    var prev = _rigPrev[i];
                    if (prev.rig == null) continue;
                    prev.rig.enabled = prev.enabled;
                }
            }

            float t = 0f;
            float[] start = new float[_rigPrev.Length];
            float[] target = new float[_rigPrev.Length];

            for (int i = 0; i < _rigPrev.Length; i++)
            {
                var prev = _rigPrev[i];
                if (prev.rig == null)
                {
                    start[i] = 0f;
                    target[i] = 0f;
                    continue;
                }

                start[i] = prev.rig.weight;
                target[i] = enableWeaponRigs ? prev.weight : 0f;
            }

            while (t < rigBlendSeconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / rigBlendSeconds);
                for (int i = 0; i < _rigPrev.Length; i++)
                {
                    var prev = _rigPrev[i];
                    if (prev.rig == null) continue;
                    prev.rig.weight = Mathf.Lerp(start[i], target[i], a);
                }
                yield return null;
            }

            // Snap end.
            for (int i = 0; i < _rigPrev.Length; i++)
            {
                var prev = _rigPrev[i];
                if (prev.rig == null) continue;
                prev.rig.weight = enableWeaponRigs ? prev.weight : 0f;
            }

            // When disabling, turn them off after fade-out.
            if (!enableWeaponRigs)
            {
                for (int i = 0; i < _rigPrev.Length; i++)
                {
                    var prev = _rigPrev[i];
                    if (prev.rig == null) continue;
                    prev.rig.enabled = false;
                }

                if (rigBuilderToDisable != null)
                    rigBuilderToDisable.enabled = false;
            }
        }

        private void RefreshWeaponRenderers()
        {
            if (weaponRenderers != null && weaponRenderers.Length > 0) return;
            if (weaponVisualRoot == null) return;

            weaponRenderers = weaponVisualRoot.GetComponentsInChildren<Renderer>(true);
        }

        private void CacheAnimatorParameters()
        {
            _hasHolsterTrigger = false;
            _hasUnholsterTrigger = false;

            _holsterHash = Animator.StringToHash(holsterTrigger);
            _unholsterHash = Animator.StringToHash(unholsterTrigger);

            if (animator == null) return;

            foreach (var p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Trigger) continue;
                if (p.nameHash == _holsterHash) _hasHolsterTrigger = true;
                if (p.nameHash == _unholsterHash) _hasUnholsterTrigger = true;
            }
        }

        // --- Input hooks ---
        // If you use PlayerInput "Invoke Unity Events", wire the Player/Next event to one of these.

        // Matches PlayerInput Action Event signature.
        public void ToggleWeapon(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            Toggle();
        }

        // Parameterless alternative (some UnityEvent setups are easier without args).
        public void ToggleWeapon()
        {
            Toggle();
        }

        // Backwards-compatible name if you prefer wiring "Next" to a method named OnNext.
        public void OnNext(InputAction.CallbackContext ctx)
        {
            ToggleWeapon(ctx);
        }

        public void Toggle()
        {
            if (ParentCharacter == null) return;
            if (ParentCharacter.IsWeaponTransitioning) return;

            if (ParentCharacter.IsWeaponHolstered)
                BeginUnholster();
            else
                BeginHolster();
        }

        private void BeginHolster()
        {
            if (ParentCharacter == null) return;

            ParentCharacter.IsWeaponTransitioning = true;
            ParentCharacter.IsWeaponHolstered = true; // block immediately

            _aim?.ForceStopAiming();
            _gun?.CancelReload();

            ApplyRigStateImmediate(false);

            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(HolsterRoutine());
        }

        private void BeginUnholster()
        {
            if (ParentCharacter == null) return;

            ParentCharacter.IsWeaponTransitioning = true;
            ParentCharacter.IsWeaponHolstered = true; // stay blocked during animation

            _aim?.ForceStopAiming();
            _gun?.CancelReload();

            // Keep rigs disabled during the unholster animation, then restore near the end.
            ApplyRigStateImmediate(false);

            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(UnholsterRoutine());
        }

        private IEnumerator HolsterRoutine()
        {
            TriggerAnimator(holsterTrigger, _holsterHash, _hasHolsterTrigger);

            if (holsterHideDelay > 0f)
                yield return new WaitForSeconds(holsterHideDelay);

            SetWeaponVisible(false);

            float remaining = holsterTotalDuration - holsterHideDelay;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (ParentCharacter != null)
                ParentCharacter.IsWeaponTransitioning = false;

            _transitionRoutine = null;
        }

        private IEnumerator UnholsterRoutine()
        {
            TriggerAnimator(unholsterTrigger, _unholsterHash, _hasUnholsterTrigger);

            if (unholsterShowDelay > 0f)
                yield return new WaitForSeconds(unholsterShowDelay);

            SetWeaponVisible(true);

            // Restore weapon-related rigs after the weapon is visible again.
            yield return BlendRigWeightsTo(true);

            float remaining = unholsterTotalDuration - unholsterShowDelay;
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (ParentCharacter != null)
            {
                ParentCharacter.IsWeaponHolstered = false;
                ParentCharacter.IsWeaponTransitioning = false;
            }

            _transitionRoutine = null;
        }

        private void ApplyImmediate(bool holstered)
        {
            if (ParentCharacter != null)
            {
                ParentCharacter.IsWeaponHolstered = holstered;
                ParentCharacter.IsWeaponTransitioning = false;
            }

            if (holstered)
            {
                _aim?.ForceStopAiming();
                _gun?.CancelReload();
            }

            SetWeaponVisible(!holstered);

            ApplyRigStateImmediate(!holstered);
        }

        private void TriggerAnimator(string triggerName, int triggerHash, bool hasTrigger)
        {
            if (animator == null) return;
            if (string.IsNullOrWhiteSpace(triggerName)) return;

            if (hasTrigger) animator.SetTrigger(triggerHash);
            else
            {
                // Safe fallback: avoid "Parameter does not exist" spam.
                CacheAnimatorParameters();
                if (_hasHolsterTrigger && triggerHash == _holsterHash) animator.SetTrigger(_holsterHash);
                else if (_hasUnholsterTrigger && triggerHash == _unholsterHash) animator.SetTrigger(_unholsterHash);
            }
        }

        private void SetWeaponVisible(bool visible)
        {
            RefreshWeaponRenderers();
            if (weaponRenderers == null) return;

            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                var r = weaponRenderers[i];
                if (r == null) continue;
                r.enabled = visible;
            }
        }
    }
}
