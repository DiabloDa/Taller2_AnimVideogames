using System.Collections;
using Unity.Cinemachine;
using UnityEngine;


namespace Clases.Clase_2.Scripts
{

    public class RecoilCameraKick : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera[] _cameras;
        private CinemachineBasicMultiChannelPerlin[] perlins;
        private float[] baseAmplitud;

        private void Awake()
        {
            RebuildCacheAndCaptureBaseAmplitude();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                RebuildCacheAndCaptureBaseAmplitude();
        }

        private void RebuildCacheAndCaptureBaseAmplitude()
        {
            int len = _cameras == null ? 0 : _cameras.Length;
            perlins = new CinemachineBasicMultiChannelPerlin[len];
            baseAmplitud = new float[len];

            for (int i = 0; i < len; i++)
            {
                var cam = _cameras[i];
                if (!cam) continue;

                // In Cinemachine 3, pipeline components can be retrieved via the pipeline API.
                var perlin = cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (perlin == null)
                    perlin = cam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

                perlins[i] = perlin;
                if (perlin != null) baseAmplitud[i] = perlin.AmplitudeGain;
            }
        }

        public void Kick(float strength, float peakDuration, float recoverDuration)
        {
            if (_cameras == null || _cameras.Length == 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[{nameof(RecoilCameraKick)}] No hay cámaras asignadas en '_cameras'.", this);
#endif
                return;
            }

            if (perlins == null || baseAmplitud == null || perlins.Length != _cameras.Length)
                RebuildCacheAndCaptureBaseAmplitude();

            bool anyPerlin = false;
            bool anyMissingProfile = false;
            for (int i = 0; i < perlins.Length; i++)
            {
                if (perlins[i] != null)
                {
                    anyPerlin = true;
                    baseAmplitud[i] = perlins[i].AmplitudeGain;
                    if (perlins[i].NoiseProfile == null) anyMissingProfile = true;
                }
            }

            if (!anyPerlin)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[{nameof(RecoilCameraKick)}] No se encontró 'CinemachineBasicMultiChannelPerlin' (Noise) en las cámaras asignadas. Agrega el componente de Noise a la CinemachineCamera.", this);
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (anyMissingProfile)
                Debug.LogWarning($"[{nameof(RecoilCameraKick)}] Hay cámaras con Perlin pero sin 'NoiseProfile'. Sin perfil, no habrá shake visible.", this);
#endif

            StopAllCoroutines();
            StartCoroutine(KickCoroutine(strength, peakDuration, recoverDuration));
        }

        IEnumerator KickCoroutine(float strength, float peak, float recover)
        {
            peak = Mathf.Max(0.0001f, peak);
            recover = Mathf.Max(0.0001f, recover);

            float t = 0f;
            while (t < peak)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / peak);
                for (int i = 0; i < perlins.Length; i++)
                    if (perlins[i]) perlins[i].AmplitudeGain = Mathf.Lerp(baseAmplitud[i], baseAmplitud[i] + strength, k);
                yield return null;
            }

            t = 0f;
            while (t < recover)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / recover);
                for (int i = 0; i < perlins.Length; i++)
                    if (perlins[i]) perlins[i].AmplitudeGain = Mathf.Lerp(baseAmplitud[i] + strength, baseAmplitud[i], k);
                yield return null;
            }

            for (int i = 0; i < perlins.Length; i++)
                if (perlins[i])
                    perlins[i].AmplitudeGain = baseAmplitud[i];

        }




    }


}