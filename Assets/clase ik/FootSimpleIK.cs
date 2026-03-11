using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class FootSimpleIK : MonoBehaviour
{
    public LayerMask groundMask = ~0;
    public float rayAbove = 0.6f, rayBelow = 1.0f;
    public float footOffset = 0.03f;
    [Range(0, 1)] public float footWeight;

    Animator animator;
    private Transform hips;
    private float pelvisOffset;
    private float pelvisVel;

    [Header("Pelvis")]
    public bool movePelvis = true;
    [Range(0, 1)] public float pelvisAdjust = 0.7f;
    public float pelvisSmooth = 0.08f;
    public float pelvisClamp = 0.2f;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!animator) return;

        Vector3 lPose = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 rPose = animator.GetIKPosition(AvatarIKGoal.RightFoot);

        bool lHit = Physics.Raycast(lPose + Vector3.up * rayAbove, Vector3.down
            , out var l, rayAbove + rayBelow,
            groundMask, QueryTriggerInteraction.Ignore);

        bool rHit = Physics.Raycast(rPose + Vector3.up * rayAbove, Vector3.down, out var r,
            rayAbove + rayBelow, groundMask, QueryTriggerInteraction.Ignore);

        Vector3 lGoalPose = lHit ? l.point + Vector3.up * footOffset : lPose;
        Vector3 rGoalPose = rHit ? r.point + Vector3.up * footOffset : rPose;

        Quaternion lGoalRot = lHit ?
            Quaternion.FromToRotation(Vector3.up, l.normal) * animator.GetIKRotation(AvatarIKGoal.LeftFoot) :
            animator.GetIKRotation(AvatarIKGoal.LeftFoot);
        Quaternion rGoalRot = rHit ?
            Quaternion.FromToRotation(Vector3.up, r.normal) * animator.GetIKRotation(AvatarIKGoal.RightFoot) :
            animator.GetIKRotation(AvatarIKGoal.RightFoot);


        if (movePelvis && hips)
        {
            float ldelta = lGoalPose.y - lPose.y;
            float rdelta = rGoalPose.y - rPose.y;

            float target = (ldelta < 0f || rdelta < 0f) ? Mathf.Min(ldelta, rdelta) : Mathf.Max(ldelta, rdelta);
            target = Mathf.Clamp(target * pelvisAdjust, -pelvisClamp, pelvisClamp);

            float newOffset = Mathf.SmoothDamp(pelvisOffset, target, ref pelvisVel, pelvisSmooth);
            float delta = newOffset - pelvisOffset;
            pelvisOffset = newOffset;

            hips.position += Vector3.up * delta;
        }

        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footWeight);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, lGoalPose);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, lGoalRot);

        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, footWeight);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rGoalPose);
        animator.SetIKRotation(AvatarIKGoal.RightFoot, rGoalRot);
    }
}
