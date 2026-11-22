using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

internal sealed partial class FinalLabResonatorBuilder
{
    private void RoutePower(Transform module, Transform props)
    {
        var network = props.Find("09_Cables/MainPowerTrunk/East_Distribution");
        var beam = props.Find("09_Cables/GeneratorCables/Beam_Junction");
        if (network == null || beam == null) throw new InvalidOperationException("The east trunk and generator feed are required.");
        var ports = Group(module, "K17_CableConnectors", Vector3.zero);
        var runs = Group(module, "K17_EastWallRuns", Vector3.zero);
        var junction = Group(runs, "Branch to east trunk and generator feed", Vector3.zero);
        junction.SetPositionAndRotation(new Vector3(4.939f, network.position.y, beam.position.z - .27f), Quaternion.identity);
        Block(junction, "Wall terminal box", Vector3.zero, new(.098f, .214f, .310f), steel);
        Fit(junction, "Factory industrial junction cover", "TirgamesAssets/Factory/Prefabs/PowerBox02_1.prefab",
            new(-.029f, -.094f, 0), new(.053f, .188f, .267f), Vertical, finish: steel);
        Plate(junction, "Branch identification", new(-.060f, .031f, 0), new(.116f, .035f), black, Ink, ("КОНТУР 17", 58)).localRotation = Quaternion.Euler(0, 90, 0);
        var rubber = Load<Material>("Assets/Art/Models/Environment/FinalLab/Materials/FinalLab_Cable_Rubber.mat");
        var signal = Load<Material>("Assets/Art/Models/Environment/FinalLab/Materials/FinalLab_Cable_FadedBrown.mat");
        string[] names = { "K17_PowerInput", "K17_SignalOutput", "K17_GroundConnector" };
        string[] labels = { "ВВОД", "СИГНАЛ", "ЗЕМЛЯ" };
        for (int i = 0; i < names.Length; i++)
        {
            var port = Group(ports, names[i], new Vector3(.105f + .120f * i, .957f, -.316f));
            Cylinder(port, "Metal bulkhead gland", Vector3.zero, .032f, .049f, steel, Face);
            Cylinder(port, "Ceramic or brass insert", new(0, 0, -.024f), .020f, .022f, i == 2 ? copper : ceramic, Face);
            Plate(port, "Terminal tag", new(0, -.063f, -.008f), new(.090f, .026f), black, Ink, (labels[i], 57)).localRotation = Quaternion.identity;
            var route = Group(runs, names[i] + " lead", Vector3.zero);
            Vector3 start = port.TransformPoint(new Vector3(0, 0, -.031f));
            float z = start.z;
            float x = network.position.x - .016f - .008f * i;
            Vector3 end = junction.TransformPoint(new Vector3(-.006f, -.110f, -.102f + .096f * i));
            Cylinder(junction, "Sealed lower conduit gland", junction.InverseTransformPoint(end), .026f, .039f, steel, Vertical);
            float lower = 2.177f + .096f * i;
            Vector3[] anchors = { start, new(x, start.y + .058f, z), new(x, lower, z),
                new(x, lower, end.z), new(x, end.y - .047f, end.z), end };
            var points = FinalLabProps.BakePackagePoints(FinalLabProps.Rounded(anchors, .050f), true);
            var mesh = FinalLabProps.Tube(route.name, route, points, i == 0 ? .014f : .010f);
            string meshPath = Generated + "/" + names[i] + "_Cable.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved != null)
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
                mesh = saved;
            }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            route.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            route.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i == 0 ? rubber : i == 1 ? signal : black;
            Group(route, "Port", route.InverseTransformPoint(start));
            Group(route, "Distribution terminal", route.InverseTransformPoint(end));
            foreach (float y in new[] { 1.24f, 1.62f, 2.02f })
            {
                Vector3 clamp = new((x + 4.997f) * .5f, y, z);
                Block(route, "Wall saddle", route.InverseTransformPoint(clamp), new(.032f, .024f, 4.997f - x + .015f), steel);
            }
        }
    }

    private void TransportCrate()
    {
        var crate = Group(null, CrateName, new Vector3(.889f, 0, 2.145f));
        var wood = Material("Shipping_Worn_Timber", new Color(.86f, .78f, .68f), .02f, .27f,
            Load<Texture2D>(ParentPath + "/Crate/tex/crate_tex.jpg"));
        Fit(crate, "Imported wooden packing case", "Crate/Crate.fbx", new(0, .035f, 0), new(.834f, .650f, 1.164f), Vertical, finish: wood);
        Block(crate, "Case collision", new(0, .343f, 0), new(.838f, .686f, 1.168f), null, true);
        foreach (float z in new[] { -.423f, .423f })
        {
            Block(crate, "Underside skid", new(0, .018f, z), new(.830f, .036f, .103f), wood);
            foreach (float x in new[] { -.417f, .417f })
            {
                Block(crate, "Steel band up side", new(x, .366f, z), new(.008f, .620f, .053f), steel);
                Bolt(crate, new Vector3(x + Mathf.Sign(x) * .009f, .599f, z), .015f, Quaternion.Euler(0, 0, 90));
                Bolt(crate, new Vector3(x + Mathf.Sign(x) * .009f, .140f, z), .015f, Quaternion.Euler(0, 0, 90));
            }
            Block(crate, "Steel band across lid", new(0, .686f, z), new(.837f, .008f, .053f), steel);
        }
        var cargo = Plate(crate, "1992 shipping notice", new(-.421f, .400f, .013f), new(.697f, .342f), paper, BlueBlack,
            ("ЭЛЕКТРОИЗМЕРИТЕЛЬНОЕ", 39), ("ОБОРУДОВАНИЕ", 43), ("ГРУЗ № 04", 65), ("РОССИЯ — НЬЮ-ЙОРК", 49), ("1992     •     ХРУПКОЕ", 50));
        cargo.localRotation = Quaternion.Euler(0, 90, 0);
        Plate(crate, "Former seal mark", new(-.421f, .581f, .294f), new(.177f, .046f), paper, BlueBlack,
            ("ПЛОМБА № 04", 62)).localRotation = Quaternion.Euler(0, 90, 0);
        Plate(crate, "Small metal case identifier", new(-.420f, .183f, .484f), new(.084f, .030f), steel, Ink,
            ("К-17", 73)).localRotation = Quaternion.Euler(0, 90, 0);
        Spot(crate, "Soft cargo label spill", new(-1.10f, 1.68f, -.38f), new(-.39f, .44f, .02f),
            new Color(1f, .88f, .72f), 2.65f, 2.30f, 51);
        const string credit = Generated + "/Transport Crate Credit.txt";
        if (!File.Exists(credit))
            File.WriteAllText(credit, "Crate by Vinrax (vinrax@gmail.com), 30 July 2015.\nCC BY 3.0 — https://creativecommons.org/licenses/by/3.0/\nAdapted with scaled dimensions, metal bands and shipping labels.\n");
    }

    private Transform Plate(Transform parent, string name, Vector3 position, Vector2 size, Material backing, Color color,
        params (string text, int fontSize)[] lines)
    {
        var panel = Group(parent, name, position);
        panel.localRotation = Quaternion.Euler(0, 180, 0);
        Block(panel, "Plate backing", new(0, 0, .0028f), new(size.x, size.y, .005f), backing);
        if (size.x > .25f && size.y > .08f)
            foreach (float x in new[] { -.47f, .47f })
                foreach (float y in new[] { -.455f, .455f })
                    Bolt(panel, new Vector3(x * size.x, y * size.y, -.001f), .0075f, Face);
        const float width = 960f;
        float height = width * size.y / size.x;
        var canvas = print.PrintCanvas(panel, size, width, height);
        float row = height * .89f / lines.Length;
        float textTop = height * .055f;
        for (int i = 0; i < lines.Length; i++)
        {
            float lineHeight = name == "K17_Nameplate" ? row * (i == 0 ? 1.56f : .86f) : row;
            float limit = Mathf.Min(lines[i].fontSize * (width / 500f), lineHeight * .94f);
            var label = print.Label(canvas, "Inscription " + (i + 1), lines[i].text, 32, textTop,
                width - 64, lineHeight, limit, color, FontStyles.Bold, limit * .62f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            textTop += lineHeight;
        }
        return panel;
    }

    private static Transform Fit(Transform parent, string name, string path, Vector3 bottomCenter, Vector3 size, Quaternion rotation,
        Func<MeshRenderer, bool> select = null, Material finish = null)
    {
        var holder = Group(parent, name, Vector3.zero);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(ParentPath + "/" + path), holder);
        foreach (var item in model.GetComponentsInChildren<Collider>(true)) item.enabled = false;
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (select != null && !select(renderer)) renderer.enabled = false;
            if (renderer.enabled && finish != null) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => finish).ToArray();
        }
        // A nested alignment pivot keeps dimensions in the fan-out axes of the installed panel.
        model.transform.localRotation = rotation * model.transform.localRotation;
        var renderers = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToArray();
        var bounds = LocalBounds(parent, renderers);
        holder.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        var fitted = LocalBounds(parent, renderers);
        holder.localPosition = bottomCenter - new Vector3(fitted.center.x, fitted.min.y, fitted.center.z);
        return holder;
    }

    private static Bounds LocalBounds(Transform space, MeshRenderer[] renderers)
    {
        Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
        foreach (var renderer in renderers)
        {
            var b = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            var matrix = space.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        return new Bounds((min + max) * .5f, max - min);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
        throw new InvalidOperationException("Required asset is missing: " + path);

    private static Material Material(string name, Color color, float metallic, float smoothness, Texture texture = null, float emission = 0)
    {
        string path = Generated + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetTexture("_BaseMap", texture);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission > 0)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Transform Group(Transform parent, string name, Vector3 position)
    {
        var node = new GameObject(name).transform;
        node.SetParent(parent, false);
        node.localPosition = position;
        return node;
    }

    private static Transform Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = false) =>
        Part(parent, name, PrimitiveType.Cube, position, size, Vertical, material, solid);

    private static Transform Cylinder(Transform parent, string name, Vector3 position, float radius, float depth, Material material, Quaternion rotation) =>
        Part(parent, name, PrimitiveType.Cylinder, position, new Vector3(radius * 2f, depth * .5f, radius * 2f), rotation, material, false);

    private void Bolt(Transform parent, Vector3 position, float diameter, Quaternion rotation) =>
        Cylinder(parent, "Fastener", position, diameter * .5f, .009f, steel, rotation);

    private static Transform Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 size,
        Quaternion rotation, Material material, bool solid)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.SetLocalPositionAndRotation(position, rotation);
        part.transform.localScale = size;
        if (material == null) Object.DestroyImmediate(part.GetComponent<MeshRenderer>());
        else part.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }

    private static void Spot(Transform parent, string name, Vector3 source, Vector3 target, Color color, float intensity, float range, float angle)
    {
        var anchor = Group(parent, name, source);
        anchor.localRotation = Quaternion.LookRotation(target - source);
        var light = anchor.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.intensity = intensity;
        light.color = color;
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * .73f;
        light.shadows = LightShadows.None;
    }
}
