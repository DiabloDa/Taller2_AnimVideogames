using Clases.Clase_2.Scripts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;



namespace Actividad2
{


    public class CharacterMovement : MonoBehaviour, ICharacterComponent
    {

        [SerializeField] private FloatDampener speedX;
        [SerializeField] private FloatDampener speedY;

        [SerializeField] private Camera camara;

        private Quaternion targetRotation;

        private int _speedXHash;
        private int _speedYHash;

        private Animator _animator;

        private void Awake()
        {  
            _animator = GetComponent<Animator>();
            _speedXHash = Animator.StringToHash(name:"SpeedX");
            _speedYHash = Animator.StringToHash(name: "SpeedY");

        }

        private void SolveCharacterRotation()
        {
            Vector3 floorNormal = transform.up;
            Vector3 cameraRealFoward = camara.transform.forward;

            float angleInterpolator = Mathf.Abs(Vector3.Dot(cameraRealFoward, floorNormal));
            Vector3 cameraFoward = Vector3.Lerp(cameraRealFoward, camara.transform.up, angleInterpolator).normalized;

            Vector3 characterForward = Vector3.ProjectOnPlane(cameraFoward, floorNormal).normalized;
            Debug.DrawLine(transform.position, transform.position + characterForward*3, Color.green, duration:5);

            targetRotation = quaternion.LookRotation(characterForward, floorNormal);    

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
            _animator.SetFloat(_speedXHash, speedX .CurrentValue);
            _animator.SetFloat(_speedYHash, speedY.CurrentValue);
        }


        public Character ParentCharacter { get; set; }
    }

}