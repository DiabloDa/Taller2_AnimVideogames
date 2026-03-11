using UnityEngine;

public class CharacterAnimatorController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterInputFactory.InputType inputType = CharacterInputFactory.InputType.Player;

    private ICharacterInput _input;
    private CharacterAnimator _CharacterAnimator;
    private void Awake()
    {
        _input = CharacterInputFactory.CreateInput(inputType);
        _CharacterAnimator = new CharacterAnimator(animator);
    }

    // Update is called once per frame
    private void Update()
    {
        float speed = _input.GetSpeedInput();
        _CharacterAnimator.UpdateSpeed(speed);
    }
}
