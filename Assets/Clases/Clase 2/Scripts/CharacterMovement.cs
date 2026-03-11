using System;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Clases.Clase_2.Scripts
{
    public class CharacterMovement : MonoBehaviour ,ICharacterComponent
    {

        [SerializeField] private FloatDampener speedX;
        [SerializeField] private FloatDampener speedY;
        [SerializeField] private Camera camera;
        [SerializeField] private float angularSpeed;
        private Quaternion targetRotation;
        
        private int _speedXHash;
        private int _speedYHash;
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _speedXHash = Animator.StringToHash("SpeedX");
            _speedYHash = Animator.StringToHash("SpeedY");
        }

        private void SolveCharacterRotation()
        {
#if UNITY_EDITOR
            // Uncomment temporarily if you need to debug rotation solve.
            // Debug.Log("[CharacterMovement] Solve rotations");
#endif
            Vector3 floorNormal = transform.up;
            Vector3 cameraRealForward = camera.transform.forward;
            float angleInterpolator = Mathf.Abs(Vector3.Dot(cameraRealForward, floorNormal));
            Vector3 cameraForward = Vector3.Lerp(cameraRealForward, camera.transform.up, angleInterpolator).normalized;
            Vector3 characterForward = Vector3.ProjectOnPlane(cameraForward,floorNormal).normalized;
            Debug.DrawLine(transform.position, transform.position + characterForward*3, Color.green,5);
            targetRotation = Quaternion.LookRotation(characterForward,floorNormal);
        }

        public void OnMove(InputAction.CallbackContext ctx)
        {
            Vector2 inputValue = ctx.ReadValue<Vector2>();
            speedX.TargetValue = inputValue.x;
            speedY.TargetValue = inputValue.y;
        }
        private void Update()
        {
            speedX.Update();
            speedY.Update();
            _animator.SetFloat(_speedXHash,speedX.CurrentValue);
            _animator.SetFloat(_speedYHash,speedY.CurrentValue);
            SolveCharacterRotation();
            if (!ParentCharacter.IsAiming)
                ApplyCharacterRotation();
        }

        private void ApplyCharacterRotation()
        {
            float motionMagnitud = Mathf.Sqrt(speedX.TargetValue * speedX.TargetValue + speedY.TargetValue * speedY.TargetValue);
            float rotationSpeed = Mathf.SmoothStep(0, .01f, motionMagnitud);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,targetRotation,angularSpeed*rotationSpeed);
        }

        public Character ParentCharacter { get; set; }
    }
}