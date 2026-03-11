using System;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Clases.Clase_2.Scripts
{
    [Serializable]
    public struct FloatDampener
    {
        [SerializeField] private float _smoothTime;
        private float currentVelocity;
        public float TargetValue { get; set; }
        public float CurrentValue { get; private set;}

        public void Update()
        {
            CurrentValue = Mathf.SmoothDamp(CurrentValue, TargetValue, ref currentVelocity, _smoothTime);
        }
    }
}