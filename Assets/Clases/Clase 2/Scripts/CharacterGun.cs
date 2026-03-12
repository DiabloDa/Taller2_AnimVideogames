using Clases.Clase_2.Scripts;
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

        public bool IsFiring => isFiring;

        public Character ParentCharacter {  get; set; }

        private float _nextShootTime;

        [SerializeField] private float debugDuration;

        public void OnFire(InputAction.CallbackContext context)
        {
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

        private void Update()
        {
            if (automatic && isFiring) TryShoot();

        }


        private void TryShoot()
        {
            if(requiereAim && (ParentCharacter == null || ! ParentCharacter.IsAiming)) return;
            if(Time.time < _nextShootTime) return;

            ShootOnce();

            float interval = fireRate > 0f ? (1f / fireRate) : 0f;
            _nextShootTime = Time.time + interval;

        }

        private void ShootOnce()
        {
            if(animator) animator.SetTrigger("Fire");
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

    }

}