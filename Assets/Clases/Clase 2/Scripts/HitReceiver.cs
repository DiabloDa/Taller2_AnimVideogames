using Clases.Clase_2.Scripts;
using UnityEngine;

public class HitReceiver : MonoBehaviour, IHittable
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string hitTriggerName = "Hit";
    public void ApplyHit(HitInfo info)
    {
        if(_animator) _animator.SetTrigger(hitTriggerName);
    }

    private void Reset()
    {
        _animator = GetComponent<Animator>();
    }





}
