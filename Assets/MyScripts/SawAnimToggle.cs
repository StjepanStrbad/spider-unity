using UnityEngine;

public class SawAnimToggle : MonoBehaviour
{
    Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        animator.SetBool("isSpinning", true);
    }
}