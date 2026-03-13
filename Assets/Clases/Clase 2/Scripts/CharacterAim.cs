using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace Clases.Clase_2.Scripts
{
    public class CharacterAim : MonoBehaviour, ICharacterComponent
    {
        public Character ParentCharacter { get; set; }
        
        [SerializeField] private CinemachineCamera aimCamera;
        [SerializeField] private FloatDampener aimDampener;
        [SerializeField] private AimConstraint aimConstraint;

        [Header("Rigging")]
        [Tooltip("Rig de Animation Rigging que controla el apuntado del arma (por ejemplo: RigLayer_WeaponAim).")]
        [SerializeField] private Rig weaponAimRig;
        
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            SetWeaponAimRigActive(false);
        }

        private void OnDisable()
        {
            // Safety: if the component is disabled mid-aim, ensure cameras/rigs are reset.
            ForceStopAiming();
        }

        public void ForceStopAiming()
        {
            aimCamera?.gameObject.SetActive(false);
            if (ParentCharacter != null) ParentCharacter.IsAiming = false;
            aimDampener.TargetValue = 0f;
            SetWeaponAimRigActive(false);
        }

        public void OnAim(InputAction.CallbackContext ctx)
        {
            if(!ctx.started && !ctx.canceled) return;

            if (ParentCharacter != null && (ParentCharacter.IsWeaponHolstered || ParentCharacter.IsWeaponTransitioning))
            {
                // If stored, ignore aim input and ensure we are not aiming.
                if (ctx.started) ForceStopAiming();
                return;
            }
            
            aimCamera?.gameObject.SetActive(ctx.started);
            if (ParentCharacter != null) ParentCharacter.IsAiming = ctx.started;
            //aimConstraint.enabled = ctx.started;
            aimDampener.TargetValue = ctx.started ? 1 : 0;

            SetWeaponAimRigActive(ctx.started);
        }

        private void SetWeaponAimRigActive(bool isAiming)
        {
            if (weaponAimRig == null) return;

            weaponAimRig.weight = isAiming ? 1f : 0f;
            weaponAimRig.enabled = isAiming;
        }

        private void Update()
        {
            aimDampener.Update();
           // aimConstraint.weight = aimDampener.CurrentValue;
            if (animator != null && animator.layerCount > 1)
                animator.SetLayerWeight(1, aimDampener.CurrentValue);
        }
    }
}