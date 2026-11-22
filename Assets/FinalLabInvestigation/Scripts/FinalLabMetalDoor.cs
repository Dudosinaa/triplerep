using UnityEngine;

namespace FinalLab.Investigation
{
    public sealed class FinalLabMetalDoor : MonoBehaviour
    {
        public const float UseDistance = 2.35f;

        [SerializeField] private Transform hinge;
        [SerializeField] private BoxCollider leaf;
        [SerializeField] private BoxCollider openInteractionArea;
        [SerializeField] private float swingAngle;
        [SerializeField] private float travelTime;
        [SerializeField] private bool locked;

        private readonly Collider[] blockers = new Collider[12];
        private float travel;
        private bool opening;

        public bool IsOpen => travel >= 1f;
        public bool IsMoving => travel != (opening ? 1f : 0f);
        public string Prompt => locked ? "Дверь заперта" : IsOpen ? "E — Закрыть дверь" : "E — Открыть дверь";
        public float TravelTime => travelTime;
        public float SwingAngle => swingAngle;
        public Transform Hinge => hinge;
        public BoxCollider Leaf => leaf;

        public void Configure(Transform pivot, BoxCollider movingCollider, BoxCollider openTarget, float angle, float duration)
        {
            hinge = pivot;
            leaf = movingCollider;
            openInteractionArea = openTarget;
            swingAngle = angle;
            travelTime = duration;
        }

        public bool OpenDoor()
        {
            if (locked) return false;
            opening = true;
            return true;
        }

        public void CloseDoor() => opening = false;

        public void SetLocked(bool value) => locked = value;

        public void Toggle()
        {
            if (locked || IsMoving) return;
            if (IsOpen) CloseDoor();
            else OpenDoor();
        }

        private void Update()
        {
            if (!IsMoving) return;
            float next = Mathf.MoveTowards(travel, opening ? 1f : 0f, Time.deltaTime / travelTime);
            float currentAngle = Mathf.SmoothStep(0, swingAngle, travel);
            float nextAngle = Mathf.SmoothStep(0, swingAngle, next);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(nextAngle - currentAngle) / 3f));
            for (int i = 1; i <= steps; i++)
                if (WouldTouchPlayer(Mathf.Lerp(currentAngle, nextAngle, i / (float)steps))) return;

            travel = next;
            hinge.localRotation = Quaternion.Euler(0, nextAngle, 0);
            if (openInteractionArea.enabled != IsOpen) openInteractionArea.enabled = IsOpen;
        }

        private bool WouldTouchPlayer(float angle)
        {
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 center = transform.TransformPoint(hinge.localPosition + rotation * leaf.center);
            Quaternion worldRotation = transform.rotation * rotation;
            Vector3 extents = leaf.size * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(center, extents, blockers, worldRotation,
                ~0, QueryTriggerInteraction.Ignore);
            var candidates = blockers;
            if (count == blockers.Length)
            {
                candidates = Physics.OverlapBox(center, extents, worldRotation, ~0, QueryTriggerInteraction.Ignore);
                count = candidates.Length;
            }
            for (int i = 0; i < count; i++)
                if (candidates[i] is CharacterController) return true;
            return false;
        }
    }
}
