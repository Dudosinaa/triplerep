using UnityEngine;

public sealed class NpcTalkAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;

    public void Configure(Animator target) => animator = target;

    private NpcDialogue dialogue;

    private void OnEnable()
    {
        dialogue = GetComponent<NpcDialogue>();
        if (dialogue == null) return;
        dialogue.ConversationStarted += StartTalking;
        dialogue.ConversationCompleted += FinishTalking;
        dialogue.ConversationCancelled += StopTalking;
    }

    private void OnDisable()
    {
        if (dialogue == null) return;
        dialogue.ConversationStarted -= StartTalking;
        dialogue.ConversationCompleted -= FinishTalking;
        dialogue.ConversationCancelled -= StopTalking;
        StopTalking();
    }

    private void StartTalking() => SetTalking(true);
    private void FinishTalking(FirstPersonPlayer _) => StopTalking();
    private void StopTalking() => SetTalking(false);

    public void SetTalking(bool talking)
    {
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetBool("Talking", talking);
    }
}
