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
    private MeshRenderer[] outline;
    private Quaternion rest;
    private static MaterialPropertyBlock colorProperties;

    public Function Kind => function;
    public string Label => label;
    public Vector3 Point => transform.position;
    public bool Engaged { get; private set; }

    public void Configure(Function role, string caption, Transform movingPart, Vector3 engagedEuler, Renderer indicator,
        MeshRenderer[] geometry)
    {
        function = role;
        label = caption;
        actuator = movingPart;
        movement = engagedEuler;
        lamp = indicator;
        surfaces = geometry;
    }

    public void Prepare(Material outlineMaterial)
    {
        rest = actuator.localRotation;
        outline = new MeshRenderer[surfaces.Length];
        for (int i = 0; i < surfaces.Length; i++)
        {
            var surface = surfaces[i];
            var mesh = surface.GetComponent<MeshFilter>().sharedMesh;
            var edge = new GameObject("Gameplay outline", typeof(MeshFilter), typeof(MeshRenderer));
            edge.transform.SetParent(surface.transform, false);
            edge.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = outline[i] = edge.GetComponent<MeshRenderer>();
            var layers = new Material[mesh.subMeshCount];
            for (int n = 0; n < layers.Length; n++) layers[n] = outlineMaterial;
            renderer.sharedMaterials = layers;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }
    }

    public void Highlight(bool visible)
    {
        if (outline == null) return;
        foreach (var renderer in outline) renderer.enabled = visible;
    }

    public void Engage()
    {
        if (Engaged) return;
        Engaged = true;
        actuator.localRotation = rest * Quaternion.Euler(movement);
        if (lamp != null) SetColor(lamp, new Color(.24f, .8f, .48f));
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
