using UnityEngine;

internal static class ShotRaycast
{
    public static bool TryGetFirstHit(Ray ray, float range, int layers, Transform shooter,
        RaycastHit[] buffer, out RaycastHit closest)
    {
        int count = Physics.RaycastNonAlloc(ray, buffer, range, layers, QueryTriggerInteraction.Ignore);
        if (count == buffer.Length)
        {
            buffer = Physics.RaycastAll(ray, range, layers, QueryTriggerInteraction.Ignore);
            count = buffer.Length;
        }

        closest = default;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = buffer[i];
            if (hit.distance >= distance || hit.collider.transform.IsChildOf(shooter)) continue;
            closest = hit;
            distance = hit.distance;
        }
        return closest.collider != null;
    }
}
