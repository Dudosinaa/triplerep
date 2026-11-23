using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class FinalLabProps
{
    // The hall route goes east of the coil and west of the utility table. The rear cross-aisle
    // stays clear between the generator and the two original room openings.
    internal static readonly Vector2[] DressingWalk =
    {
        new(2.05f, 7.12f), new(2.28f, 9.15f), new(3.55f, 9.38f), new(2.25f, 10.20f),
        new(2.12f, 11.71f), new(3.31f, 12.37f), new(2.03f, 12.60f), new(0.92f, 12.96f),
        new(0.92f, 14.72f), new(0.92f, 16.93f), new(0.92f, 14.73f), new(0.92f, 13.02f),
        new(-1.52f, 13.02f), new(-1.52f, 14.61f), new(-1.99f, 15.47f), new(-2.35f, 16.12f),
        new(-1.98f, 15.43f), new(-1.52f, 14.60f), new(-1.52f, 13.02f),
        new(-2.66f, 12.92f), new(-2.61f, 11.30f), new(-2.61f, 9.65f), new(-3.55f, 7.55f),
        new(-1.90f, 7.02f), new(0, 6.58f), new(0, 4.75f)
    };

    private static void Validate(Transform root, Layout layout, List<string> report)
    {
        void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        Assert(root.childCount == 10, "The new dressing must have its ten named groups.");
        var renderers = root.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
        foreach (var renderer in renderers)
        {
            Assert(renderer.GetComponent<MeshFilter>()?.sharedMesh != null, "Missing mesh on " + renderer.name);
            foreach (var material in renderer.sharedMaterials)
                Assert(material != null && material.shader != null &&
                    (material.shader.name.StartsWith("Universal Render Pipeline/") || material.shader.name.StartsWith("Shader Graphs/glTF")),
                    "Missing or incompatible shader on " + renderer.transform.parent.name + "/" + renderer.name);
        }
        Assert(!root.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0),
            "A dressing prop has a missing script.");
        Assert(root.GetComponentsInChildren<Rigidbody>().Length == 0, "A dressing prop would be driven by physics.");
        Transform desk = root.Find("02_IvanWorkspace/Desk");
        Transform device = root.Find("02_IvanWorkspace/OldSchoolEquipment");
        Bounds deviceGeometry = GeometryBounds(new Prop(device, device.Find("Model")));
        float desktop = Surface(new Prop(desk, desk.Find("Model")), deviceGeometry.center.x, deviceGeometry.center.z).y;
        Assert(Mathf.Abs(deviceGeometry.min.y - desktop) < 0.005f, "The scan is floating above or sinking into the desk.");
        var cabling = root.Find("09_Cables");
        Assert(!cabling.GetComponentsInChildren<Collider>().Any(c => c.enabled), "Decorative cable has collision.");
        var routes = root.GetComponentsInChildren<Transform>().Where(t => t.Find("Terminals") != null).ToArray();
        int cableSamples = 0;
        foreach (var route in routes)
        {
            var mesh = route.GetComponent<MeshFilter>().sharedMesh;
            Assert(AssetDatabase.GetAssetPath(mesh) == MeshPath && mesh.vertexCount >= 20,
                "Unbaked route " + route.name);
            Vector3[] vertices = mesh.vertices;
            var first = vertices.Take(10).Aggregate(Vector3.zero, (sum, p) => sum + p) / 10f;
            var last = vertices.Skip(vertices.Length - 10).Aggregate(Vector3.zero, (sum, p) => sum + p) / 10f;
            Assert(Vector3.Distance(route.TransformPoint(first), route.Find("Terminals/Start").position) < 0.004f &&
                Vector3.Distance(route.TransformPoint(last), route.Find("Terminals/End").position) < 0.004f,
                "Cable is missing a terminal: " + route.name);
            for (int ring = 1; ring < vertices.Length / 10 - 1; ring++)
            {
                Vector3 center = Vector3.zero;
                for (int side = 0; side < 10; side++) center += vertices[ring * 10 + side];
                var p = route.TransformPoint(center / 10);
                var hits = Physics.OverlapSphere(p, 0.002f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c.transform.IsChildOf(layout.Architecture) &&
                        !c.name.Contains("Fixture") && !c.name.Contains("Lamp")).ToArray();
                Assert(hits.Length == 0, $"Route {route.name} enters architecture at {p:F3}: {string.Join(", ", hits.Select(c => c.name))}.");
                cableSamples++;
            }
        }
        int samples = 0;
        Vector2 previous = new(0, layout.Hall.Near + 0.80f);
        foreach (var point in DressingWalk)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(previous, point) / 0.075f));
            for (int step = 1; step <= steps; step++)
            {
                var p = Vector2.Lerp(previous, point, step / (float)steps);
                var low = new Vector3(p.x, 0.298f, p.y);
                var blockers = Physics.OverlapCapsule(low, low + Vector3.up * 1.19f, 0.273f, ~0, QueryTriggerInteraction.Ignore);
                Assert(!blockers.Any(), $"Blocked dressed route at {p:F3}: {string.Join(", ", blockers.Select(c => c.transform.parent.name + "/" + c.name))}");
                Assert(Physics.Raycast(new Vector3(p.x, 0.25f, p.y), Vector3.down, 0.32f, ~0, QueryTriggerInteraction.Ignore),
                    $"The dressed route has no floor at {p:F3}.");
                samples++;
            }
            previous = point;
        }
        foreach (var (group, room) in new[]
        {
            ("01_ArchiveCorridor", layout.Archive), ("02_IvanWorkspace", layout.Hall), ("03_ExperimentArea", layout.Hall),
            ("04_LabUtilityTable", layout.Hall), ("05_StorageArea", layout.Hall), ("06_ControlRoom", layout.Control),
            ("07_FatherContainment", layout.Containment), ("08_TapeRecorder", layout.Hall)
        })
        {
            var props = root.Find(group);
            foreach (var collider in props.GetComponentsInChildren<BoxCollider>().Where(c => c.enabled))
            {
                Bounds bounds = collider.bounds;
                Assert(bounds.min.x >= room.Left - 0.003f && bounds.max.x <= room.Right + 0.003f &&
                    bounds.min.z >= room.Near - 0.003f && bounds.max.z <= room.Far + 0.003f &&
                    bounds.min.y >= room.Floor - 0.003f && bounds.max.y <= room.Ceiling + 0.003f,
                    $"Solid {collider.transform.parent.name} goes outside {group}: {bounds}.");
            }
        }
        report.Add($"{renderers.Length} visible renderers verified for meshes and URP/glTF materials.");
        report.Add("Ivan's instrument rests on the sampled desktop at the lowest actual vertex of the scan.");
        report.Add($"{routes.Length} baked cables and pipe fittings have serialized geometry at both terminals.");
        report.Add($"{cableSamples} interior cable/piping center samples clear of the concrete structure.");
        report.Add($"{samples} capsule and floor samples on routes around the furniture, coil and both rear rooms.");
        report.Add("Collision bounds of solid props remain within their original rooms.");
    }
}
