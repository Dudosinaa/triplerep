using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class RadioMemoryPuzzle
{
    [SerializeField] private Texture2D familyPhoto;
    [SerializeField] private Transform deskLamp;
    [SerializeField] private Light deskLight;
    [SerializeField] private Vector3 photoPosition;

    private Transform photoFrame, photoCopy;
    private BoxCollider photoTarget, lampTarget;
    private FirstMindCloseup photoCloseup;
    private Material frameMaterial, printMaterial;
    private float photoYaw, photoPitch;

    private void CreateHouseActivities()
    {
        if (deskLamp != null && deskLight != null)
        {
            var renderers = deskLamp.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) lampTarget = InteractionBox("Desk lamp interaction", renderers);
        }
        if (familyPhoto == null || room == null) return;
        var frameShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var printShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        frameMaterial = new Material(frameShader) { color = new Color(.26f, .15f, .08f) };
        printMaterial = new Material(printShader) { mainTexture = familyPhoto };
        printMaterial.SetFloat("_Cull", 0);

        photoFrame = new GameObject("Family photograph").transform;
        photoFrame.SetParent(transform, true);
        photoFrame.SetPositionAndRotation(room.TransformPoint(photoPosition),
            room.rotation * Quaternion.Euler(-8, 8, 0));
        PhotoPart("Wooden frame", PrimitiveType.Cube, Vector3.zero,
            new Vector3(.244f, .187f, .024f), frameMaterial);
        PhotoPart("Family print", PrimitiveType.Quad, new Vector3(0, 0, -.013f),
            new Vector3(.210f, .1575f, 1), printMaterial);
        PhotoPart("Support", PrimitiveType.Cube, new Vector3(0, -.093f, .022f),
            new Vector3(.21f, .015f, .085f), frameMaterial);
        photoTarget = InteractionBox("Photograph interaction", photoFrame.GetComponentsInChildren<Renderer>());
        photoTarget.size += Vector3.one * .04f;
    }

    private void PhotoPart(string title, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = title;
        part.transform.SetParent(photoFrame, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
    }

    private void OpenPhoto()
    {
        photoCloseup = new FirstMindCloseup(view, gameObject.scene, new Vector3(0, .2f, -6),
            Vector3.zero, 1.48f, 1.69f);
        photoCopy = Instantiate(photoFrame, photoCloseup.Root);
        photoCopy.name = "Photo in hand";
        photoCopy.localPosition = Vector3.up * .15f;
        photoCopy.localRotation = Quaternion.identity;
        photoCopy.localScale = Vector3.one * 13f;
        foreach (var part in photoCopy.GetComponentsInChildren<Transform>())
            part.gameObject.layer = photoCloseup.Layer;
        photoYaw = photoPitch = 0f;
    }

    private void UpdatePhoto()
    {
        photoCloseup.UpdateFraming();
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed) return;
        Vector2 delta = mouse.delta.ReadValue();
        photoYaw = Mathf.Repeat(photoYaw - delta.x * .35f, 360f);
        photoPitch = Mathf.Clamp(photoPitch + delta.y * .30f, -65f, 65f);
        photoCopy.localRotation = Quaternion.Euler(photoPitch, photoYaw, 0f);
    }

    private void ToggleDeskLamp()
    {
        deskLight.enabled = !deskLight.enabled;
        PlayEffect(contactClick);
    }

    private BoxCollider InteractionBox(string title, Renderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var target = new GameObject(title);
        target.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
        target.transform.SetParent(transform, true);
        var collider = target.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = bounds.size + Vector3.one * .04f;
        return collider;
    }

    private static void CheckTarget(Collider collider, Target candidate, Ray ray, ref float reach, ref Target target)
    {
        if (collider == null || !collider.gameObject.activeInHierarchy || !collider.Raycast(ray, out var hit, reach))
            return;
        reach = hit.distance;
        target = candidate;
    }

    private void OnDestroy()
    {
        radioCloseup?.Dispose();
        photoCloseup?.Dispose();
        if (frameMaterial != null) Destroy(frameMaterial);
        if (printMaterial != null) Destroy(printMaterial);
        DisposeRoomProps();
    }
}
