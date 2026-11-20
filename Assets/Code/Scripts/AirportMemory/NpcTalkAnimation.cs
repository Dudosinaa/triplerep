using UnityEngine;

public sealed class NpcTalkAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    public void Configure(Animator target) => animator = target;

    public void SetTalking(bool talking)
    {
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetBool("Talking", talking);
    }
}
