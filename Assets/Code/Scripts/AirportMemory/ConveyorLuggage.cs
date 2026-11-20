using UnityEngine;

// Loops bags along the belt so motion is visible as soon as Play starts.
public sealed class ConveyorLuggage : MonoBehaviour
{
    [SerializeField] private Vector3 start;
    [SerializeField] private Vector3 end;
    [SerializeField, Min(.1f)] private float metresPerSecond = 1.35f;
    [SerializeField, Range(0f, 1f)] private float phase;

    public void Configure(Vector3 from, Vector3 to, float speed, float initialPhase)
    {
        start = from;
        end = to;
        metresPerSecond = speed;
        phase = initialPhase;
        transform.localPosition = Vector3.Lerp(start, end, phase);
    }

    private void Update()
    {
        float distance = Vector3.Distance(start, end);
        if (distance < .01f) return;
        float progress = Mathf.Repeat(phase + Time.time * metresPerSecond / distance, 1f);
        transform.localPosition = Vector3.Lerp(start, end, progress);
    }
}
