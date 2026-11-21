using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DocumentReader : MonoBehaviour
{
    public bool IsReading => openDocument != null;

    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonPlayer movement;
    [SerializeField, Min(0.1f)] private float readingDistance = 3f;
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject readingPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private ScrollRect bodyScrollRect;

    private readonly RaycastHit[] aimHits = new RaycastHit[8];
    internal float HintDistance => readingDistance;

    private InteractionHints interactionHints;
    private ReadableDocument openDocument;

    private void Awake()
    {
        interactionHints = GetComponent<InteractionHints>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (movement == null) movement = GetComponent<FirstPersonPlayer>();
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (readingPanel != null) readingPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || playerCamera == null) return;

        bool pressedE = Keyboard.current.eKey.wasPressedThisFrame;
        if (openDocument != null)
        {
            if ((pressedE || Keyboard.current.escapeKey.wasPressedThisFrame) && InteractionInput.TryConsume()) CloseDocument();
            else ScrollDocument();
            return;
        }

        if (movement != null && !movement.enabled)
        {
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
            return;
        }
        ReadableDocument target = FindDocument();
        if (target != null) interactionHints?.OfferFocus(target);
        if (interactionPrompt != null) interactionPrompt.SetActive(target != null);
        if (pressedE && target != null && InteractionInput.TryConsume()) OpenDocument(target);
    }

    private ReadableDocument FindDocument()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, readingDistance, ~0, QueryTriggerInteraction.Ignore))
            return null;
        if (!hit.collider.transform.IsChildOf(transform))
            return hit.collider.GetComponentInParent<ReadableDocument>();

        // A downward ray can hit the player's own controller before reaching a document.
        var hits = aimHits;
        int count = Physics.RaycastNonAlloc(ray, hits, readingDistance, ~0, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
        {
            // A full buffer can omit a closer hit.
            hits = Physics.RaycastAll(ray, readingDistance, ~0, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        Collider closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var collider = hits[i].collider;
            if (hits[i].distance >= closestDistance || collider.transform.IsChildOf(transform)) continue;

            closest = collider;
            closestDistance = hits[i].distance;
        }
        return closest != null ? closest.GetComponentInParent<ReadableDocument>() : null;
    }

    private void OpenDocument(ReadableDocument document)
    {
        openDocument = document;
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        if (titleText != null) titleText.text = document.Title;
        if (bodyText != null) bodyText.text = document.Text;
        if (readingPanel != null) readingPanel.SetActive(true);
        if (movement != null) movement.enabled = false;

        Canvas.ForceUpdateCanvases();
        if (bodyScrollRect != null) bodyScrollRect.verticalNormalizedPosition = 1f;
    }

    private void CloseDocument()
    {
        openDocument = null;
        if (readingPanel != null) readingPanel.SetActive(false);
        if (movement != null) movement.enabled = true;
    }

    private void ScrollDocument()
    {
        if (bodyScrollRect == null || Mouse.current == null) return;
        float wheel = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;

        float overflow = Mathf.Max(1f, bodyScrollRect.content.rect.height - bodyScrollRect.viewport.rect.height);
        bodyScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
            bodyScrollRect.verticalNormalizedPosition + wheel * 0.5f / overflow);
    }

    private void OnDisable()
    {
        if (openDocument != null) CloseDocument();
    }
}
