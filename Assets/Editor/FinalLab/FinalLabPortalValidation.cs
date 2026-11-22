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
    private static void VerifyPortalScene(GameObject[] roots, Transform architecture)
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
            var renderers = door.Hinge.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            Require(renderers.Length > 1 && renderers.All(r => r.sharedMaterials.All(m =>
                m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")) &&
                renderers.All(r => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject) == modelPaths[i]),
                door.name + " uses the requested imported model with valid materials.");
            var surface = renderers.OrderByDescending(r => r.bounds.size.y).First().sharedMaterial;
            Require(i == 0 ? surface.name == FinalLabPortalBuilder.ControlDoorSteel && surface.GetTexture("_BaseMap") != null
                : surface.GetTexture("baseColorTexture") != null,
                door.name + " uses a textured aged metal surface.");
            Require(door.Leaf.transform == door.Hinge && !door.Leaf.isTrigger &&
                door.Hinge.GetComponent<Rigidbody>().isKinematic &&
                Mathf.Abs(door.Hinge.position.x - body.max.x) < 0.055f &&
                body.min.x > opening.Left && body.max.x < opening.Right &&
                body.min.y >= opening.Bottom && body.max.y < opening.Top &&
                body.size.y > opening.Height * 0.90f && body.size.x > 0.86f &&
                Mathf.Abs(door.SwingAngle - 90) < 1f && Mathf.Abs(door.TravelTime - (i == 0 ? 1 : 1.75f)) < 0.01f,
                door.name + " fits the existing opening and has a right-side moving hinge collider and the requested timing.");
            VerifySwing(door);
        }

        var control = installation.Find("ControlRoom_Observation").GetComponentsInChildren<MeshRenderer>()
            .Where(r => r.name.EndsWith("smoke glass", StringComparison.Ordinal)).ToArray();
        var father = installation.Find("FatherRoom_Observation/Clear protective glass").GetComponent<MeshRenderer>();
        Require(control.Length == 2 && control[0].sharedMaterial == control[1].sharedMaterial &&
            control[0].sharedMaterial.name == FinalLabPortalBuilder.ControlGlass &&
            father.sharedMaterial.name == FinalLabPortalBuilder.FatherGlass &&
            father.sharedMaterial.GetColor("_BaseColor").a < control[0].sharedMaterial.GetColor("_BaseColor").a * 0.4f,
            "The two control panes share smoke tint and the father room uses a different, clear protective material.");
        foreach (var pane in control.Append(father))
        {
            var mat = pane.sharedMaterial;
            Require(mat.shader.name == "Universal Render Pipeline/Lit" && mat.GetFloat("_Surface") == 1 &&
                mat.GetFloat("_Cull") == 0 && mat.GetFloat("_ZWrite") == 0 && mat.renderQueue == (int)RenderQueue.Transparent &&
                mat.GetTexture("_BaseMap") != null && pane.GetComponent<BoxCollider>() is { isTrigger: false },
                pane.name + " uses one collision pane and a double-sided URP transparent surface without depth writes.");
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 origin = pane.bounds.center + Vector3.forward * (side * 0.43f);
                var hits = Physics.RaycastAll(origin, Vector3.back * side, 0.86f, ~0, QueryTriggerInteraction.Ignore);
                Require(hits.Length == 1 && hits[0].collider.gameObject == pane.gameObject,
                    pane.name + " has an unobstructed window opening on side " + side + ".");
            }
        }
        Require(installation.GetComponentsInChildren<Transform>(true)
            .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
            "The added portals have no missing scripts.");
    }

    private static void VerifySwing(FinalLabMetalDoor door)
    {
        Vector3 half = door.Leaf.size * 0.5f - Vector3.one * 0.003f;
        for (int step = 0; step <= 30; step++)
        {
            float angle = door.SwingAngle * step / 30f;
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
            Capture(camera, door.name + "-open-from-hall");
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
        }

        var installation = GameObject.Find(FinalLabPortalBuilder.RootName).transform;
        foreach (var room in new[] { "ControlRoom", "FatherRoom" })
        {
            var window = installation.Find(room + "_Observation").position;
            var door = installation.Find(room == "ControlRoom" ? "ControlRoom_MetalDoor" : "FatherRoom_HeavyMetalDoor").position;
            Vector3 focus = new Vector3((window.x + door.x) * 0.5f, 1.38f, window.z);
            Aim(player, camera, controller, new Vector3(focus.x, 0, window.z - 2.75f), focus);
            for (int f = 0; f < 4; f++) yield return null;
            Capture(camera, room + "-facade");
            yield return null;
            Aim(player, camera, controller, new Vector3(window.x + (room == "ControlRoom" ? 0.38f : 0.12f), 0, window.z - 0.96f),
                new Vector3(window.x + (room == "ControlRoom" ? 0.38f : 0.12f), 1.42f, window.z));
            for (int f = 0; f < 4; f++) yield return null;
            Capture(camera, room + "-glass");
            yield return null;
        }
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
