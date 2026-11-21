using UnityEngine;

namespace FinalLab.Investigation
{
    public sealed class DocumentInspectionCamera
    {
        private readonly Camera camera;
        private readonly Transform player;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private Vector3 localPosition;
        private Quaternion localRotation;
        private float initialFov;
        private Pose start, end;
        private float startFov, endFov, duration, elapsed;

        public DocumentInspectionCamera(Camera view, Transform playerRoot)
        {
            camera = view;
            player = playerRoot;
        }

        public Pose Current => new(camera.transform.position, camera.transform.rotation);
        public Pose GameplayPose => new(camera.transform.parent.TransformPoint(localPosition),
            camera.transform.parent.rotation * localRotation);
        public float GameplayFov => initialFov;

        public void Remember()
        {
            localPosition = camera.transform.localPosition;
            localRotation = camera.transform.localRotation;
            initialFov = camera.fieldOfView;
        }

        public void Restore()
        {
            camera.transform.localPosition = localPosition;
            camera.transform.localRotation = localRotation;
            camera.fieldOfView = initialFov;
        }

        public void Move(Pose target, float fieldOfView, float seconds)
        {
            start = Current;
            end = target;
            startFov = camera.fieldOfView;
            endFov = fieldOfView;
            duration = seconds;
            elapsed = 0;
        }

        public bool Advance(float delta, out float amount)
        {
            elapsed += delta;
            float progress = Mathf.Clamp01(elapsed / duration);
            amount = progress * progress * (3 - 2 * progress);
            Vector3 position = Vector3.LerpUnclamped(start.position, end.position, amount);
            camera.transform.SetPositionAndRotation(ClearPosition(camera.transform.position, position),
                Quaternion.SlerpUnclamped(start.rotation, end.rotation, amount));
            camera.fieldOfView = Mathf.LerpUnclamped(startFov, endFov, amount);
            return progress >= 1;
        }

        public bool CanReach(Vector3 from, Vector3 to) => (ClearPosition(from, to) - to).sqrMagnitude < 0.0004f;

        public void Zoom(float fieldOfView, float delta)
        {
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, fieldOfView, 1 - Mathf.Exp(-14 * delta));
        }

        private Vector3 ClearPosition(Vector3 from, Vector3 to)
        {
            Vector3 span = to - from;
            float distance = span.magnitude;
            if (distance < 0.0001f) return to;
            Vector3 direction = span / distance;
            var results = hits;
            int count = Physics.SphereCastNonAlloc(from, 0.075f, direction, results, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == results.Length)
            {
                results = Physics.SphereCastAll(from, 0.075f, direction, distance, ~0, QueryTriggerInteraction.Ignore);
                count = results.Length;
            }
            float closest = distance;
            for (int i = 0; i < count; i++)
                if (!results[i].collider.transform.IsChildOf(player))
                    closest = Mathf.Min(closest, Mathf.Max(0, results[i].distance - 0.012f));
            return from + direction * closest;
        }
    }
}
