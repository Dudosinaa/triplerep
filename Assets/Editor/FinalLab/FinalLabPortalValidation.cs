using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FinalLab.Investigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class FinalLabInvestigationValidation
{
    internal static void VerifyPortalScene(GameObject[] roots, Transform architecture)
    {
        var installation = roots.Single(o => o.name == FinalLabPortalBuilder.RootName).transform;
        var names = new[] { "ControlRoom_MetalDoor", "FatherRoom_HeavyMetalDoor" };
        var rooms = new[] { FinalLabPortalBuilder.ControlRoom, FinalLabPortalBuilder.FatherRoom };
        var modelPaths = new[] { FinalLabPortalBuilder.ControlDoorPath, FinalLabPortalBuilder.FatherDoorPath };
        for (int i = 0; i < names.Length; i++)
        {
            var door = installation.Find(names[i]).GetComponent<FinalLabMetalDoor>();
            var opening = FinalLabPortalBuilder.ReadOpening(architecture, rooms[i], "DoorOpening");
            var body = door.Leaf.bounds;
            var renderers = door.Hinge.GetChild(0).GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            Require(renderers.Length > 1 && renderers.All(r => r.sharedMaterials.All(m =>
                m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")) &&
                renderers.All(r => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject) == modelPaths[i]),
                door.name + " uses the requested imported model with valid materials.");
            var surface = renderers.OrderByDescending(r => r.bounds.size.y).First().sharedMaterial;
            Require(surface.HasProperty("baseColorTexture") && surface.GetTexture("baseColorTexture") != null ||
                surface.HasProperty("_BaseMap") && surface.GetTexture("_BaseMap") != null,
                door.name + " preserves its textured surface.");
            Require(door.Leaf.transform == door.Hinge && !door.Leaf.isTrigger &&
                door.Hinge.GetComponent<Rigidbody>().isKinematic &&
                Mathf.Abs(door.Hinge.position.x - body.max.x) < 0.055f &&
                body.min.x > opening.Left && body.max.x < opening.Right &&
                body.min.y >= opening.Bottom && body.max.y < opening.Top &&
                body.size.y > opening.Height * 0.90f && body.size.x > 0.86f &&
                Mathf.Abs(door.SwingAngle - 90) < 1f && Mathf.Abs(door.TravelTime - (i == 0 ? 1 : 1.75f)) < 0.01f,
                door.name + " fits the existing opening and has a right-side moving hinge collider and the requested timing.");
            VerifyHingeMount(door, opening, i == 1);
            VerifySwing(door);
        }
        VerifyRoomJoints(architecture);

        var father = installation.Find("FatherRoom_Observation/Clear protective glass").GetComponent<MeshRenderer>();
        Require(installation.Find("ControlRoom_Observation") == null &&
            architecture.Find(FinalLabPortalBuilder.ControlRoom + "/HallPartition/ControlPartition_Concrete") != null &&
            father.sharedMaterial.name == FinalLabPortalBuilder.FatherGlass,
            "The solid control-room wall and the father-room window are preserved.");
        var mat = father.sharedMaterial;
        Require(mat.shader.name == "Universal Render Pipeline/Lit" && mat.GetFloat("_Surface") == 1 &&
            mat.GetFloat("_Cull") == 0 && mat.GetFloat("_ZWrite") == 0 && mat.renderQueue == (int)RenderQueue.Transparent &&
            mat.GetTexture("_BaseMap") != null && father.GetComponent<BoxCollider>() is { isTrigger: false },
            "The father-room window retains its physical pane and two-sided material.");
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 origin = father.bounds.center + Vector3.forward * (side * 0.43f);
            var hits = Physics.RaycastAll(origin, Vector3.back * side, 0.86f, ~0, QueryTriggerInteraction.Ignore);
            Require(hits.Length == 1 && hits[0].collider.gameObject == father.gameObject,
                "Father-room window has an unobstructed opening on side " + side + ".");
        }
        Require(installation.GetComponentsInChildren<Transform>(true)
            .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "The added portals have no missing scripts.");
    }

    private static void VerifyHingeMount(FinalLabMetalDoor door, FinalLabPortalBuilder.Aperture opening, bool heavy)
    {
        var axis = door.Hinge.position;
        var leaf = door.Leaf.bounds;
        var fixedMount = door.transform.Find("Fixed hinge hardware");
        var movingMount = door.Hinge.Find("Moving hinge leaves");
        var jamb = door.transform.Find(heavy ? "Mounting plate right" : "Right steel jamb").GetComponent<BoxCollider>();
        Require(door.Hinge.name == (heavy ? "FatherRoom_DoorHinge" : "ControlRoom_DoorHinge") &&
            door.Hinge.localScale == Vector3.one && door.Hinge.up == Vector3.up &&
            axis.x > leaf.max.x && axis.z > leaf.max.z && Mathf.Abs(axis.z - jamb.bounds.max.z) < 0.011f &&
            Mathf.Abs(jamb.bounds.max.x - opening.Right) < 0.001f && !jamb.transform.IsChildOf(door.Hinge),
            door.name + " has an external vertical hinge axis directly beside its stationary jamb.");
        var pins = fixedMount.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Through pin").ToArray();
        var knuckles = movingMount.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Moving knuckle").ToArray();
        Require(pins.Length == 3 && knuckles.Length == 3, door.name + " has three complete pin assemblies.");
        foreach (int angle in new[] { 0, 30, 60, 90 })
        {
            door.Hinge.localRotation = Quaternion.Euler(0, angle, 0);
            Require(door.Hinge.position == axis && pins.Zip(knuckles, (p, k) =>
                Vector3.Distance(p.transform.position, k.transform.position)).All(d => d < 0.0001f),
                $"{door.name}: fixed pins and moving knuckles stay coaxial at {angle} degrees.");
        }
        door.Hinge.localRotation = Quaternion.identity;
        Physics.SyncTransforms();
        var samples = new[] { new Vector2(leaf.min.x - 0.006f, leaf.center.y),
            new Vector2(leaf.max.x + 0.004f, leaf.center.y), new Vector2(leaf.center.x, leaf.max.y + 0.003f),
            new Vector2(leaf.center.x, leaf.min.y - 0.002f) };
        foreach (var sample in samples)
        {
            var from = new Vector3(sample.x, sample.y, opening.Hall - 0.03f);
            Require(Physics.RaycastAll(from, Vector3.forward, 0.7f, ~0, QueryTriggerInteraction.Ignore)
                .Any(hit => hit.transform.IsChildOf(door.transform)), door.name + " closed rebate covers the seam at " + sample + ".");
        }
    }

    private static void VerifyRoomJoints(Transform architecture)
    {
        const float tolerance = 0.005f;
        var hall = architecture.Find("03_Main_Laboratory");
        var hallFloor = hall.Find("MainFloor").GetComponent<Renderer>().bounds;
        var ceiling = hall.Find("Ceiling/HallSlab").GetComponent<Renderer>().bounds;
        foreach (bool heavy in new[] { false, true })
        {
            string prefix = heavy ? "Containment" : "Control";
            var room = architecture.Find(heavy ? FinalLabPortalBuilder.FatherRoom : FinalLabPortalBuilder.ControlRoom);
            Bounds Part(string name) => room.Find(name).GetComponent<Renderer>().bounds;
            var floor = Part(prefix + "Floor");
            var roof = Part(prefix + "Ceiling");
            var back = Part("Walls/" + prefix + "Back");
            var west = Part("Walls/" + prefix + "West");
            var east = Part("Walls/" + prefix + "East");
            var lintel = Part($"HallPartition/DoorOpening/{prefix}Partition_DoorOpening_Lintel");
            bool Touch(float a, float b) => Mathf.Abs(a - b) <= tolerance;
            Require(Touch(hallFloor.max.z, floor.min.z) && Touch(hallFloor.max.y, floor.max.y) &&
                Touch(ceiling.max.z, roof.min.z) && Touch(lintel.max.y, ceiling.min.y) && Touch(lintel.max.z, roof.min.z) &&
                new[] { back, east, west }.All(wall => Touch(wall.min.y, floor.max.y) && Touch(wall.max.y, roof.min.y)) &&
                new[] { east, west }.All(wall => Touch(wall.max.z, back.min.z) && Touch(wall.min.z, lintel.max.z)) &&
                Touch(floor.min.x, west.min.x) && Touch(floor.max.x, east.max.x) &&
                Touch(back.min.x, west.min.x) && Touch(back.max.x, east.max.x),
                prefix + " floor, walls, lintel and ceiling meet at the existing planar shell joints.");
        }
    }

    private static void VerifySwing(FinalLabMetalDoor door)
    {
        Vector3 half = door.Leaf.size * 0.5f - Vector3.one * 0.001f;
        for (int step = 0; step <= 90; step++)
        {
            float angle = door.SwingAngle * step / 90f;
            var rotation = Quaternion.Euler(0, angle, 0);
            Vector3 center = door.Hinge.position + door.transform.rotation * (rotation * door.Leaf.center);
            var collision = Physics.OverlapBox(center, half, door.transform.rotation * rotation,
                ~0, QueryTriggerInteraction.Ignore)
                .FirstOrDefault(c => c != door.Leaf && c is not CharacterController);
            if (collision != null)
                throw new InvalidOperationException($"{door.name} intersects {collision.name} at {angle:F1} degrees.");
        }
        Require(true, door.name + " clears stationary geometry throughout its complete swing.");
    }

    private static IEnumerable PlayPortalChecks(FirstPersonPlayer player, Camera camera, CharacterController controller,
        FinalLabInvestigationManager manager, TMP_Text prompt)
    {
        var doors = Object.FindObjectsByType<FinalLabMetalDoor>(FindObjectsSortMode.None).OrderBy(door => door.TravelTime);
        foreach (var door in doors)
        {
            Vector3 plane = door.transform.position;
            Vector3 target = plane + 1.18f * Vector3.up;
            Vector3 hall = plane + Vector3.back * 1.18f;
            Vector3 room = plane + Vector3.forward * 1.02f;
            Aim(player, camera, controller, hall, target);
            for (int f = 0; f < 3; f++) yield return null;
            Require(prompt.text == "E — Открыть дверь" && !door.IsOpen,
                door.name + " offers opening from the hall.");
            Capture(camera, "fit-" + door.name + "-closed-from-hall");
            yield return null;
            AssertClosedPassage(controller, plane.z, Vector3.forward, door);
            Aim(player, camera, controller, hall, target);
            door.SetLocked(true);
            Require(!door.OpenDoor(), door.name + " respects its programmatic lock.");
            door.SetLocked(false);
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = Vector3.Lerp(camera.transform.position, target, 0.54f);
            obstacle.transform.localScale = Vector3.one * 0.32f;
            Physics.SyncTransforms();
            foreach (var ignored in DoorKeyPress()) yield return ignored;
            Require(prompt.text.Length == 0 && !door.IsMoving, door.name + " cannot be operated through an opaque obstacle.");
            obstacle.GetComponent<Collider>().enabled = false;
            Object.Destroy(obstacle);

            Aim(player, camera, controller, hall, target);
            foreach (var ignored in UseDoor(door, true, prompt)) yield return ignored;
            Capture(camera, "fit-" + door.name + "-open-from-hall");
            yield return null;
            controller.Move(Vector3.forward * 2.2f);
            Require(controller.transform.position.z > plane.z + 0.46f, door.name + " admits the player into the room.");
            Aim(player, camera, controller, room, target);
            for (int f = 0; f < 3; f++) yield return null;
            Require(prompt.text == "E — Закрыть дверь", door.name + " exposes the open-door prompt from the other side.");
            foreach (var ignored in UseDoor(door, false, prompt)) yield return ignored;
            AssertClosedPassage(controller, plane.z, Vector3.back, door);
            Aim(player, camera, controller, room, target);
            foreach (var ignored in UseDoor(door, true, prompt)) yield return ignored;
            controller.Move(Vector3.back * 2.1f);
            Require(controller.transform.position.z < plane.z - 0.40f, door.name + " admits the player back into the hall.");
            Aim(player, camera, controller, hall, target);
            foreach (var ignored in UseDoor(door, false, prompt)) yield return ignored;
            Require(manager.State == FinalLabInvestigationManager.ViewState.Walking &&
                manager.Completed == 0 && manager.ActivePoint == null && player.enabled,
                door.name + " consumes E without starting a research action.");
            if (!Application.isBatchMode)
                foreach (var ignored in CaptureHingeAngles(player, camera, controller, door)) yield return ignored;
        }

        if (Application.isBatchMode) yield break;
        foreach (var view in new[]
        {
            ("control-back-joints", new Vector3(-2.22f, 0, 14.65f), new Vector3(-4.50f, 1.36f, 17.58f)),
            ("control-front-joints", new Vector3(-3.10f, 0, 17.02f), new Vector3(-0.77f, 1.36f, 14.19f)),
            ("father-back-joints", new Vector3(1.77f, 0, 14.55f), new Vector3(4.12f, 1.36f, 18.07f)),
            ("father-front-joints", new Vector3(3.67f, 0, 17.47f), new Vector3(-0.23f, 1.36f, 14.21f))
        })
        {
            Aim(player, camera, controller, view.Item2, view.Item3);
            for (int f = 0; f < 4; f++) yield return null;
            Capture(camera, "fit-" + view.Item1);
            yield return null;
        }
    }

    private static IEnumerable CaptureHingeAngles(FirstPersonPlayer player, Camera camera, CharacterController controller, FinalLabMetalDoor door)
    {
        var pin = door.Hinge.position;
        Vector3 floor = new Vector3(pin.x - 0.92f, 0, pin.z + 1.02f);
        Aim(player, camera, controller, floor, pin + Vector3.up * 1.23f);
        foreach (int angle in new[] { 0, 30, 60, 90 })
        {
            door.Hinge.localRotation = Quaternion.Euler(0, angle, 0);
            for (int f = 0; f < 3; f++) yield return null;
            Capture(camera, $"fit-{door.name}-hinges-{angle:D2}");
            yield return null;
        }
        door.Hinge.localRotation = Quaternion.identity;
    }

    private static void AssertClosedPassage(CharacterController controller, float plane, Vector3 direction, FinalLabMetalDoor door)
    {
        controller.Move(direction * 2.15f);
        Require((controller.transform.position.z - plane) * direction.z < -0.12f,
            door.name + " physically stops the character while shut.");
    }

    private static IEnumerable DoorKeyPress()
    {
        Keys(Key.E);
        for (int f = 0; f < 3; f++) yield return null;
        Keys();
        yield return null;
    }

    private static IEnumerable UseDoor(FinalLabMetalDoor door, bool open, TMP_Text prompt)
    {
        double start = EditorApplication.timeSinceStartup;
        foreach (var ignored in DoorKeyPress()) yield return ignored;
        Require(door.IsMoving, door.name + " starts moving on E.");
        while (door.IsMoving) yield return null;
        for (int f = 0; f < 2; f++) yield return null;
        Require(door.IsOpen == open &&
            Mathf.Abs(Mathf.DeltaAngle(door.Hinge.localEulerAngles.y, open ? door.SwingAngle : 0)) < 0.1f &&
            EditorApplication.timeSinceStartup - start >= door.TravelTime * 0.85f &&
            prompt.text == (open ? "E — Закрыть дверь" : "E — Открыть дверь"),
            door.name + (open ? " opens smoothly and offers closing." : " closes smoothly and offers opening."));
    }
}
