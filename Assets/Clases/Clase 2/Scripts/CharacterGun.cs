using Clases.Clase_2.Scripts;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Clases.Clase_2.Scripts
{
    public class CharacterGun : MonoBehaviour, ICharacterComponent
    {

        [SerializeField] private Camera mainCamera;
        [SerializeField] private Animator animator;
        [SerializeField] private RecoilCameraKick recoil;


        [Header("Shooting")]
        [SerializeField] private bool automatic;
        [SerializeField] private bool requiereAim = true;
        [SerializeField] private float fireRate = 10f;
        [SerializeField] private float range = 20f;
        [SerializeField] private LayerMask hitMask;

        [Header("RecoilCamera")]
        [SerializeField] private float camShake = 0.6f;
        [SerializeField] private float camKick = 0.12f;
        [SerializeField] private float camRecover = 0.18f;

        [SerializeField] private Transform tracerOrigin;

        [SerializeField] private bool isFiring;

        [Header("Ammo")]
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int ammoInMagazine = 30;
        [SerializeField] private int ammoReserve = 90;

        [Header("Reload")]
        [SerializeField] private bool autoReloadWhenEmpty = true;
        [Tooltip("If true, pressing keyboard R triggers reload using the new Input System Keyboard device.")]
        [SerializeField] private bool pollKeyboardR = true;
        [SerializeField] private float reloadSeconds = 1.6f;

        [Header("Rigging")]
        [Tooltip("If you use Animation Rigging (Rig Builder) for weapon/hand IK, prefer disabling only the hand IK rigs during reload (avoids weapon floating/snapping).")]
        [SerializeField] private bool disableSpecificRigsWhileReloading = true;

        [Tooltip("GameObject names of Animation Rigging components to fade out during reload. You can target a whole Rig (e.g., 'RigHand_IK') or a specific constraint (e.g., 'LeftHandIK').")]
        [SerializeField] private string[] rigsToDisableWhileReloading = new[] { "RigHand_IK" };

        [SerializeField] private float rigBlendSeconds = 0.08f;

        [Tooltip("Delay before releasing IK/rig weights after starting reload. Helps avoid a snap if the reload animation needs a short blend-in.")]
        [SerializeField] private float rigReleaseDelaySeconds = 0.06f;

        [Header("Animator")]
        [SerializeField] private string fireTrigger = "Fire";
        [SerializeField] private string reloadTrigger = "Reload";
        [Tooltip("Optional animator layer name to use for upper-body reload (e.g., 'Reloading'). If set and reloadLayer < 0, we auto-resolve the index.")]
        [SerializeField] private string reloadLayerName = "Reloading";
        [Tooltip("Animator layer index used for reload upper-body layer. Set to -1 to disable layer weight control.")]
        [SerializeField] private int reloadLayer = -1;
        [SerializeField] private float reloadLayerWeight = 1f;

        public bool IsFiring => isFiring;

        public bool IsReloadingWeapon => IsReloading();

        public Character ParentCharacter {  get; set; }

        private float _nextShootTime;

        private Coroutine _reloadRoutine;

        private float _reloadLayerPrevWeight;

        private int _fireTriggerHash;
        private int _reloadTriggerHash;

        private bool _hasFireTrigger;
        private bool _hasReloadTrigger;

        private readonly List<RigHandle> _rigsToDisable = new();

        private struct RigHandle
        {
            public Behaviour rig;
            public PropertyInfo weightProp;
            public FieldInfo weightField;
            public bool canSetWeight;
            public float prevWeight;
            public bool prevEnabled;
        }

        [SerializeField] private float debugDuration;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInParent<Animator>();

            CacheRigHandles();

            if (magazineSize < 1) magazineSize = 1;
            ammoInMagazine = Mathf.Clamp(ammoInMagazine, 0, magazineSize);
            ammoReserve = Mathf.Max(0, ammoReserve);

            ResolveReloadLayer();

            _fireTriggerHash = Animator.StringToHash(fireTrigger);
            _reloadTriggerHash = Animator.StringToHash(reloadTrigger);

            CacheAnimatorParameters();
        }

        private void CacheRigHandles()
        {
            _rigsToDisable.Clear();
            if (!disableSpecificRigsWhileReloading) return;

            // Avoid hard dependency on Unity.Animation.Rigging assembly; find Rig/Constraint components by namespace and a float weight (property or field).
            // IMPORTANT: rig constraints live under the rig hierarchy (children), not necessarily in parents.
            var behaviours = (animator != null)
                ? animator.GetComponentsInChildren<Behaviour>(true)
                : GetComponentsInParent<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;

                var t = b.GetType();
                if (t == null) continue;

                // Must be from Animation Rigging.
                string fullName = t.FullName ?? string.Empty;
                if (!fullName.Contains("UnityEngine.Animations.Rigging")) continue;

                if (!IsRigNameSelected(b.gameObject.name))
                    continue;

                // Try to access weight via property or field. Serialized constraints often store it as m_Weight.
                var weightProp = t.GetProperty("weight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                bool propOk = weightProp != null && weightProp.PropertyType == typeof(float) && weightProp.CanRead && weightProp.CanWrite;

                FieldInfo weightField = null;
                if (!propOk)
                {
                    weightField = t.GetField("weight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                 ?? t.GetField("m_Weight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                bool fieldOk = weightField != null && weightField.FieldType == typeof(float) && !weightField.IsInitOnly;

                _rigsToDisable.Add(new RigHandle
                {
                    rig = b,
                    weightProp = propOk ? weightProp : null,
                    weightField = fieldOk ? weightField : null,
                    canSetWeight = propOk || fieldOk,
                    prevWeight = 1f,
                    prevEnabled = b.enabled
                });
            }
        }

        private System.Collections.IEnumerator BlendRigWeightsToPrev(float seconds)
        {
            if (_rigsToDisable.Count == 0) yield break;

            float t = 0f;
            if (seconds <= 0f)
            {
                RestoreRigWeightsImmediate();
                yield break;
            }

            float[] start = new float[_rigsToDisable.Count];
            float[] target = new float[_rigsToDisable.Count];

            for (int i = 0; i < _rigsToDisable.Count; i++)
            {
                var h = _rigsToDisable[i];
                target[i] = h.prevWeight;
                if (h.rig == null) { start[i] = 0f; continue; }
                try { start[i] = (float)h.weightProp.GetValue(h.rig); }
                catch { start[i] = 0f; }
            }

            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / seconds);
                for (int i = 0; i < _rigsToDisable.Count; i++)
                {
                    var h = _rigsToDisable[i];
                    if (h.rig == null) continue;
                    float w = Mathf.Lerp(start[i], target[i], a);
                    try { h.weightProp.SetValue(h.rig, w); } catch { }
                }
                yield return null;
            }

            RestoreRigWeightsImmediate();
        }

        private bool IsRigNameSelected(string rigGameObjectName)
        {
            if (rigsToDisableWhileReloading == null || rigsToDisableWhileReloading.Length == 0) return true;
            for (int i = 0; i < rigsToDisableWhileReloading.Length; i++)
            {
                var n = rigsToDisableWhileReloading[i];
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (string.Equals(rigGameObjectName, n, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void ResolveReloadLayer()
        {
            if (animator == null) return;
            if (reloadLayer >= 0 && reloadLayer < animator.layerCount) return;
            if (string.IsNullOrWhiteSpace(reloadLayerName)) return;

            int idx = animator.GetLayerIndex(reloadLayerName);
            if (idx >= 0) reloadLayer = idx;
        }

        private void CacheAnimatorParameters()
        {
            _hasFireTrigger = false;
            _hasReloadTrigger = false;

            if (animator == null) return;

            foreach (var p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Trigger) continue;

                if (p.nameHash == _fireTriggerHash) _hasFireTrigger = true;
                if (p.nameHash == _reloadTriggerHash) _hasReloadTrigger = true;
            }
        }

        public void OnFire(InputAction.CallbackContext context)
        {
            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning))
            {
                // Ensure we don't get stuck in a firing state while the weapon is stored.
                isFiring = false;
                ParentCharacter.IsFiring = false;
                return;
            }

            if (context.started)
            {
                isFiring = true;
                if (ParentCharacter != null) ParentCharacter.IsFiring = true;
            }

            if (context.canceled)
            {
                isFiring = false;
                if (ParentCharacter != null) ParentCharacter.IsFiring = false;
            }
            if (!automatic && context.performed) TryShoot();

        }

        // Optional: if you later add an InputAction named "Reload" with PlayerInput SendMessages,
        // Unity will call OnReload automatically.
        public void OnReload(InputAction.CallbackContext context)
        {
            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning))
                return;

            // With PlayerInput "Invoke Unity Events" it's common to receive started/performed.
            // Accept both so the reload works regardless of button interaction settings.
            if (!context.started && !context.performed) return;
            TryStartReload();
        }

        public void CancelReload()
        {
            bool wasReloading = _reloadRoutine != null || (ParentCharacter != null && ParentCharacter.IsReloading);

            if (_reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
            }

            isFiring = false;
            if (ParentCharacter != null)
            {
                ParentCharacter.IsFiring = false;
                ParentCharacter.IsReloading = false;
            }

            RestoreRigWeightsImmediate();

            if (wasReloading && animator != null && reloadLayer >= 0 && reloadLayer < animator.layerCount)
                animator.SetLayerWeight(reloadLayer, _reloadLayerPrevWeight);
        }

        private void OnDisable()
        {
            // If disabled mid-reload, don't get stuck in a reloading state.
            if (_reloadRoutine != null)
            {
                StopCoroutine(_reloadRoutine);
                _reloadRoutine = null;
            }

            if (ParentCharacter != null) ParentCharacter.IsReloading = false;

            RestoreRigWeightsImmediate();

            if (animator != null && reloadLayer >= 0 && reloadLayer < animator.layerCount)
                animator.SetLayerWeight(reloadLayer, _reloadLayerPrevWeight);
        }

        private void CaptureRigPrevWeights()
        {
            for (int i = 0; i < _rigsToDisable.Count; i++)
            {
                var h = _rigsToDisable[i];
                if (h.rig == null) continue;
                try
                {
                    h.prevEnabled = h.rig.enabled;

                    if (h.canSetWeight)
                    {
                        if (h.weightProp != null) h.prevWeight = (float)h.weightProp.GetValue(h.rig);
                        else if (h.weightField != null) h.prevWeight = (float)h.weightField.GetValue(h.rig);
                    }
                    _rigsToDisable[i] = h;
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void RestoreRigWeightsImmediate()
        {
            for (int i = 0; i < _rigsToDisable.Count; i++)
            {
                var h = _rigsToDisable[i];
                if (h.rig == null) continue;
                try
                {
                    if (h.canSetWeight)
                    {
                        if (h.weightProp != null) h.weightProp.SetValue(h.rig, h.prevWeight);
                        else if (h.weightField != null) h.weightField.SetValue(h.rig, h.prevWeight);
                    }

                    h.rig.enabled = h.prevEnabled;
                }
                catch { }
            }
        }

        private System.Collections.IEnumerator BlendRigWeights(float targetWeight, float seconds)
        {
            if (_rigsToDisable.Count == 0) yield break;

            // Fallback: if a constraint doesn't expose weight, disable it while targetWeight==0.
            if (Mathf.Approximately(targetWeight, 0f))
            {
                for (int i = 0; i < _rigsToDisable.Count; i++)
                {
                    var h = _rigsToDisable[i];
                    if (h.rig == null) continue;
                    if (!h.canSetWeight) h.rig.enabled = false;
                }
            }

            float t = 0f;
            if (seconds <= 0f)
            {
                for (int i = 0; i < _rigsToDisable.Count; i++)
                {
                    var h = _rigsToDisable[i];
                    if (h.rig == null || !h.canSetWeight) continue;
                    try
                    {
                        if (h.weightProp != null) h.weightProp.SetValue(h.rig, targetWeight);
                        else if (h.weightField != null) h.weightField.SetValue(h.rig, targetWeight);
                    }
                    catch { }
                }
                yield break;
            }

            // Read start weights once.
            float[] start = new float[_rigsToDisable.Count];
            for (int i = 0; i < _rigsToDisable.Count; i++)
            {
                var h = _rigsToDisable[i];
                if (h.rig == null || !h.canSetWeight) { start[i] = 0f; continue; }
                try
                {
                    if (h.weightProp != null) start[i] = (float)h.weightProp.GetValue(h.rig);
                    else if (h.weightField != null) start[i] = (float)h.weightField.GetValue(h.rig);
                    else start[i] = 0f;
                }
                catch { start[i] = 0f; }
            }

            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / seconds);
                for (int i = 0; i < _rigsToDisable.Count; i++)
                {
                    var h = _rigsToDisable[i];
                    if (h.rig == null || !h.canSetWeight) continue;
                    float w = Mathf.Lerp(start[i], targetWeight, a);
                    try
                    {
                        if (h.weightProp != null) h.weightProp.SetValue(h.rig, w);
                        else if (h.weightField != null) h.weightField.SetValue(h.rig, w);
                    }
                    catch { }
                }
                yield return null;
            }

            for (int i = 0; i < _rigsToDisable.Count; i++)
            {
                var h = _rigsToDisable[i];
                if (h.rig == null || !h.canSetWeight) continue;
                try
                {
                    if (h.weightProp != null) h.weightProp.SetValue(h.rig, targetWeight);
                    else if (h.weightField != null) h.weightField.SetValue(h.rig, targetWeight);
                }
                catch { }
            }
        }

        private void Update()
        {
            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning))
            {
                // Weapon stored: block auto-reload and continuous fire.
                isFiring = false;
                ParentCharacter.IsFiring = false;
                return;
            }

            if (pollKeyboardR)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                    TryStartReload();
            }

            if (autoReloadWhenEmpty && !IsReloading() && ammoInMagazine <= 0 && ammoReserve > 0)
                TryStartReload();

            if (automatic && isFiring) TryShoot();

        }


        private void TryShoot()
        {
            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning)) return;
            if(requiereAim && (ParentCharacter == null || ! ParentCharacter.IsAiming)) return;
            if (IsReloading()) return;
            if(Time.time < _nextShootTime) return;

            if (ammoInMagazine <= 0)
            {
                if (autoReloadWhenEmpty)
                    TryStartReload();
                return;
            }

            ShootOnce();

            float interval = fireRate > 0f ? (1f / fireRate) : 0f;
            _nextShootTime = Time.time + interval;

        }

        private void ShootOnce()
        {
            ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);

            if (animator)
            {
                if (_hasFireTrigger) animator.SetTrigger(_fireTriggerHash);
                else animator.SetTrigger(fireTrigger);
            }
            if(recoil) recoil.Kick(camShake,camKick,camRecover);

            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 from = tracerOrigin ? tracerOrigin.position : ray.origin;

            if (Physics.Raycast(ray, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 to = hit.point;

                Debug.DrawRay(ray.origin, ray.direction * Vector3.Distance(ray.origin, to), Color.magenta,debugDuration);
                Debug.DrawLine(from,to, Color.yellow,debugDuration);
                Debug.DrawRay(to, hit.normal, Color.red, debugDuration);

                var info = new HitInfo
                {
                    point = hit.point,
                    normal = hit.normal,
                    damage = 10f
                };

                if (hit.collider.TryGetComponent<IHittable>(out var h))
                {
                    h.ApplyHit(info);
                }
                else
                {
                    var rb = hit.collider.attachedRigidbody;
                    if (rb && rb.TryGetComponent<IHittable>(out var hRb))
                    {
                        hRb.ApplyHit(info);
                    }
                    else
                    {
                        var hParent = hit.collider.GetComponentInParent<IHittable>();
                        if(hParent != null) hParent.ApplyHit(info);
                    }
                }
            }
            else
            {
                Vector3 to = ray.origin + ray.direction * range;
                Debug.DrawRay(ray.origin,ray.direction * range,Color.gray,debugDuration);
                Debug.DrawLine(from,to,Color.cyan,debugDuration);
            }
        }

        private bool IsReloading()
        {
            if (_reloadRoutine != null) return true;
            if (ParentCharacter != null && ParentCharacter.IsReloading) return true;
            return false;
        }

        private void TryStartReload()
        {
            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning)) return;
            if (IsReloading()) return;
            if (ammoReserve <= 0) return;
            if (ammoInMagazine >= magazineSize) return;

            ResolveReloadLayer();

            _reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            if (ParentCharacter != null) ParentCharacter.IsReloading = true;

            // 1) Start Animator reload first (so we blend into the reload pose before releasing IK).
            if (animator != null && reloadLayer >= 0 && reloadLayer < animator.layerCount)
            {
                _reloadLayerPrevWeight = animator.GetLayerWeight(reloadLayer);
                animator.SetLayerWeight(reloadLayer, reloadLayerWeight);
            }

            if (animator)
            {
                if (_hasReloadTrigger) animator.SetTrigger(_reloadTriggerHash);
                else animator.SetTrigger(reloadTrigger);
            }

            // 2) After a short blend-in, release only the selected IK rigs/constraints.
            if (rigReleaseDelaySeconds > 0f)
                yield return new WaitForSeconds(rigReleaseDelaySeconds);

            CaptureRigPrevWeights();
            yield return BlendRigWeights(0f, rigBlendSeconds);

            if (reloadSeconds > 0f)
                yield return new WaitForSeconds(reloadSeconds);

            CompleteReload();


            // 3) Return to normal upper-body first, then re-attach IK smoothly.
            if (animator != null && reloadLayer >= 0 && reloadLayer < animator.layerCount)
                animator.SetLayerWeight(reloadLayer, _reloadLayerPrevWeight);

            yield return BlendRigWeightsToPrev(rigBlendSeconds);

            if (ParentCharacter != null) ParentCharacter.IsReloading = false;

            _reloadRoutine = null;
        }

        private void CompleteReload()
        {
            int needed = Mathf.Max(0, magazineSize - ammoInMagazine);
            int toLoad = Mathf.Min(needed, ammoReserve);
            ammoInMagazine += toLoad;
            ammoReserve -= toLoad;
        }

    }

}