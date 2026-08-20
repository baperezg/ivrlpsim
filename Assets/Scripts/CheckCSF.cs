using UnityEngine;

public class CheckCSF : MonoBehaviour
{
    // Reference to the Animator component
    public Animator animator;
    public bool checking = false;

    public void moveForward()
    {
        animator.SetBool("CheckCSF", true);
        animator.SetBool("ViewCSF", false);
        checking = false;
}
    public void moveBackward()
    {
        animator.SetBool("CheckCSF", false);
        animator.SetBool("ViewCSF", false);
        checking = false;
    }
    public void checkCSF()
    {        
        animator.SetBool("ViewCSF", true);
        checking = true;
    }
}
