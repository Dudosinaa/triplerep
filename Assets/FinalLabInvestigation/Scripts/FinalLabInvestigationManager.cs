using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FinalLab.Investigation
{
    [DefaultExecutionOrder(30)]
    public sealed class FinalLabInvestigationManager : MonoBehaviour
    {
        public enum ViewState { Walking, Moving, Overview, Detail }
        private const string ArchiveId = "Archive";
        private const string FatherId = "Father";
        private const string IvanId = "Ivan";
        private static readonly string[] RequiredArchiveIds = { "А1", "А2", "А3", "А4" };
        private static readonly string[] RequiredFatherIds = { "О1", "О2", "О3" };
        private static readonly string[] RequiredIvanIds = { "И1", "И2", "И3" };

        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private Camera view;
        [SerializeField] private FinalLabInvestigationUI ui;
        [SerializeField] private InvestigationPoint[] points;

        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly HashSet<string> archiveViewed = new(StringComparer.Ordinal);
        private readonly HashSet<string> fatherViewed = new(StringComparer.Ordinal);
        private readonly HashSet<string> ivanViewed = new(StringComparer.Ordinal);
        private DocumentInspectionCamera inspection;
        private InvestigationProgress progress;
        private InvestigationPoint active;
        private InvestigationDocument selected, hovered;
        private ViewState state;
        private Action arrived;
        private bool keyOpened, reactionShown, hinted, playerWasEnabled;
        private float lastAction, zoomTarget, detailFov;
        private CursorLockMode oldCursor;
        private bool oldCursorVisible;
        private Pose documentHome, deskDocumentHome, paperFrom, paperTo;
        private Transform movingPaper;

        public ViewState State => state;
        public InvestigationPoint ActivePoint => active;
        public InvestigationPoint ObjectivePoint => progress != null && progress.Count < points.Length ? points[progress.Count] : null;
        public InvestigationDocument Selected => selected;
        public int Completed => progress?.Count ?? 0;
        public int ArchiveViewedCount => archiveViewed.Count;
        public int FatherViewedCount => fatherViewed.Count;
        public int IvanViewedCount => ivanViewed.Count;
        public InvestigationPoint[] Points => points;
        public event Action InvestigationCompleted;
        public event Action IvanDeskClosedAfterInvestigation;
        private bool AtArchive => active != null && active.Id == ArchiveId;
        private bool AtFather => active != null && active.Id == FatherId;
        private bool AtIvan => active != null && active.Id == IvanId;
        private bool AtDesk => AtFather || AtIvan;
        private bool RequiresAllDocuments => AtArchive || AtDesk;
        private HashSet<string> ViewedDocuments => AtFather ? fatherViewed : AtIvan ? ivanViewed : archiveViewed;
        private string[] RequiredDocuments => AtFather ? RequiredFatherIds : AtIvan ? RequiredIvanIds : RequiredArchiveIds;
        private int ViewedCount => ViewedDocuments.Count;
        private int RequiredCount => RequiredDocuments.Length;

        public void Configure(FirstPersonPlayer motor, Camera camera, FinalLabInvestigationUI display,
            InvestigationPoint[] orderedPoints)
        {
            player = motor;
            view = camera;
            ui = display;
            points = orderedPoints;
        }

        private void Start()
        {
            inspection = new DocumentInspectionCamera(view, player.transform);
            progress = new InvestigationProgress(points.Length);
            lastAction = Time.unscaledTime;
            UpdateObjective(true);
        }

        private void Update()
        {
            if (GameplayPause.BlocksGameplayInput) return;
            if (progress == null) return;
            if (state == ViewState.Moving)
            {
                bool ended = inspection.Advance(Time.unscaledDeltaTime, out float amount);
                if (movingPaper != null)
                    movingPaper.SetPositionAndRotation(Vector3.LerpUnclamped(paperFrom.position, paperTo.position, amount),
                        Quaternion.SlerpUnclamped(paperFrom.rotation, paperTo.rotation, amount));
                if (ended)
                {
                    movingPaper = null;
                    var callback = arrived;
                    arrived = null;
                    callback?.Invoke();
                }
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (state == ViewState.Walking)
            {
                var eye = inspection.Current;
                if (player.enabled && NearestHit(new Ray(eye.position, eye.forward), FinalLabMetalDoor.UseDistance, out var doorHit) &&
                    doorHit.collider.GetComponentInParent<FinalLabMetalDoor>() is { } door)
                {
                    ui.SetPrompt(door.Prompt);
                    if (keyboard != null && keyboard.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
                        door.Toggle();
                    return;
                }
                var point = NearbyPoint();
                if (!IsUnlocked(point)) point = null;
                ui.SetPrompt(point != null ? point.Prompt : "");
                if (point != null && keyboard != null && keyboard.eKey.wasPressedThisFrame && InteractionInput.TryConsume())
                    TryOpen(point);
                if (active == null && !hinted && ObjectivePoint != null && Time.unscaledTime - lastAction >= 20f)
                {
                    ui.Hint(ObjectivePoint.Hint);
                    hinted = true;
                }
                return;
            }
            bool back = InteractionInput.TryConsumeCancel();
            if (state == ViewState.Overview)
            {
                var candidate = mouse != null ? DocumentAt(view.ScreenPointToRay(mouse.position.ReadValue())) : null;
                Highlight(candidate);
                if (back) Close(fromEscape: true);
                else if (click && candidate != null) TrySelect(candidate);
                return;
            }
            if (back || click)
            {
                Close();
                return;
            }
            if (RequiresAllDocuments) return;
            if (mouse != null) Scroll(mouse.scroll.ReadValue().y / 120f);
            inspection.Zoom(zoomTarget, Time.unscaledDeltaTime);
        }

        public InvestigationPoint NearbyPoint()
        {
            if (inspection == null || !player.enabled) return null;
            Pose eye = inspection.Current;
            Ray look = new(eye.position, eye.forward);
            if (NearestHit(look, 3.2f, out var aimed))
                foreach (var point in points)
                    if (aimed.distance <= point.Range && point.Owns(aimed.collider)) return point;

            InvestigationPoint best = null;
            float bestScore = 0.57f;
            foreach (var point in points)
            {
                Vector3 offset = point.Focus.position - eye.position;
                float distance = offset.magnitude;
                if (distance > point.Range || distance < 0.02f) continue;
                float score = Vector3.Dot(eye.forward, offset / distance) - distance * 0.04f;
                if (score <= bestScore) continue;
                if (NearestHit(new Ray(eye.position, offset / distance), distance, out var blocker) &&
                    !point.Owns(blocker.collider)) continue;
                best = point;
                bestScore = score;
            }
            return best;
        }

        public InvestigationDocument DocumentAt(Ray ray)
        {
            if (active == null || !NearestHit(ray, 5f, out var hit)) return null;
            var item = hit.collider.GetComponentInParent<InvestigationDocument>();
            return item != null && active.Owns(item) ? item : null;
        }

        public bool TryOpen(InvestigationPoint point)
        {
            if (state != ViewState.Walking || inspection == null || !player.enabled ||
                !IsUnlocked(point) || NearbyPoint() != point ||
                !inspection.CanReach(view.transform.position, point.Overview.position)) return false;
            active = point;
            keyOpened = false;
            reactionShown = progress.IsStudied(Array.IndexOf(points, point));
            playerWasEnabled = player.enabled;
            oldCursor = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            inspection.Remember();
            player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            lastAction = Time.unscaledTime;
            ui.ClearHint();
            ui.SetPrompt("");
            Move(OverviewPose(), OverviewFov(), AtDesk ? 0.66f : 0.53f, ShowOverview);
            return true;
        }

        private bool IsUnlocked(InvestigationPoint point)
        {
            if (point == null || progress == null) return false;
            int index = Array.IndexOf(points, point);
            return index >= 0 && index <= progress.Count;
        }

        public bool TrySelect(InvestigationDocument item)
        {
            if (state != ViewState.Overview || item == null || !active.Owns(item)) return false;
            Pose from = inspection.Current;
            float dist = Mathf.Max(0.41f, item.Size.y * 1.33f, item.Size.x * 1.33f / Mathf.Max(0.70f, view.aspect));
            const float focusFov = 48;
            if (RequiresAllDocuments)
            {
                float verticalSpan = 2 * Mathf.Tan(focusFov * 0.5f * Mathf.Deg2Rad);
                dist = Mathf.Max(0.36f, item.Size.y / (verticalSpan * 0.78f),
                    item.Size.x / (verticalSpan * Mathf.Max(0.3f, view.aspect) * 0.83f));
            }
            Pose cameraTarget = from;
            if (!item.Lifts)
            {
                Vector3 front = -item.transform.forward;
                cameraTarget = new Pose(item.transform.position + front * dist,
                    Quaternion.LookRotation(-front, item.transform.up));
                if (!inspection.CanReach(from.position, cameraTarget.position)) return false;
            }
            Highlight(null);
            if (RequiresAllDocuments)
            {
                active.SetHighlighted(false);
                if (AtDesk) ui.ShowDeskDetail(item.Kind);
                else ui.ShowArchiveDetail();
            }
            selected = item;
            documentHome = new Pose(item.transform.position, item.transform.rotation);
            if (AtDesk) deskDocumentHome = new Pose(item.transform.localPosition, item.transform.localRotation);
            if (item.Lifts) AnimatePaper(item.transform, new Pose(from.position + from.forward * dist, from.rotation));
            detailFov = focusFov;
            Move(cameraTarget, detailFov, item.Lifts ? 0.49f : 0.52f, () =>
            {
                if (AtArchive && (inspection.Current.position - cameraTarget.position).sqrMagnitude > 0.0004f)
                {
                    RestorePaper();
                    Move(OverviewPose(), OverviewFov(), 0.46f, ShowOverview);
                    return;
                }
                zoomTarget = detailFov;
                if (item.Id == active.KeyId) keyOpened = true;
                if (RequiresAllDocuments && Array.IndexOf(RequiredDocuments, item.Id) >= 0)
                    ViewedDocuments.Add(item.Id);
                state = ViewState.Detail;
                lastAction = Time.unscaledTime;
                if (!RequiresAllDocuments)
                    ui.SetControls(item.Lifts
                        ? "Колёсико — Масштаб\nЛКМ / Esc — Положить документ"
                        : "Колёсико — Масштаб\nЛКМ / Esc — Вернуться к контейнеру");
            });
            return true;
        }

        public void Scroll(float steps)
        {
            if (state == ViewState.Detail && !RequiresAllDocuments)
                zoomTarget = Mathf.Clamp(zoomTarget - Mathf.Clamp(steps, -3f, 3f) * 5.3f, detailFov - 19, detailFov + 17);
        }

        public bool Close() => Close(false);

        private bool Close(bool fromEscape)
        {
            if (state == ViewState.Detail)
            {
                if (selected.Lifts) AnimatePaper(selected.transform, documentHome);
                Move(OverviewPose(), OverviewFov(), 0.46f, () =>
                {
                    RestorePaper();
                    ShowOverview();
                    if (keyOpened && !reactionShown && !active.Container) ShowReaction();
                });
                return true;
            }
            if (state != ViewState.Overview) return false;
            if (RequiresAllDocuments && ViewedCount < RequiredCount)
            {
                if (AtDesk) ui.WarnDeskExit(AtIvan ? "Ивана" : "отца", ViewedCount, RequiredCount);
                else ui.WarnArchiveExit(ViewedCount, RequiredCount);
                return false;
            }
            Highlight(null);
            ui.SetControls("");
            if (RequiresAllDocuments) ui.ClearReading();
            Move(inspection.GameplayPose, inspection.GameplayFov, 0.50f, () => FinishPoint(fromEscape));
            return true;
        }

        private void ShowOverview()
        {
            state = ViewState.Overview;
            if (AtArchive)
            {
                active.SetHighlighted(true);
                ui.ShowArchiveOverview(ViewedCount, RequiredCount);
                return;
            }
            if (AtDesk)
            {
                active.SetHighlighted(true);
                ui.ShowDeskOverview(ViewedCount, RequiredCount);
                return;
            }
            ui.SetControls(active.Container ? "ЛКМ — Выбрать документ или деталь\nEsc — Закрыть осмотр"
                : "ЛКМ — Выбрать документ\nEsc — Выйти");
        }

        private void FinishPoint(bool fromEscape)
        {
            int before = Completed;
            bool lastIvanDesk = AtIvan && fromEscape;
            bool readRequired = RequiresAllDocuments ? ViewedCount == RequiredCount : keyOpened;
            if (RequiresAllDocuments) active.SetHighlighted(active == ObjectivePoint);
            if (readRequired && progress.Complete(Array.IndexOf(points, active)))
            {
                if (!reactionShown && !(lastIvanDesk && Completed == points.Length))
                    ShowReaction();
                lastAction = Time.unscaledTime;
            }
            active = null;
            state = ViewState.Walking;
            RestorePlayer();
            if (before != Completed)
            {
                hinted = false;
                UpdateObjective(false);
                if (Completed == points.Length)
                {
                    InvestigationCompleted?.Invoke();
                    if (lastIvanDesk) IvanDeskClosedAfterInvestigation?.Invoke();
                }
            }
        }

        private void ShowReaction()
        {
            if (AtIvan) return;
            reactionShown = true;
            ui.React(active.Reaction);
        }

        private void UpdateObjective(bool immediate)
        {
            var target = ObjectivePoint;
            foreach (var point in points) point.SetHighlighted(point == target);
            ui.Progress(Completed, points.Length, target != null ? target.Objective : "", immediate);
        }

        private Pose OverviewPose() => new(active.Overview.position, active.Overview.rotation);

        private float OverviewFov() => active.Wall ? 62f : 58f;

        private void AnimatePaper(Transform paper, Pose target)
        {
            movingPaper = paper;
            paperFrom = new Pose(paper.position, paper.rotation);
            paperTo = target;
        }

        private void Move(Pose target, float fov, float seconds, Action callback)
        {
            state = ViewState.Moving;
            arrived = callback;
            inspection.Move(target, fov, seconds);
        }

        private void Highlight(InvestigationDocument item)
        {
            if (hovered == item) return;
            if (hovered != null) hovered.SetHighlighted(false);
            hovered = item;
            if (hovered != null) hovered.SetHighlighted(true);
        }

        private void RestorePaper()
        {
            if (selected != null && selected.Lifts)
            {
                if (AtDesk)
                    selected.transform.SetLocalPositionAndRotation(deskDocumentHome.position, deskDocumentHome.rotation);
                else selected.transform.SetPositionAndRotation(documentHome.position, documentHome.rotation);
            }
            selected = null;
        }

        private void RestorePlayer()
        {
            if (view != null) inspection.Restore();
            if (player != null) player.enabled = playerWasEnabled;
            Cursor.lockState = oldCursor;
            Cursor.visible = oldCursorVisible;
        }

        private void OnDisable()
        {
            Highlight(null);
            RestorePaper();
            if (active != null && inspection != null)
            {
                if (RequiresAllDocuments)
                {
                    active.SetHighlighted(active == ObjectivePoint);
                    if (ui != null) ui.ClearReading();
                }
                RestorePlayer();
                active = null;
                arrived = null;
                movingPaper = null;
                state = ViewState.Walking;
                if (ui != null) ui.SetControls("");
            }
        }

        internal bool NearestHit(Ray ray, float range, out RaycastHit nearest,
            QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide)
        {
            var results = hits;
            int count = Physics.RaycastNonAlloc(ray, results, range, ~0, triggers);
            if (count == results.Length)
            {
                results = Physics.RaycastAll(ray, range, ~0, triggers);
                count = results.Length;
            }
            nearest = default;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = results[i];
                if (hit.distance >= distance || hit.collider.transform.IsChildOf(player.transform)) continue;
                if (hit.collider.isTrigger && hit.collider.GetComponent<InvestigationDocument>() == null &&
                    hit.collider.GetComponentInParent<FinalLabMetalDoor>() == null) continue;
                nearest = hit;
                distance = hit.distance;
            }
            return nearest.collider != null;
        }
    }
}
