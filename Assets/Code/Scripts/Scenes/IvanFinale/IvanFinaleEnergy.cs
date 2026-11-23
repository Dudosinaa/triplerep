using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class IvanFinaleEnergy : MonoBehaviour
{
    public Transform Alexei, Core;
    public void SetProgress(float progress, bool moving)
    {
        var line = GetComponent<LineRenderer>();
        line.enabled = progress < 1;
        line.positionCount = 25;
        for (int i = 0; i < 25; i++)
        {
            float t = i / 24f;
            var at = Vector3.Lerp(Alexei.position, Core.position, t);
            at.y += Mathf.Sin(t * Mathf.PI) * .5f + Mathf.Sin(t * 40 - Time.time * 8) * .06f;
            line.SetPosition(i, at);
        }
        line.widthMultiplier = moving ? .07f : .025f;
    }
}
