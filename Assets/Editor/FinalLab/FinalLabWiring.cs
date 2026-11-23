using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class FinalLabProps
{
    private sealed partial class Dressing
    {
        public void Wire()
        {
            var zone = Group(root, "09_Cables");
            var trunk = Group(zone, "MainPowerTrunk");
            Room hall = layout.Hall, control = layout.Control, archive = layout.Archive, father = layout.Containment;
            float rear = hall.Far - 0.072f, west = hall.Left + 0.073f, east = hall.Right - 0.073f;
            float high = hall.Ceiling - 0.35f, controlHigh = control.Ceiling - 0.20f;
            float controlEdge = layout.ControlDoor.max.x - 0.095f, controlPass = layout.ControlDoor.min.y - 0.092f;
            float fatherEdge = layout.ContainmentDoor.max.x - 0.100f, fatherPass = layout.ContainmentDoor.min.y - 0.087f;
            float archiveEdge = layout.ArchiveDoor.max.x - 0.101f, archivePass = layout.ArchiveDoor.min.y - 0.087f;
            float beam = layout.NorthBeam.center.z;
            var panelPort = new Vector3(panel.Bounds.max.x - 0.17f, panel.Bounds.max.y - 0.018f, panel.Bounds.center.z + 0.14f);
            Junction(trunk, "Panel_Gland", panelPort, new Vector3(0.12f, 0.07f, 0.10f));

            Run(trunk, "Panel_To_Hall", rubber, 0.024f, true, panelPort,
                V(panelPort.x, controlHigh, panelPort.z), V(controlEdge, controlHigh, control.Near + 0.17f),
                V(controlEdge, controlPass, control.Near + 0.12f), V(controlEdge, controlPass, hall.Far - 0.15f),
                V(controlEdge, high, rear));
            Junction(trunk, "Main_Junction", V(controlEdge, high, rear), V(0.26f, 0.17f, 0.145f));
            Run(trunk, "Rear_Main_East", rubber, 0.023f, true, V(controlEdge, high, rear), V(1.0f, high, rear),
                V(2.78f, high, rear), V(east, high, rear));
            Run(trunk, "Rear_Main_West", signal, 0.018f, true, V(controlEdge, high + 0.066f, rear),
                V(-2.6f, high + 0.066f, rear), V(west, high + 0.066f, rear));
            Junction(trunk, "East_Distribution", V(east, high, rear), V(0.15f, 0.20f, 0.22f));
            Junction(trunk, "West_Distribution", V(west, high + 0.066f, rear), V(0.15f, 0.17f, 0.22f));

            var corridor = Group(zone, "CorridorCables");
            float archWall = archive.Right - 0.07f;
            Run(corridor, "Hall_To_Archive", rubber, 0.018f, true, V(east, high, rear), V(east, high, beam),
                V(east, high, hall.Near + 3.6f), V(east, high, hall.Near + 0.09f),
                V(archiveEdge, high, hall.Near + 0.09f), V(archiveEdge, archivePass, hall.Near + 0.07f),
                V(archiveEdge, archivePass, archive.Far - 0.13f), V(archWall, archivePass, archive.Far - 0.17f),
                V(archWall, archive.Ceiling - 0.17f, archive.Far - 0.25f), V(archWall, archive.Ceiling - 0.17f, archive.Near + 2.17f),
                V(archWall, archive.Ceiling - 0.17f, archive.Near + 0.18f));
            var archiveLamp = layout.Architecture.Find("06_Lighting/Archive_WallWash").position;
            Junction(corridor, "Archive_Lighting_Junction", V(archWall, archive.Ceiling - 0.17f, archiveLamp.z),
                V(0.13f, 0.14f, 0.18f));
            Run(corridor, "Archive_Light_Feed", signal, 0.012f, false,
                V(archWall, archive.Ceiling - 0.17f, archiveLamp.z), V(archWall - 0.12f, archive.Ceiling - 0.065f, archiveLamp.z),
                V(archiveLamp.x, archive.Ceiling - 0.064f, archiveLamp.z));
            var shaft = layout.Architecture.Find("01_Entrance_Stairwell/StairwellWalls/ShaftEast").GetComponent<BoxCollider>().bounds;
            var shaftLamp = layout.Architecture.Find("06_Lighting/ShaftLampHousing").GetComponent<BoxCollider>().bounds;
            float shaftX = shaft.min.x - 0.044f;
            Run(corridor, "Stair_Light_Feed", signal, 0.014f, true, V(archWall, archive.Ceiling - 0.17f, archive.Near + 0.18f),
                V(shaftX, archive.Ceiling - 0.17f, archive.Near + 0.16f), V(shaftX, archive.Ceiling - 0.17f, shaft.max.z - 0.13f),
                V(shaftX, shaftLamp.max.y + 0.08f, shaft.max.z - 0.18f), V(shaftX, shaftLamp.max.y + 0.08f, shaftLamp.center.z),
                V(shaftX, shaftLamp.max.y - 0.017f, shaftLamp.center.z));

            var ivan = Group(zone, "IvanDeskCables");
            float ivanZ = ivanDesk.Bounds.center.z + 0.52f;
            var ivanSocket = V(west, 0.69f, ivanZ);
            Junction(ivan, "Desk_Wall_Socket", ivanSocket, V(0.13f, 0.12f, 0.14f));
            Run(ivan, "Ivan_Desk_Feed", signal, 0.012f, true, V(west, high + 0.066f, rear), V(west, high + 0.066f, ivanZ),
                V(west, 1.53f, ivanZ), ivanSocket, V(ivanDesk.Bounds.min.x + 0.15f, 0.749f, ivanZ - 0.08f));

            var gen = Group(zone, "GeneratorCables");
            Bounds gb = generator.Bounds;
            var generatorFeed = V(gb.center.x - 0.43f, gb.max.y - 0.13f, gb.min.z + 0.20f);
            Junction(gen, "Generator_Input", generatorFeed, V(0.18f, 0.13f, 0.16f));
            Junction(gen, "Beam_Junction", V(generatorFeed.x, layout.NorthBeam.min.y - 0.026f, beam),
                V(0.20f, 0.075f, 0.175f));
            Run(gen, "Generator_Feed", rubber, 0.024f, true, V(east, high, beam),
                V(east - 0.125f, layout.NorthBeam.min.y - 0.052f, beam),
                V(2.88f, layout.NorthBeam.min.y - 0.052f, beam),
                V(1.03f, layout.NorthBeam.min.y - 0.052f, beam),
                V(generatorFeed.x, layout.NorthBeam.min.y - 0.052f, beam),
                V(generatorFeed.x, 1.15f, generatorFeed.z), generatorFeed);

            var coil = Group(zone, "TeslaCoilCables");
            var output = V(gb.max.x - 0.09f, 0.26f, gb.min.z + 0.04f);
            var inlet = V(tesla.Bounds.center.x - 0.39f, 0.132f, tesla.Bounds.center.z + 0.55f);
            Junction(coil, "Three_Generator_Outlets", output, V(0.25f, 0.17f, 0.145f));
            Junction(coil, "Tesla_Base_Connector", inlet, V(0.25f, 0.135f, 0.16f));
            for (int i = 0; i < 3; i++)
            {
                float offset = (i - 1) * 0.054f;
                var delta = Vector3.right * offset;
                Run(coil, i < 2 ? "Power_0" + (i + 1) : "Signal_03", i < 2 ? rubber : signal,
                    i == 0 ? 0.023f : i == 1 ? 0.017f : 0.011f, false,
                    output + delta, V(output.x + offset, 0.052f, gb.min.z - 0.16f),
                    V(inlet.x + offset, 0.047f, inlet.z + 0.22f), inlet + delta);
            }

            var bedside = Group(zone, "FatherRoomCables");
            float bedWall = father.Right - 0.082f, backWall = father.Far - 0.042f;
            var distribution = V(bedWall, 1.08f, father.Far - 0.76f);
            Junction(bedside, "Bedside_Distribution_Box", distribution, V(0.19f, 0.32f, 0.23f));
            Run(bedside, "Containment_Power", rubber, 0.020f, true,
                V(fatherEdge, high, rear), V(fatherEdge, fatherPass, hall.Far - 0.11f),
                V(fatherEdge, fatherPass, father.Near + 0.12f), V(fatherEdge, father.Ceiling - 0.18f, father.Near + 0.16f),
                V(bedWall, father.Ceiling - 0.18f, father.Near + 0.16f),
                V(bedWall, father.Ceiling - 0.18f, father.Near + 1.9f),
                V(bedWall, father.Ceiling - 0.18f, distribution.z), distribution);
            for (int line = 0; line < 3; line++)
            {
                float y = 0.68f + 0.077f * line;
                float headX = (bed.Bounds.center + bed.Fit.rotation * Vector3.left * 0.94f).x;
                float cappedZ = Mathf.Min(backWall - 0.006f, bed.Bounds.max.z + 0.035f);
                var tip = V(headX + 0.38f + 0.14f * line, y, cappedZ);
                Run(bedside, "Bedside_Signal_" + (line + 1), line == 1 ? rubber : signal, 0.010f + line * 0.001f,
                    false, distribution + V(0, -0.09f + line * 0.03f, 0.055f),
                    V(bedWall, y, backWall), V(tip.x, y, backWall), tip);
                Junction(bedside, "Capped_Connector_" + (line + 1), tip, V(0.048f, 0.044f, 0.054f));
            }

            var decorative = Group(zone, "DecorativeCableBundles");
            PlaceModel(decorative, "Panel_Wall_Wiring", ImportedCable, "power_cable_wiring",
                V(control.Right - 0.019f, 1.60f, panel.Bounds.center.z + 0.75f), 90, 0.53f);
            PlaceModel(decorative, "Ivan_Extension_Cord", ImportedCable, "extension_cord",
                V(ivanDesk.Bounds.min.x + 0.19f, 0.004f, ivanDesk.Bounds.center.z - 0.46f), 25, width: 0.215f);
            PlaceModel(decorative, "Generator_Service_Loop", ImportedCable, "bunch_of_electric_wires",
                V(gb.center.x - 0.43f, 0.001f, gb.max.z + 0.19f), 16, width: 0.59f, depth: 0.40f);
            FinalLabProps.Place(decorative, "Slack_Cable_Behind_Desk", ImportedCable + "cables.glb",
                V(ivanDesk.Bounds.min.x + 0.10f, 0.032f, ivanDesk.Bounds.center.z + 0.39f),
                new Vector3(0, 90, 0), height: 0.20f, width: 0.15f, depth: 0.42f, selection: r => r.name == "Object_2");
        }

        private void Pipes(Transform zone)
        {
            Room room = layout.Control;
            var pipes = Group(zone, "Pipes");
            const string part = "Pipe_Straight_ExtraLong_low";
            string asset = Furniture + "modular_industrial_pipes.glb";
            float x = room.Left + 0.158f, z = room.Far - 0.146f, axisY = room.Ceiling - 0.265f;
            float joint = x + 1.66f, runStart = x + 0.16f;
            for (int i = 0; i < 2; i++)
                FinalLabProps.Place(pipes, "Ceiling_Run_" + (i + 1), asset,
                    V(runStart + 0.75f + i * 1.50f, axisY - 0.080f, z), Vector3.zero, width: 1.50f,
                    selection: r => r.name.StartsWith(part, StringComparison.Ordinal));
            var upright = FinalLabProps.Place(pipes, "Wall_Riser", asset, V(x, 0.79f, z),
                new Vector3(0, 0, 90), height: axisY - 0.79f - 0.16f,
                selection: r => r.name.StartsWith(part, StringComparison.Ordinal));
            Run(pipes, "Riser_Elbow", pipeMetal, 0.073f, false,
                V(x, upright.Bounds.max.y - 0.022f, z), V(x, axisY, z), V(runStart + 0.045f, axisY, z));
            Run(pipes, "Flanged_Joint", pipeMetal, 0.088f, false, V(joint - 0.043f, axisY, z), V(joint + 0.043f, axisY, z));
            Junction(pipes, "Ceiling_Pipe_Terminal", V(runStart + 3.013f, axisY, z), V(0.16f, 0.19f, 0.285f));
            Junction(pipes, "Riser_Wall_Terminal", V(x, 0.78f, z), V(0.21f, 0.15f, 0.285f));
        }

        private void Junction(Transform group, string name, Vector3 point, Vector3 size) =>
            Block(group, name, point, size, steel);

        private void Run(Transform group, string name, Material surface, float radius, bool clips, params Vector3[] anchors)
        {
            var route = Group(group, name);
            var control = Rounded(anchors, radius < 0.03f ? 0.095f : 0.106f);
            var points = BakePackagePoints(control, radius < 0.03f);
            if (!route.TryGetComponent<MeshFilter>(out var filter)) filter = route.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = StoreMesh(Tube(group.name + "_" + name, route, points, radius));
            if (!route.TryGetComponent<MeshRenderer>(out var renderer)) renderer = route.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surface;
            if (clips)
                for (int i = 1; i < anchors.Length - 1; i++)
                {
                    var target = anchors[i];
                    Vector3 contact = points.OrderBy(p => (p - target).sqrMagnitude).First();
                    Vector3 span = target - contact;
                    var clip = Block(route, "Mount_" + i.ToString("00"), (target + contact) * 0.5f,
                        new Vector3(radius * 2.9f, radius * 2.7f, span.magnitude + radius * 2.6f), steel);
                    clip.rotation = span.sqrMagnitude > 0.00001f ? Quaternion.LookRotation(span) : Quaternion.identity;
                }
            var pointsFolder = Group(route, "Terminals");
            Mark(pointsFolder, "Start", anchors[0]);
            Mark(pointsFolder, "End", anchors[^1]);
            bakedCables++;
        }
    }

    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    internal static List<Vector3> Rounded(Vector3[] points, float trim)
    {
        var result = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Length - 1; i++)
        {
            Vector3 before = points[i - 1] - points[i], after = points[i + 1] - points[i];
            if (before.magnitude < 0.001f || after.magnitude < 0.001f) continue;
            float cut = Mathf.Min(trim, before.magnitude * 0.32f, after.magnitude * 0.32f);
            var first = points[i] + before.normalized * cut;
            var last = points[i] + after.normalized * cut;
            result.Add(first);
            result.Add(Vector3.Lerp(Vector3.Lerp(first, points[i], 0.5f), Vector3.Lerp(points[i], last, 0.5f), 0.5f));
            result.Add(last);
        }
        result.Add(points[^1]);
        return result;
    }

    internal static List<Vector3> BakePackagePoints(List<Vector3> points, bool soft)
    {
        var temporary = new GameObject("Temporary cable package sampler");
        temporary.hideFlags = HideFlags.HideAndDontSave;
        temporary.SetActive(false);
        try
        {
            var tool = temporary.AddComponent<CableProceduralMultipoint>();
            var line = temporary.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            var setup = new SerializedObject(tool);
            setup.FindProperty("pointDensity").floatValue = 360;
            var sections = setup.FindProperty("cableSections");
            sections.arraySize = points.Count - 1;
            var controls = points.Select((point, i) => Mark(temporary.transform, "Point_" + i, point)).ToArray();
            for (int i = 0; i < controls.Length - 1; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                section.FindPropertyRelative("start").objectReferenceValue = controls[i];
                section.FindPropertyRelative("end").objectReferenceValue = controls[i + 1];
                float distance = Vector3.Distance(points[i], points[i + 1]);
                section.FindPropertyRelative("sag").floatValue = soft && distance > 0.30f ? Mathf.Min(0.011f, distance * 0.004f) : 0;
            }
            setup.ApplyModifiedPropertiesWithoutUndo();
            // ExecuteAlways initializes from Start. Call it in Batch Mode, then bake its sampled path
            // into a round mesh and discard the temporary component and its billboard renderer.
            typeof(CableProceduralMultipoint).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(tool, null);
            var samples = new Vector3[line.positionCount];
            line.GetPositions(samples);
            if (samples.Length < 2 || Vector3.Distance(samples[0], points[0]) > 0.002f ||
                Vector3.Distance(samples[^1], points[^1]) > 0.002f)
                throw new InvalidOperationException("Cable package did not sample both terminals.");
            var result = new List<Vector3> { samples[0] };
            for (int i = 1; i < samples.Length - 1; i++)
            {
                bool close = Vector3.Distance(result[^1], samples[i]) < 0.071f;
                var before = (samples[i] - samples[i - 1]).normalized;
                var after = (samples[i + 1] - samples[i]).normalized;
                bool corner = before.sqrMagnitude > 0.1f && after.sqrMagnitude > 0.1f && Vector3.Dot(before, after) < 0.992f;
                if ((corner || !close) && Vector3.Distance(result[^1], samples[i]) > 0.0005f)
                    result.Add(samples[i]);
            }
            if (Vector3.Distance(result[^1], samples[^1]) > 0.0005f) result.Add(samples[^1]);
            return result;
        }
        finally
        {
            Object.DestroyImmediate(temporary);
        }
    }

    internal static Mesh Tube(string name, Transform route, List<Vector3> points, float radius)
    {
        const int sides = 10;
        var vertices = new Vector3[points.Count * sides];
        var normals = new Vector3[vertices.Length];
        var indices = new List<int>((points.Count - 1) * sides * 6);
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
            if (normal.sqrMagnitude < 0.5f)
            {
                Vector3 axis = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < 0.92f ? Vector3.up : Vector3.right;
                normal = Vector3.Cross(tangent, axis).normalized;
            }
            var binormal = Vector3.Cross(tangent, normal);
            for (int side = 0; side < sides; side++)
            {
                float angle = 2 * Mathf.PI * side / sides;
                var radial = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                int index = i * sides + side;
                normals[index] = route.InverseTransformDirection(radial);
                vertices[index] = route.InverseTransformPoint(points[i] + radial * radius);
                if (i == points.Count - 1) continue;
                int right = i * sides + (side + 1) % sides;
                indices.AddRange(new[] { index, right, right + sides, index, right + sides, index + sides });
            }
        }
        var mesh = new Mesh { name = name, vertices = vertices, normals = normals };
        mesh.SetTriangles(indices, 0);
        return mesh;
    }
}
