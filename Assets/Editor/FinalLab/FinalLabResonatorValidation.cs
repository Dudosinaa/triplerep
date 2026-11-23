using System;
using System.Collections;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEngine;

internal static class FinalLabResonatorValidation
{
    public static void Verify(GameObject[] roots)
    {
        var module = roots.Single(root => root.name == FinalLabResonatorBuilder.ModuleName).transform;
        var crate = roots.Single(root => root.name == FinalLabResonatorBuilder.CrateName).transform;
        var environment = roots.Single(root => root.name == "FinalLab_Environment").transform;
        var props = roots.Single(root => root.name == "FinalLab_Props").transform;
        var investigation = roots.Single(root => root.name == "FinalLab_Investigation").transform;
        Check(investigation.Find("04 K-17 investigation") == null && investigation.Find("K-17 wall-mounted cable paths") == null &&
            !investigation.GetComponentsInChildren<InvestigationPoint>(true).Any(p => p.Container), "Legacy crate, scene papers, light and leads are gone.");
        var floor = environment.Find("03_Main_Laboratory/MainFloor").GetComponent<BoxCollider>().bounds;
        var table = BoundsOf(props.Find("04_LabUtilityTable"));
        var body = BoundsOf(module.Find("K17_BaseFrame"));
        Check(Vector3.Dot(module.forward, Vector3.left) > .99f && Mathf.Abs(body.min.y - floor.max.y) < .001f &&
            body.max.x < 4.96f && body.min.z - table.max.z is > .24f and < .7f, "The module stands beside the flasks, on the floor, facing the hall.");
        var panel = module.Find("K17_ControlPanel");
        foreach (string path in new[] { "K17_ModeSelector", "K17_AnalogGauge", "K17_IsolationGauge", "K17_OverloadButton",
                     "K17_OverloadCover", "Breaker bay/K17_KnifeSwitch" })
            Check(panel.Find(path) != null, "Independent component: " + path);
        Check(module.Find("K17_CoilAssembly/K17_Core") != null && module.Find("K17_CoilAssembly").GetComponent<LODGroup>().lodCount == 1,
            "The coil has a separate core and a single stable render level.");
        var caseBox = crate.Find("Case collision").GetComponent<BoxCollider>().bounds;
        var archiveFloor = environment.Find("02_Archive_Corridor/ArchiveFloor").GetComponent<BoxCollider>().bounds;
        var boxes = BoundsOf(props.Find("01_ArchiveCorridor/ArchiveBoxes"));
        Check(caseBox.min.x > .42f && caseBox.max.x < 1.35f && caseBox.min.z > archiveFloor.min.z && caseBox.max.z < boxes.min.z - .055f &&
            Mathf.Abs(caseBox.min.y - archiveFloor.max.y) < .001f && crate.childCount > 6,
            "The packing case is on the right of the corridor with both the stair exit and the cabinet clear.");
        foreach (var root in new[] { module, crate })
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>().Where(r => r.enabled))
                Check(renderer.sharedMaterials.All(mat => mat != null && mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader"),
                    "Material on " + renderer.name, false);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>())
                Check(label.font != null && label.font.HasCharacters(label.text), "Glyph coverage for " + label.text, false);
            Check(root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                root.name + " has no missing scripts.");
        }
        var leads = module.Find("K17_EastWallRuns").Cast<Transform>().Where(t => t.name.EndsWith(" lead", StringComparison.Ordinal)).ToArray();
        Check(leads.Length == 3, "Three independently attached cable leads.");
        foreach (var lead in leads)
        {
            var vertices = lead.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 Center(int offset)
            {
                Vector3 sum = Vector3.zero;
                for (int side = 0; side < 10; side++) sum += vertices[offset + side];
                return lead.TransformPoint(sum / 10f);
            }
            Check(Vector3.Distance(Center(0), lead.Find("Port").position) < .003f &&
                Vector3.Distance(Center(vertices.Length - 10), lead.Find("Distribution terminal").position) < .003f,
                lead.name + " ends inside both glands.");
            for (int offset = 0; offset < vertices.Length; offset += 10)
            {
                var point = Center(offset);
                Check(point.x is > 4.84f and < 4.968f && point.y > .93f && point.y < 2.60f &&
                    !Physics.OverlapSphere(point, .014f, ~0, QueryTriggerInteraction.Ignore).Any(c => c.transform.IsChildOf(environment)),
                    lead.name + " crosses a concrete surface at " + point, false);
            }
        }
        Check(Physics.Raycast(new Vector3(3.15f, 1.125f, module.position.z), Vector3.right, out var hit, 1.4f) && hit.transform.IsChildOf(module),
            "The face is reachable from an open standing position in the hall.");
        Check(!Physics.CapsuleCast(new Vector3(0, .31f, 1.37f), new Vector3(0, 1.53f, 1.37f), .28f,
            Vector3.forward, 2.65f, ~0, QueryTriggerInteraction.Ignore), "An FPS capsule can pass the shipping case in the corridor.");
    }

    private static Bounds BoundsOf(Transform node) => FinalLabPropsSurvey.Bounds(node.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled));

    private static void Check(bool result, string message, bool log = true)
    {
        if (!result) throw new InvalidOperationException(message);
        if (log) Debug.Log("K17_CHECK " + message);
    }
}

public static partial class FinalLabInvestigationValidation
{
    private static IEnumerable PlayResonatorChecks(FirstPersonPlayer player, Camera camera, CharacterController controller)
    {
        var crate = GameObject.Find(FinalLabResonatorBuilder.CrateName).transform;
        var module = GameObject.Find(FinalLabResonatorBuilder.ModuleName).transform;
        Aim(player, camera, controller, new Vector3(-.32f, 0, 1.41f), crate.position + new Vector3(-.30f, .43f, 0));
        for (int f = 0; f < 4; f++) yield return null;
        Capture(camera, "K17-transport-crate");
        yield return null;
        controller.Move(Vector3.forward * 2.38f);
        Require(controller.transform.position.z > 3.70f, "The player walks past the shipping crate from the stairs.");
        if (Application.isBatchMode) yield break;
        foreach (var view in new[]
        {
            ("K17-laboratory", new Vector3(-1.06f, 0, 9.00f), new Vector3(3.95f, 1.23f, 10.62f)),
            ("K17-front", new Vector3(2.76f, 0, 10.94f), module.position + Vector3.up * .88f),
            ("K17-from-flasks", new Vector3(2.67f, 0, 9.55f), module.position + Vector3.up),
            ("K17-side-and-cables", new Vector3(3.18f, 0, 12.52f), new Vector3(4.66f, 1.47f, 11.05f)),
            ("K17-panel", new Vector3(3.49f, 0, 11.25f), new Vector3(4.17f, 1.18f, 11.27f)),
            ("K17-wall-junction", new Vector3(3.45f, 0, 12.08f), new Vector3(4.84f, 2.13f, 11.39f)),
            ("K17-former-site", new Vector3(-2.80f, 0, 6.17f), new Vector3(-4.54f, .66f, 6.25f))
        })
        {
            Aim(player, camera, controller, view.Item2, view.Item3);
            for (int f = 0; f < 4; f++) yield return null;
            Capture(camera, view.Item1);
            yield return null;
        }
    }
}
