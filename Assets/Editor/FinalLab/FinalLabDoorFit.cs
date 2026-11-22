using System;
using System.Linq;
using FinalLab.Investigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class FinalLabPortalBuilder
{
    public static void RepairDoors()
    {
        FinalLabInvestigationSurvey.RequireBatch();
        var scene = EditorSceneManager.OpenScene(FinalLabBuilder.ScenePath);
        var roots = scene.GetRootGameObjects();
        var architecture = roots.Single(o => o.name == "FinalLab_Environment").transform;
        var installation = roots.Single(o => o.name == RootName).transform;
        RepairDoorPair(architecture, installation);
        FinalLabInvestigationValidation.VerifyPortalScene(roots, architecture);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the fitted doors.");
        Debug.Log("FINAL_LAB_DOOR_FIT_SAVED");
    }

    private static void RepairDoorPair(Transform architecture, Transform installation)
    {
        FitHinges(installation.Find("ControlRoom_MetalDoor").GetComponent<FinalLabMetalDoor>(),
            ReadOpening(architecture, ControlRoom, "DoorOpening"), false);
        FitHinges(installation.Find("FatherRoom_HeavyMetalDoor").GetComponent<FinalLabMetalDoor>(),
            ReadOpening(architecture, FatherRoom, "DoorOpening"), true);
        Physics.SyncTransforms();
    }

    private static void FitHinges(FinalLabMetalDoor door, Aperture opening, bool heavy)
    {
        var node = door.transform;
        var hinge = door.Hinge;
        var movingHardware = hinge.Find("Moving hinge leaves");
        if (movingHardware != null) Object.DestroyImmediate(movingHardware.gameObject);
        var fixedHardware = node.Find("Fixed hinge hardware");
        if (fixedHardware != null) Object.DestroyImmediate(fixedHardware.gameObject);
        var stops = node.Find("Rebated door stops");
        if (stops != null) Object.DestroyImmediate(stops.gameObject);

        Transform model = hinge.GetChild(0);
        var body = hinge.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).OrderByDescending(r => r.bounds.size.y).First().bounds;
        var jambLeft = node.Find(heavy ? "Mounting plate left" : "Left steel jamb");
        var jambRight = node.Find(heavy ? "Mounting plate right" : "Right steel jamb");
        var lintel = node.Find(heavy ? "Head backing plate" : "Head seal");
        var steel = jambRight.GetComponent<MeshRenderer>().sharedMaterial;
        float jambFront = jambRight.GetComponent<Renderer>().bounds.min.z;
        Debug.Log($"{door.name}: old axis {hinge.position:F3}, rear face of right jamb {jambRight.GetComponent<Renderer>().bounds.max.z:F3}, leaf {BoundsText(body)}");

        float radius = heavy ? 0.023f : 0.019f;
        float offset = heavy ? 0.017f : 0.014f;
        Vector3 axis = new Vector3(body.max.x + offset, body.min.y, body.max.z + offset);
        Vector3 modelPosition = model.position;
        hinge.name = heavy ? "FatherRoom_DoorHinge" : "ControlRoom_DoorHinge";
        hinge.position = axis;
        model.position = modelPosition;
        door.Leaf.center = hinge.InverseTransformPoint(body.center);
        door.Leaf.size = body.size;

        const float rightClearance = 0.008f;
        float leftInner = body.min.x - (heavy ? 0.016f : 0.014f);
        float rightInner = body.max.x + rightClearance;
        float jambBack = axis.z - 0.004f;
        SetBox(jambLeft, new Vector3(opening.Left, opening.Bottom, jambFront), new Vector3(leftInner, opening.Top, jambBack));
        SetBox(jambRight, new Vector3(rightInner, opening.Bottom, jambFront), new Vector3(opening.Right, opening.Top, jambBack));
        SetBox(lintel, new Vector3(leftInner, body.max.y + 0.007f, jambFront), new Vector3(rightInner, opening.Top, jambBack));
        var threshold = node.Find(heavy ? "Bottom threshold" : "Flush steel threshold");
        if (threshold == null)
        {
            Cube(node, "Flush steel threshold", Vector3.zero, Vector3.one, steel);
            threshold = node.Find("Flush steel threshold");
        }
        SetBox(threshold, new Vector3(leftInner, opening.Bottom, jambFront),
            new Vector3(rightInner, body.min.y - 0.005f, jambBack));

        stops = Group(node, "Rebated door stops", node.position);
        float stopFront = body.min.z - 0.035f;
        float stopBack = body.min.z - 0.005f;
        AddBox(stops, "Left stop", new Vector3(leftInner - 0.005f, opening.Bottom, stopFront),
            new Vector3(body.min.x + 0.020f, body.max.y + 0.007f, stopBack), steel);
        AddBox(stops, "Right stop", new Vector3(body.max.x - 0.020f, opening.Bottom, stopFront),
            new Vector3(rightInner + 0.005f, body.max.y + 0.007f, stopBack), steel);
        AddBox(stops, "Head stop", new Vector3(body.min.x + 0.020f, body.max.y - 0.020f, stopFront),
            new Vector3(body.max.x - 0.020f, body.max.y + 0.012f, stopBack), steel);
        AddBox(stops, "Sill stop", new Vector3(body.min.x + 0.020f, body.min.y - 0.005f, stopFront),
            new Vector3(body.max.x - 0.020f, body.min.y + 0.003f, stopBack), steel);
        // A folded return masks the pin-side light gap in the free space immediately beside the axis.
        AddBox(stops, "Hinge-side frame return", new Vector3(body.max.x - 0.002f, body.min.y - 0.004f, axis.z - 0.005f),
            new Vector3(rightInner + 0.020f, body.max.y + 0.006f, axis.z + 0.005f), steel);
        if (heavy)
        {
            // The wider steel cover follows the fixed jamb into the room, outside the swept leaf.
            AddBox(stops, "Room-side hinge cover", new Vector3(body.max.x + 0.0025f, body.min.y - 0.005f, axis.z + 0.003f),
                new Vector3(rightInner + 0.026f, body.max.y + 0.007f, axis.z + 0.125f), steel);
        }

        fixedHardware = Group(node, "Fixed hinge hardware", axis);
        movingHardware = Group(hinge, "Moving hinge leaves", axis);
        float length = heavy ? 0.052f : 0.042f;
        foreach (float height in new[] { 0.14f, 0.51f, 0.88f })
        {
            Vector3 pin = axis + Vector3.up * (body.size.y * height);
            AddHardware(fixedHardware, "Jamb hinge leaf", PrimitiveType.Cube,
                pin + new Vector3(radius + 0.007f, 0, -radius * 0.42f), new Vector3(radius * 2.8f, length * 3.5f, 0.018f), steel);
            AddHardware(fixedHardware, "Upper fixed knuckle", PrimitiveType.Cylinder, pin + Vector3.up * (length + 0.002f),
                new Vector3(radius * 2, length * 0.5f, radius * 2), steel);
            AddHardware(fixedHardware, "Lower fixed knuckle", PrimitiveType.Cylinder, pin - Vector3.up * (length + 0.002f),
                new Vector3(radius * 2, length * 0.5f, radius * 2), steel);
            AddHardware(fixedHardware, "Through pin", PrimitiveType.Cylinder, pin,
                new Vector3(radius, length * 1.62f, radius), steel);
            AddHardware(movingHardware, "Moving knuckle", PrimitiveType.Cylinder, pin,
                new Vector3(radius * 1.91f, length * 0.5f, radius * 1.91f), steel);
            AddHardware(movingHardware, "Hinge leaf on door", PrimitiveType.Cube,
                pin + new Vector3(-offset - 0.026f, 0, -offset + 0.004f), new Vector3(0.083f, length * 1.3f, 0.018f), steel);
        }
        Debug.Log($"{door.name}: fitted vertical pin at {hinge.position:F3}; jamb rear {jambBack:F3}.");
    }

    private static void SetBox(Transform box, Vector3 min, Vector3 max)
    {
        box.position = (min + max) * 0.5f;
        box.localScale = max - min;
    }

    private static void AddBox(Transform parent, string name, Vector3 min, Vector3 max, Material material) =>
        Cube(parent, name, (min + max) * 0.5f, max - min, material);

    private static void AddHardware(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.position = position;
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }
}
