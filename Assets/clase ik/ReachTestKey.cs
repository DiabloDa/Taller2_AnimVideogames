using UnityEngine;
using System;

public class ReachTestKey : MonoBehaviour
{
   
    public Animator animator;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            animator.SetTrigger("reach");
        }
    }


}
