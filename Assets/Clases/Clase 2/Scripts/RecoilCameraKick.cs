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
            perlins = new CinemachineBasicMultiChannelPerlin[_cameras.Length];
            baseAmplitud = new float[_cameras.Length];

            for (int i = 0; i < _cameras.Length; i++)
            {
                if (!_cameras[i]) continue;
                perlins[i] = _cameras[i].GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (perlins[i]) baseAmplitud[i] = perlins[i].AmplitudeGain;
            }
        }

        public void Kick(float strength, float peakDuration, float recoverDuration)
        {
            StopAllCoroutines();
            StartCoroutine(KickCoroutine(strength, peakDuration, recoverDuration));
        }

        IEnumerator KickCoroutine(float strength, float peak, float recover)
        {
            float t = 0f;

            while (t < peak)
            {
                t += Time.deltaTime;
                float K = t / Mathf.Max(0.0001f, peak);

                for (int i = 0; i < perlins.Length; i++)
                {
                    if (perlins[i]) perlins[i].AmplitudeGain = Mathf.Lerp(baseAmplitud[i], baseAmplitud[i] + strength, K);
                    yield return null; 

                }

                t = 0f;
                while(t < recover)
                {
                    t += Time.deltaTime;
                    float k = t / Mathf.Max(0.0001f, recover);
                    for (int i = 0; i < perlins.Length; i++)
                        if (perlins[i]) perlins[i].AmplitudeGain = Mathf.Lerp(baseAmplitud[i] + strength, baseAmplitud[i], k);
                    yield return null;
                }

                for (int i = 0;i < perlins.Length; i++) 
                     if (perlins[i])
                        perlins[i].AmplitudeGain = baseAmplitud[i];

            }

        }




    }


}