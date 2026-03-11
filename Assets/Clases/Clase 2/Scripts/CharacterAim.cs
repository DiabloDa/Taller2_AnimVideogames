using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

namespace Clases.Clase_2.Scripts
{
    public class CharacterAim : MonoBehaviour, ICharacterComponent
    {
        public Character ParentCharacter { get; set; }
        
        [SerializeField] private CinemachineCamera aimCamera;
        [SerializeField] private FloatDampener aimDampener;
        [SerializeField] private AimConstraint aimConstraint;
        
        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void OnAim(InputAction.CallbackContext ctx)
        {
            if(!ctx.started && !ctx.canceled) return;
            
            aimCamera?.gameObject.SetActive(ctx.started);
            ParentCharacter.IsAiming = ctx.started;
            aimConstraint.enabled = ctx.started;
            aimDampener.TargetValue = ctx.started ? 1 : 0;
        }

        private void Update()
        {
            aimDampener.Update();
            aimConstraint.weight = aimDampener.CurrentValue;
            animator.SetLayerWeight(1,aimDampener.CurrentValue);
        }
    }
}