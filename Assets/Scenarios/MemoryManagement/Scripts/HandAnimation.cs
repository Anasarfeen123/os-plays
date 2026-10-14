using UnityEngine;
using UnityEngine.InputSystem;
public class HandAnimation : MonoBehaviour
{
    [SerializeField] private InputActionReference gripActionReference;
    [SerializeField] private InputActionReference triggerActionReference;

    private Animator animator;

    private string gripActionName = "Grip";

    private string triggerActionName = "Pinch";

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if(animator == null)
        {
            Debug.LogError("Animator Component not found on the GameObject.");
        }
    }

    
    void Update()
    {
        if (animator == null)
        {
            return;
        }

        float gripValue = gripActionReference.action.ReadValue<float>();
        float triggerValue = triggerActionReference.action.ReadValue<float>();

        animator.SetFloat(gripActionName, gripValue);
        animator.SetFloat(triggerActionName, triggerValue);
    }
}
