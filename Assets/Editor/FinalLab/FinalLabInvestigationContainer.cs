using System;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed partial class FinalLabInvestigationBuilder
{
    private static readonly Vector3 CratePosition = new(-4.44f, 0, 6.264f);
    private static readonly Quaternion FrontUp = Quaternion.LookRotation(Vector3.down, Vector3.forward);
    private const float CrateWidth = 0.77f, CrateDepth = 0.65f, CrateHeight = 0.51f;

    private InvestigationPoint Container()
    {
        var point = Child(root, "04 K-17 investigation", CratePosition, WestWallPage);
        var crate = Child(point, "К-17");
        CrateShell(crate);
        var lid = CrateLid(crate);
        var module = Resonator(crate);
        CrateFittings(crate);
        WireModule(module);
        LightCrate(point);

        var mounted = EvidencePresentation.Mounted;
        var passport = Page(lid, "К1", new(-0.181f, -0.026f, -0.435f), new(0.20f, 0.271f), mounted,
            Quaternion.LookRotation(Vector3.up, Vector3.back));
        var waybill = Page(crate, "К2", new(-0.24f, 0.284f, -0.334f), new(0.172f, 0.246f), mounted, Quaternion.Euler(0, 0, 2));
        var pad = Child(crate, "Technical clipboard", new Vector3(0.25f, 0.617f, -0.249f), Quaternion.Euler(37, 0, 0));
        Block(pad, "Hardboard", new Vector3(0, 0, 0.002f), new Vector3(0.201f, 0.279f, 0.010f), ceramic);
        var drawing = Page(pad, "К3", new(0, 0, -0.007f), new(0.184f, 0.256f), EvidencePresentation.Lifted, Quaternion.identity);
        Block(drawing.transform, "Clipboard clip", new Vector3(0, 0.124f, -0.0048f), new Vector3(0.073f, 0.014f, 0.006f), steel);
        Tape(passport.transform, passport.Size, true);
        Tape(waybill.transform, waybill.Size, true);

        var panel = Child(crate, "Exterior stencil", new Vector3(0.09f, 0.284f, -0.326f));
        var stencil = assets.PrintCanvas(panel, new Vector2(0.378f, 0.281f), 760, 564);
        var faded = new Color(0.57f, 0.53f, 0.40f);
        assets.Label(stencil, "Crate identifier", "К-17", 0, 8, 755, 196, 148, faded, FontStyles.Bold);
        assets.Label(stencil, "Program", "ПРОЕКТ «НЕЧИСТЬ»", 4, 192, 750, 65, 55, faded, FontStyles.Bold);
        assets.Label(stencil, "Contents", "ПЕРЕНОСНОЙ РЕЗОНАНСНЫЙ МОДУЛЬ", 4, 278, 750, 110, 49, faded, FontStyles.Bold);
        assets.Label(stencil, "Authorization", "НЕ ПОДКЛЮЧАТЬ БЕЗ РАЗРЕШЕНИЯ", 4, 422, 750, 109, 42,
            new Color(0.63f, 0.37f, 0.232f), FontStyles.Bold);
        var marking = assets.AttachEvidence(panel.gameObject,
            new EvidenceRecord { id = "K17_Stencil", title = "Внешняя маркировка К-17", point = 3 },
            new Vector2(0.381f, 0.284f), EvidenceKind.Detail, mounted);

        var interior = Child(module, "Resonance block optical inspection", new Vector3(-0.045f, 0.475f, -0.118f),
            Quaternion.Euler(43, 0, 0));
        var internals = assets.AttachEvidence(interior.gameObject,
            new EvidenceRecord { id = "K17_Resonator", title = "Резонансный блок", point = 3 },
            new Vector2(0.30f, 0.170f), EvidenceKind.Detail, mounted);
        var frame = Child(crate, "Front contour anchor", new Vector3(0, 0.29f, -0.345f));
        var contour = assets.CornerOutline(frame, "Objective contour", new Vector2(0.831f, 0.487f), 0.006f, false, assets.Goal);
        var stencilContour = assets.CornerOutline(contour.transform, "Stencil corners", new Vector2(0.398f, 0.294f), 0.0035f, false, assets.Goal);
        stencilContour.transform.localPosition = new Vector3(0.09f, -0.006f, 0.016f);
        stencilContour.SetActive(true);
        var zoneMark = props.Find("10_InspectionMarkers/Inspection_04_K17_Reserved");
        if (zoneMark != null) zoneMark.position = CratePosition;
        return Point(point, "K17", "К1", "Исследуйте контейнер К-17", "Осмотреть К-17",
            "Контейнер К-17 находится возле металлического стеллажа.",
            "Значит, всё началось с этой штуки... Иван, что же ты натворил?",
            new Vector3(-3.12f, 1.30f, 6.25f), new Vector3(-4.37f, 0.63f, 6.264f), contour,
            new[] { passport, waybill, drawing, marking, internals }, crate.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray(),
            container: true);
    }

    private void LightCrate(Transform point)
    {
        var light = Child(point, "Spill from the storage worklight").gameObject.AddComponent<Light>();
        light.transform.position = new Vector3(-3.82f, 1.56f, 6.60f);
        light.transform.rotation = Quaternion.LookRotation(new Vector3(-4.44f, 0.43f, 6.245f) - light.transform.position);
        light.type = LightType.Spot;
        light.range = 2.3f;
        light.innerSpotAngle = 42;
        light.spotAngle = 70;
        light.color = new Color(1f, 0.86f, 0.69f);
        light.intensity = 2.25f;
        light.shadows = LightShadows.None;
    }

    private void CrateShell(Transform crate)
    {
        float halfX = CrateWidth * 0.5f, halfZ = CrateDepth * 0.5f;
        Block(crate, "Timber base", new Vector3(0, 0.083f, 0), new Vector3(CrateWidth, 0.061f, CrateDepth), wood, true);
        foreach (int side in new[] { -1, 1 })
        {
            Block(crate, "Wooden skid", new Vector3(side * 0.225f, 0.033f, 0), new Vector3(0.11f, 0.065f, 0.64f), wood);
            Block(crate, "Long side collision", new Vector3(0, 0.308f, side * (halfZ - 0.019f)),
                new Vector3(CrateWidth, 0.401f, 0.039f), null, true);
            Block(crate, "End wall collision", new Vector3(side * (halfX - 0.019f), 0.308f, 0),
                new Vector3(0.039f, 0.40f, CrateDepth - 0.06f), null, true);
            for (int row = 0; row < 3; row++)
            {
                float y = 0.178f + row * 0.132f;
                Block(crate, "Long rough-sawn plank", new Vector3(0, y, side * (halfZ - 0.017f)),
                    new Vector3(CrateWidth, 0.129f, 0.035f), wood);
                Block(crate, "Cross rough-sawn plank", new Vector3(side * (halfX - 0.017f), y, 0),
                    new Vector3(0.035f, 0.129f, CrateDepth - 0.065f), wood);
            }
        }
    }

    private Transform CrateLid(Transform crate)
    {
        var lid = Child(crate, "Fixed ajar lid", new Vector3(0, 0.511f, 0.315f), Quaternion.Euler(56, 0, 0));
        Block(lid, "Lid collision", new Vector3(0, 0, -0.316f), new Vector3(0.778f, 0.039f, 0.638f), null, true);
        for (int board = 0; board < 4; board++)
            Block(lid, "Lid plank", new Vector3(-0.291f + board * 0.194f, 0, -0.316f), new Vector3(0.190f, 0.036f, 0.638f), wood);
        foreach (float brace in new[] { -0.340f, 0.340f })
            Block(lid, "Steel strap", new Vector3(brace, 0.022f, -0.317f), new Vector3(0.052f, 0.012f, 0.623f), steel);
        foreach (float end in new[] { -0.237f, 0.237f })
        {
            Block(lid, "Hinge web", new Vector3(end, -0.027f, -0.032f), new Vector3(0.08f, 0.014f, 0.064f), steel);
            Primitive(crate, "Hinge pin", PrimitiveType.Cylinder, new Vector3(end, 0.509f, 0.324f),
                new Vector3(0.033f, 0.049f, 0.033f), rust).localRotation = Quaternion.Euler(0, 0, 90);
        }
        return lid;
    }

    private void CrateFittings(Transform crate)
    {
        foreach (int side in new[] { -1, 1 })
        {
            foreach (int end in new[] { -1, 1 })
            {
                Block(crate, "Angle flange front", new Vector3(side * 0.365f, 0.293f, end * 0.328f),
                    new Vector3(0.047f, 0.427f, 0.012f), steel);
                Block(crate, "Angle flange return", new Vector3(side * 0.39f, 0.293f, end * 0.307f),
                    new Vector3(0.012f, 0.427f, 0.052f), steel);
                foreach (float y in new[] { 0.136f, 0.288f, 0.450f })
                    Primitive(crate, "Rivet", PrimitiveType.Sphere, new Vector3(side * 0.365f, y, end * 0.337f),
                        new Vector3(0.014f, 0.014f, 0.010f), rust);
            }
            Block(crate, "Handle baseplate", new Vector3(side * 0.390f, 0.31f, 0),
                new Vector3(0.012f, 0.083f, 0.212f), steel);
            foreach (float z in new[] { -0.076f, 0.076f })
                Block(crate, "Handle post", new Vector3(side * 0.417f, 0.31f, z), new Vector3(0.052f, 0.028f, 0.025f), rust);
            Block(crate, "Handle grip", new Vector3(side * 0.442f, 0.31f, 0), new Vector3(0.024f, 0.029f, 0.16f), steel);
        }
        Block(crate, "Old sprung latch", new Vector3(0.307f, 0.456f, -0.338f), new Vector3(0.044f, 0.084f, 0.018f), rust);
        Block(crate, "Broken transport seal", new Vector3(0.308f, 0.384f, -0.353f), new Vector3(0.031f, 0.025f, 0.009f), copper);
        for (int i = 0; i < 4; i++)
        {
            var scratch = Block(crate, "Pry mark", new Vector3(0.278f + i * 0.017f, 0.49f - i * 0.007f, -0.327f),
                new Vector3(0.008f, 0.042f - i * 0.004f, 0.0028f), rust);
            scratch.localRotation = Quaternion.Euler(0, 0, -23);
        }
        var tag = Child(crate, "Lead inventory tag", new Vector3(0.282f, 0.121f, -0.329f), Quaternion.Euler(0, 0, -7));
        assets.Plane(tag, "Tag", Vector3.zero, new Vector2(0.116f, 0.041f), assets.Paper);
        SmallMark(tag, "Marking", "ПЛОМБА 04", new Vector2(0.116f, 0.041f), Ink);
    }

    private Transform Resonator(Transform crate)
    {
        var module = Child(crate, "Portable resonant module");
        Block(module, "Stamped chassis", new Vector3(-0.015f, 0.207f, 0.018f), new Vector3(0.638f, 0.18f, 0.472f), steel);
        Block(module, "Bakelite control strip", new Vector3(-0.035f, 0.302f, -0.137f),
            new Vector3(0.53f, 0.024f, 0.114f), ceramic);
        Block(module, "Laminated magnetic core", new Vector3(-0.095f, 0.371f, 0.123f),
            new Vector3(0.269f, 0.135f, 0.124f), rust);
        for (int coil = 0; coil < 2; coil++)
        {
            float x = -0.206f + coil * 0.178f;
            for (int winding = 0; winding < 10; winding++)
            {
                var turn = Primitive(module, "Copper winding", PrimitiveType.Cylinder,
                    new Vector3(x, 0.338f + winding * 0.0121f, 0.121f), new Vector3(0.138f, 0.0045f, 0.138f), copper);
                turn.localScale += new Vector3((winding % 3) * 0.0015f, 0, (winding % 3) * 0.0015f);
            }
            Primitive(module, "Core cap", PrimitiveType.Cylinder, new Vector3(x, 0.448f, 0.121f),
                new Vector3(0.083f, 0.014f, 0.083f), steel);
            float tubeZ = 0.019f + coil * 0.134f;
            Primitive(module, "Tube socket", PrimitiveType.Cylinder, new Vector3(0.178f, 0.309f, tubeZ),
                new Vector3(0.094f, 0.029f, 0.094f), ceramic);
            Primitive(module, "Dark radio tube envelope", PrimitiveType.Capsule, new Vector3(0.178f, 0.389f, tubeZ),
                new Vector3(0.067f, 0.069f, 0.067f), glass);
            Primitive(module, "Tube cap", PrimitiveType.Sphere, new Vector3(0.178f, 0.434f, tubeZ),
                new Vector3(0.043f, 0.021f, 0.043f), steel);
        }
        for (int pin = 0; pin < 4; pin++)
        {
            float x = -0.228f + pin * 0.057f;
            Primitive(module, "Ceramic terminal foot", PrimitiveType.Cylinder, new Vector3(x, 0.328f, -0.133f),
                new Vector3(0.037f, 0.014f, 0.037f), ceramic);
            Primitive(module, "Terminal nut", PrimitiveType.Cylinder, new Vector3(x, 0.349f, -0.133f),
                new Vector3(0.021f, 0.011f, 0.021f), copper);
        }
        var toggle = Primitive(module, "Fixed А-Б selector", PrimitiveType.Cylinder, new Vector3(0.128f, 0.347f, -0.131f),
            new Vector3(0.016f, 0.039f, 0.017f), steel);
        toggle.localRotation = Quaternion.Euler(0, 0, -24);
        var legends = Child(module, "Mode label", new Vector3(0.127f, 0.319f, -0.174f), FrontUp);
        SmallMark(legends, "Modes", "А     Б", new Vector2(0.116f, 0.035f), new Color(0.76f, 0.71f, 0.53f));
        var stamp = Child(module, "Module data plate", new Vector3(-0.016f, 0.222f, -0.219f));
        assets.Plane(stamp, "Plate", Vector3.zero, new Vector2(0.195f, 0.051f), assets.Paper);
        SmallMark(stamp, "Serial", "К-17   /   МОДУЛЬ", new Vector2(0.195f, 0.051f), Ink);
        return module;
    }

    private void SmallMark(Transform node, string name, string text, Vector2 size, Color color)
    {
        var canvas = assets.PrintCanvas(node, size, 500, 180);
        var label = assets.Label(canvas, name, text, 4, 39, 492, 110, 69, color, FontStyles.Bold, 43);
        label.alignment = TextAlignmentOptions.Center;
    }

    private void WireModule(Transform module)
    {
        var routeRoot = Child(root, "K-17 wall-mounted cable paths");
        var network = props.Find("09_Cables/MainPowerTrunk/West_Distribution");
        if (network == null) throw new InvalidOperationException("West wall cable distribution was not found.");
        var materials = new[] { Load<Material>(PropPath + "Materials/FinalLab_Cable_Rubber.mat"),
            Load<Material>(PropPath + "Materials/FinalLab_Cable_FadedBrown.mat") };
        var links = Child(module, "Module outlet ferrules");
        for (int i = 0; i < materials.Length; i++)
        {
            float z = 6.157f + i * 0.079f, x = -4.946f + i * 0.021f, y = 2.453f + i * 0.048f;
            var port = Primitive(links, "Terminal ferrule", PrimitiveType.Cylinder,
                new Vector3(z - CratePosition.z, 0.301f + 0.052f * i, 0.333f),
                new Vector3(0.052f, 0.035f, 0.052f), steel);
            port.localRotation = Quaternion.Euler(90, 0, 0);
            Vector3 start = port.position + Vector3.left * 0.036f;
            Vector3 end = network.position + new Vector3(-0.015f, -0.079f + i * 0.052f, -0.083f);
            Vector3[] anchors = { start, new(x, start.y + 0.02f, z), new(x, y, z),
                new(x, y, end.z - 0.16f), new(x, end.y, end.z - 0.076f), end };
            var route = Child(routeRoot, i == 0 ? "Resonator lead to west bus" : "Return lead to west bus");
            var points = FinalLabProps.BakePackagePoints(FinalLabProps.Rounded(anchors, 0.083f), true);
            var mesh = FinalLabProps.Tube(route.name, route, points, 0.0108f);
            string path = FinalLabEvidenceAssets.Root + "/Geometry/K17_Wall_Lead_" + i + ".asset";
            var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (prior != null)
            {
                EditorUtility.CopySerialized(mesh, prior);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = prior;
            }
            else AssetDatabase.CreateAsset(mesh, path);
            route.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            route.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[i];
            foreach (float mount in new[] { 6.54f, 7.22f, 8.23f, 9.27f, 10.43f, 11.54f, 12.66f, 13.34f })
                Block(route, "Saddle clamp", new Vector3(x - 0.004f, y, mount),
                    new Vector3(0.045f, 0.027f, 0.023f), steel);
            foreach (float riser in new[] { 0.66f, 1.12f, 1.62f, 2.12f })
                Block(route, "Riser clamp", new Vector3(x - 0.004f, riser, z),
                    new Vector3(0.045f, 0.022f, 0.027f), steel);
            Child(route, "Port", start);
            Child(route, "Distribution terminal", end);
        }
        FitModel(routeRoot, "Service loop under module", PropPath + "Cables/bunch_of_electric_wires.glb",
            new Vector3(-4.86f, 0.06f, 6.26f), 0.10f, 90);
        foreach (var mesh in routeRoot.Find("Service loop under module").GetComponentsInChildren<MeshRenderer>())
            mesh.sharedMaterials = Enumerable.Repeat(materials[0], mesh.sharedMaterials.Length).ToArray();
    }
}
