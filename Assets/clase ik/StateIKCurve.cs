using UnityEditor.Rendering;
using UnityEngine;

public class StateIKCurve : StateMachineBehaviour
{
    public AnimationCurve weight = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool affectLook = false;
    public bool affectRightHand = true;

    public override void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {

        var character = animator.GetComponentInParent<Clases.Clase_2.Scripts.Character>();
        if (character != null && character.IsReloading) return;

        if (animator.IsInTransition(layerIndex)) return;

        var ik = animator.GetComponent<IKFromParams>();
        if (!ik) return;

        float t = Mathf.Clamp01(stateInfo.normalizedTime);
        float w = Mathf.Clamp01(weight.Evaluate(t));

        if(affectRightHand && ik.rightHandTarget)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);

            animator.SetIKPosition(AvatarIKGoal.RightHand, ik.rightHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, ik.rightHandTarget.rotation);
        }

        if (ik.rightElbowHint)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, ik.rightElbowHint.position);

        }

        if(affectLook && ik.lookTarget)
        {
            animator.SetLookAtWeight(w);
            animator.SetLookAtPosition(ik.lookTarget.position);

        }

    }


    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);
        animator.SetLookAtWeight(0);
    }






}
