using UnityEngine;

public sealed class IvanFinaleControl : MonoBehaviour
{
    public enum Action { Latch, Breaker, Switch, Hold, Cover, Destroy, Rescue, Exit }
    public Action Kind;
    public int Index;
    public string Prompt;
    public Transform MovingPart;
    private Quaternion initial;
    private void Awake() { if (MovingPart != null) initial = MovingPart.localRotation; }
    public void Move(float angle)
    {
        if (MovingPart != null) MovingPart.localRotation = initial * Quaternion.Euler(angle, 0, 0);
    }
}
