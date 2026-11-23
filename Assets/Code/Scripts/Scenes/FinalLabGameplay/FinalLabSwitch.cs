using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class FinalLabSwitch : MonoBehaviour
{
    public enum Function { Reserve, Isolation, Traction, Mode, Contour, Cover, Overload }

    [SerializeField] private Function function;
    [SerializeField] private string label;
    [SerializeField] private Transform actuator;
    [SerializeField] private Vector3 movement;
    [SerializeField] private Renderer lamp;
    [SerializeField] private MeshRenderer[] surfaces;
    [SerializeField] private Collider mountCollider;
    private MeshRenderer[] outline;
    private Material[][] availableLayers, focusedLayers;
    private int highlight;
    private Quaternion rest;
    private static MaterialPropertyBlock colorProperties;

    public Function Kind => function;
    public string Label => label;
    public Vector3 Point => transform.position;
    public bool Engaged { get; private set; }

    public void Configure(Function role, string caption, Transform movingPart, Vector3 engagedEuler, Renderer indicator,
        MeshRenderer[] geometry, Collider mount)
    {
        function = role;
        label = caption;
        actuator = movingPart;
        movement = engagedEuler;
        lamp = indicator;
        surfaces = geometry;
        mountCollider = mount;
    }

    public bool Owns(Collider hit) => hit == mountCollider || hit.transform.IsChildOf(actuator);

    public void Prepare(Material available, Material focused)
    {
        rest = actuator.localRotation;
        outline = new MeshRenderer[surfaces.Length];
        availableLayers = new Material[surfaces.Length][];
        focusedLayers = new Material[surfaces.Length][];
        for (int i = 0; i < surfaces.Length; i++)
        {
            var surface = surfaces[i];
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var edge = new GameObject("Gameplay outline", typeof(MeshFilter), typeof(MeshRenderer));
            edge.transform.SetParent(surface.transform, false);
            edge.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = outline[i] = edge.GetComponent<MeshRenderer>();
            var availableMaterials = availableLayers[i] = new Material[mesh.subMeshCount];
            var focusedMaterials = focusedLayers[i] = new Material[mesh.subMeshCount];
            for (int n = 0; n < mesh.subMeshCount; n++)
            {
                availableMaterials[n] = available;
                focusedMaterials[n] = focused;
            }
            renderer.sharedMaterials = availableMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }
    }

    public void Highlight(int strength)
    {
        if (outline == null || highlight == strength) return;
        highlight = strength;
        for (int i = 0; i < outline.Length; i++)
        {
            if (outline[i] == null) continue;
            outline[i].enabled = strength > 0;
            if (strength > 0) outline[i].sharedMaterials = strength == 2 ? focusedLayers[i] : availableLayers[i];
        }
    }

    public void Engage()
    {
        if (Engaged) return;
        Engaged = true;
        Highlight(0);
        StartCoroutine(MoveActuator(rest * Quaternion.Euler(movement)));
        if (lamp != null) SetColor(lamp, new Color(.24f, .8f, .48f));
    }

    private IEnumerator MoveActuator(Quaternion finish)
    {
        const float duration = .19f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            actuator.localRotation = Quaternion.Slerp(rest, finish, Mathf.SmoothStep(0, 1, elapsed / duration));
            yield return null;
        }
        actuator.localRotation = finish;
    }

    public static void SetColor(Renderer renderer, Color color)
    {
        var properties = colorProperties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        properties.SetColor("_EmissionColor", color * .35f);
        renderer.SetPropertyBlock(properties);
    }
}
