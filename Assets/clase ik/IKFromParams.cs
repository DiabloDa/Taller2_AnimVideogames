using UnityEngine;

[RequireComponent (typeof(Animator))]
public class IKFromParams : MonoBehaviour
{
    [Header("Targets")]
    public Transform rightHandTarget;
    public Transform rightElbowHint;
    public Transform lefttHandTarget;
    public Transform lefttElbowHint;
    public Transform lookTarget;

    public bool readFromAnimator = true;

    public string plook = "Look_IK";
    private string pRHPos = "RH_IK";
    private string pRHRot = "RH_IKRot";
    private string pRHHint = "RH_Hint";


    private string pLHPos = "LH_IK";
    /*private string pLHRot = "LH_IKRot";
    private string pLHHint = "LH_Hint";*/

    [Header("pesos manuales")]
    [Range(0, 1)] public float RHPos = 1, RHRot = 1, RHHint = 1, LH =1, Look =0.8f;

    Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator> ();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        float wLook = readFromAnimator ? animator.GetFloat(plook):Look;
        float wRHPos = readFromAnimator? animator.GetFloat(pRHPos): RHPos;
        float wRHHint = readFromAnimator ? animator.GetFloat(pRHHint) : RHHint;
        float wRHRot = readFromAnimator ? animator.GetFloat(pRHRot) : RHRot;
        float wLH = readFromAnimator ? animator.GetFloat(pLHPos) : LH;


        if(lookTarget)
        {
            animator.SetLookAtWeight(wLook);
            animator.SetLookAtPosition(lookTarget.position);
        }
        else
        {

            animator.SetLookAtWeight(0);

        }


        if(rightHandTarget)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, wRHPos);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, wRHRot);

            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,0);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand,0);

        }


        if (rightElbowHint)
        {

            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, wRHHint);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);

        }
        else animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
    }

}
