using UnityEngine;

internal static class ShotRaycast
{
    public static bool TryGetFirstHit(Ray ray, float range, int layers, Transform shooter,
        RaycastHit[] buffer, out RaycastHit closest, bool includeNpcHitboxes = false)
    {
        var triggers = includeNpcHitboxes ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        int count = Physics.RaycastNonAlloc(ray, buffer, range, layers, triggers);
        if (count == buffer.Length)
        {
            buffer = Physics.RaycastAll(ray, range, layers, triggers);
            count = buffer.Length;
        }

        closest = default;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = buffer[i];
            if (hit.distance >= distance || hit.collider.transform.IsChildOf(shooter)) continue;
            if (hit.collider.isTrigger &&
                hit.collider.GetComponentInParent<HostileTrainNpc>()?.IsShotHitbox(hit.collider) != true) continue;
            closest = hit;
            distance = hit.distance;
        }
        return closest.collider != null;
    }
}
