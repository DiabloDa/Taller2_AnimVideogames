using UnityEngine;

public class CharacterAnimator
{

    private readonly Animator _animator;
    private readonly int _speedHash = Animator.StringToHash(name: "Speed");

    public CharacterAnimator(Animator animator)
    {
        _animator = animator;
    }

    public void UpdateSpeed(float speed)
    {
        _animator.SetFloat(_speedHash, speed);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
   /* void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }*/
}
