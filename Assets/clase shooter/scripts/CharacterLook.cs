using System;
using Clases.Clase_2.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;



namespace Actividad2
{
    public class CharacterLook : MonoBehaviour, ICharacterComponent
    {
        [SerializeField] private Transform target;

        [SerializeField] private FloatDampener horizontalDampener;
        [SerializeField] private FloatDampener verticalDampener;

        [SerializeField] private float horizontalRotationSpeed;
        [SerializeField] private float verticalRotationSpeed;
        [SerializeField] private Vector2 verticalRotationLimits;

        private float verticalRotation;

        public void OnLook(InputAction.CallbackContext ctx)
        {
            Vector2 inputValue = ctx.ReadValue<Vector2>();

            inputValue = inputValue / new Vector2(Screen.width, Screen.height);

            horizontalDampener.TargetValue = inputValue.x;
            verticalDampener.TargetValue = inputValue.y;
        }

        private void ApplyLookRotation()
        {
            if(target == null)
            {
                throw new NullReferenceException("look target is null");

            }

            target.RotateAround(target.position, transform.up, horizontalDampener.CurrentValue * horizontalRotationSpeed * 360 * Time.deltaTime);
            verticalRotation += verticalDampener.CurrentValue * verticalRotationSpeed * 360 * Time.deltaTime;
            verticalRotation = Mathf.Clamp(verticalRotation, verticalRotationLimits.x, verticalRotationLimits.y );

            Vector3 euler = target.localEulerAngles;
            euler.x = verticalRotation;
            target.localEulerAngles = euler;
        }

        private void Update()
        {
            horizontalDampener.Update();
            verticalDampener.Update();

            ApplyLookRotation();
        }

        public Character ParentCharacter { get; set; }
    }
}