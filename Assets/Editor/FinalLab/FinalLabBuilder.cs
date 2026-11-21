using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FinalLabBuilder
{
    internal const string ScenePath = "Assets/Level/Scenes/Locations/laba/FinalScene.unity";
    internal const string RootName = "FinalLab_Environment";
    internal const float LandingHeight = 1.64f;
    internal static readonly Vector3 Spawn = new(0, LandingHeight + 0.025f, -4.03f);

    private const string AssetRoot = "Assets/Art/Models/Environment/FinalLab";
    private const string MeshPath = AssetRoot + "/FinalLab_Structure.asset";
    private const float StairTopZ = -3.18f;
    private const float HatchZ = -3.99f;
    private const float ShaftRoof = 3.94f;
    private const float CorridorEntryZ = 1.15f;
    private const float HallFrontZ = 5.8f;
    private const float HallBackZ = 13.8f;
    private const float RearRoomsZ = 14.12f;
    private const float HallHeight = 2.9f;
    private const float RoomHeight = 2.7f;

    [MenuItem("Tools/Final Lab/Build Scene")]
    public static void Build()
    {
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            (!Application.isBatchMode && (active.path != ScenePath || active.isDirty)))
            throw new InvalidOperationException("Open the saved FinalScene in Edit Mode before rebuilding.");

        var stairs = LoadModel("old_wood_stairs");
        var hatch = LoadModel("old_floor_hatch");
        var bunker = LoadModel("realism_bunker_modular");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var lab = new Construction(new FinalLabMaterials(AssetRoot));

        foreach (var item in scene.GetRootGameObjects())
        {
            if (item.name == RootName || (item.name == "Main Camera" && item.GetComponent<Camera>() != null) ||
                (item.name == "Directional Light" && item.GetComponent<Light>()?.type == LightType.Directional))
                Object.DestroyImmediate(item);
        }

        var root = new GameObject(RootName).transform;
        lab.Stairwell(Group(root, "01_Entrance_Stairwell"), stairs, hatch);
        lab.Corridor(Group(root, "02_Archive_Corridor"), bunker);
        lab.MainHall(Group(root, "03_Main_Laboratory"));
        lab.ControlRoom(Group(root, "04_Ivan_ControlRoom"));
        lab.Containment(Group(root, "05_Father_Containment"));
        lab.Lighting(Group(root, "06_Lighting"));
        Player(Group(root, "07_PlayerSpawn"));
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("FinalScene could not be saved.");
        Debug.Log("FINAL_LAB_BUILD: Saved the five connected zones to " + ScenePath);
    }

    private static GameObject LoadModel(string name)
    {
        string path = AssetDatabase.FindAssets(name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Single(p => p.EndsWith("/" + name + ".glb", StringComparison.Ordinal));
        return AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException("Could not import " + path);
    }

    private static Transform Group(Transform parent, string name)
    {
        var item = new GameObject(name).transform;
        item.SetParent(parent, false);
        return item;
    }

    private static void Marker(Transform parent, string name, float x, float y, float z)
    {
        Group(parent, name).localPosition = new Vector3(x, y, z);
    }

    private static void Player(Transform spawn)
    {
        spawn.localPosition = Spawn;
        var player = Group(spawn, "Player");
        player.tag = "Player";
        var motor = player.gameObject.AddComponent<CharacterController>();
        motor.radius = 0.28f;
        motor.height = 1.8f;
        motor.center = Vector3.up * 0.9f;
        motor.stepOffset = 0.24f;
        motor.skinWidth = 0.035f;
        motor.slopeLimit = 48;
        motor.minMoveDistance = 0;

        var view = Group(player, "Main Camera");
        view.tag = "MainCamera";
        view.localPosition = Vector3.up * 1.65f;
        view.localRotation = Quaternion.Euler(15, 0, 0);
        var camera = view.gameObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.026f, 0.029f, 0.033f);
        camera.nearClipPlane = 0.04f;
        camera.farClipPlane = 70;
        camera.fieldOfView = 70;
        view.gameObject.AddComponent<AudioListener>();
        view.gameObject.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        var controller = new SerializedObject(player.gameObject.AddComponent<FirstPersonPlayer>());
        controller.FindProperty("playerCamera").objectReferenceValue = camera;
        controller.FindProperty("initialViewPitch").floatValue = 15;
        controller.FindProperty("walkSpeed").floatValue = 3.1f;
        controller.ApplyModifiedPropertiesWithoutUndo();
    }

    private sealed class Construction
    {
        private readonly FinalLabMaterials materials;
        private readonly Dictionary<string, Mesh> meshes;

        public Construction(FinalLabMaterials materials)
        {
            this.materials = materials;
            meshes = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().ToDictionary(m => m.name);
        }

        public void Stairwell(Transform zone, GameObject stairsAsset, GameObject hatchAsset)
        {
            var walls = Group(zone, "StairwellWalls");
            var roof = Group(zone, "StairwellCeiling");
            var stairs = Group(zone, "Stairs");
            var hatch = Group(zone, "Hatch");
            float wallEnd = CorridorEntryZ - 0.25f;
            const float back = -4.76f;
            const float halfWidth = 0.88f;
            const float outer = 1.14f;

            Box(zone, "UpperLanding", materials.Floor, -halfWidth, halfWidth, LandingHeight - 0.24f, LandingHeight, back, StairTopZ);
            Box(zone, "LowerLanding", materials.Floor, -outer, outer, -0.24f, 0, back - 0.26f, wallEnd);
            Box(zone, "LandingThreshold", materials.Floor, -1.61f, 1.61f, -0.24f, 0, wallEnd, CorridorEntryZ);
            Box(walls, "ShaftWest", materials.Cracked, -outer, -halfWidth, 0, ShaftRoof, back, wallEnd);
            Box(walls, "ShaftEast", materials.Main, halfWidth, outer, 0, ShaftRoof, back, wallEnd);
            Box(walls, "ShaftBack", materials.Main, -outer, outer, 0, ShaftRoof, back - 0.26f, back);
            Box(walls, "ShaftThroatWest", materials.Cracked, -1.61f, -halfWidth, 0, ShaftRoof, wallEnd, CorridorEntryZ);
            Box(walls, "ShaftThroatEast", materials.Cracked, halfWidth, 1.61f, 0, ShaftRoof, wallEnd, CorridorEntryZ);
            Box(walls, "ShaftThroatHead", materials.Main, -halfWidth, halfWidth, RoomHeight, ShaftRoof, wallEnd, CorridorEntryZ);

            // Fit in the room's axes without replacing the glTF root's axis conversion.
            var stairFit = Group(stairs, "StairModelFit");
            GameObject stairModel = Instance(stairsAsset, stairFit);
            Bounds source = ModelBounds(stairModel);
            stairFit.localScale = new Vector3(1.43f / source.size.x,
                LandingHeight / source.size.y, -StairTopZ / source.size.z);
            Bounds fitted = ModelBounds(stairModel);
            stairFit.position += new Vector3(-fitted.center.x, -fitted.min.y, StairTopZ - fitted.min.z);
            FinishStairTreads(stairModel);
            var topTread = stairModel.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.name == "SketchUp.011_material_2_0");
            Box(stairs, "UpperLanding_WoodThreshold", topTread.sharedMaterial, -0.715f, 0.715f,
                LandingHeight - 0.045f, LandingHeight + 0.004f, StairTopZ - 0.07f, StairTopZ + 0.07f);
            var ramp = Group(stairs, "WalkingSurface");
            var rampCollider = ramp.gameObject.AddComponent<MeshCollider>();
            rampCollider.sharedMesh = SaveMesh("Stair_WalkingSurface",
                new[] { new Vector3(-0.715f, 0, 0), new Vector3(0.715f, 0, 0),
                    new Vector3(0.715f, LandingHeight, StairTopZ), new Vector3(-0.715f, LandingHeight, StairTopZ) },
                new[] { 0, 1, 2, 0, 2, 3 });

            GameObject hatchModel = Instance(hatchAsset, hatch);
            // The latch and hinges face the stairwell so the closed lid is legible from below.
            hatchModel.transform.localRotation = Quaternion.AngleAxis(180, Vector3.right) * hatchModel.transform.localRotation;
            Bounds hatchBounds = ModelBounds(hatchModel);
            var hatchScale = 1.42f / Mathf.Max(hatchBounds.size.x, hatchBounds.size.z);
            hatchModel.transform.localScale *= hatchScale;
            hatchBounds = ModelBounds(hatchModel);
            if (hatchBounds.size.y > 0.20f || Mathf.Min(hatchBounds.size.x, hatchBounds.size.z) < 1.39f)
                throw new InvalidOperationException("The imported hatch is not lying horizontally over the ceiling opening.");
            hatchModel.transform.position += new Vector3(-hatchBounds.center.x, ShaftRoof - 0.075f - hatchBounds.min.y,
                HatchZ - hatchBounds.center.z);

            // Solid frame borders meet the hatch lip, leaving an actual opening in the slab.
            const float hx = 0.64f;
            const float hz = 0.64f;
            const float lip = 0.24f;
            Box(roof, "HatchRoofWest", materials.Ceiling, -outer, -hx, ShaftRoof, ShaftRoof + lip, back - 0.26f, CorridorEntryZ);
            Box(roof, "HatchRoofEast", materials.Ceiling, hx, outer, ShaftRoof, ShaftRoof + lip, back - 0.26f, CorridorEntryZ);
            Box(roof, "HatchRoofRear", materials.Ceiling, -hx, hx, ShaftRoof, ShaftRoof + lip, back - 0.26f, HatchZ - hz);
            Box(roof, "HatchRoofForward", materials.Ceiling, -hx, hx, ShaftRoof, ShaftRoof + lip, HatchZ + hz, CorridorEntryZ);
            Box(roof, "ThroatRoofWest", materials.Ceiling, -1.61f, -outer, ShaftRoof, ShaftRoof + lip, wallEnd, CorridorEntryZ);
            Box(roof, "ThroatRoofEast", materials.Ceiling, outer, 1.61f, ShaftRoof, ShaftRoof + lip, wallEnd, CorridorEntryZ);
            var hatchCollider = hatch.gameObject.AddComponent<BoxCollider>();
            hatchCollider.center = new Vector3(0, ShaftRoof - 0.025f, HatchZ);
            hatchCollider.size = new Vector3(2 * hx, 0.10f, 2 * hz);
        }

        private static void FinishStairTreads(GameObject stairModel)
        {
            var modelRenderers = stairModel.GetComponentsInChildren<MeshRenderer>();
            // These two source overlays occupy the same plane as the textured treads.
            foreach (var overlay in modelRenderers.Where(r => r.name == "SketchUp.007_material_0_0" ||
                         r.name == "SketchUp.004_material_0_0"))
                overlay.enabled = false;

            var firstTread = modelRenderers.Single(r => r.name == "SketchUp.005_material_4_0");
            var bottom = Object.Instantiate(firstTread.gameObject, firstTread.transform.parent, false);
            bottom.name = "Bottom_Wood_Tread";
            bottom.transform.position += new Vector3(0, -firstTread.bounds.min.y, firstTread.bounds.size.z);
        }

        public void Corridor(Transform zone, GameObject bunker)
        {
            var walls = Group(zone, "Walls");
            Box(zone, "ArchiveFloor", materials.Floor, -1.61f, 1.61f, -0.24f, 0, CorridorEntryZ, 5.5f);
            Box(zone, "ArchiveCeiling", materials.Ceiling, -1.61f, 1.61f, RoomHeight, RoomHeight + 0.24f, CorridorEntryZ, 5.5f);
            Box(walls, "ArchiveWest", materials.Cracked, -1.61f, -1.35f, 0, RoomHeight, CorridorEntryZ, 5.5f);
            Box(walls, "ArchiveEast", materials.Cracked, 1.35f, 1.61f, 0, RoomHeight, CorridorEntryZ, 5.5f);
            Marker(zone, "ArchiveWall_Reserved", -1.35f, 1.45f, 3.35f);
            var door = Group(zone, "EntranceOpening");
            ArchivePortal(door, bunker);
        }

        private void ArchivePortal(Transform parent, GameObject bunker)
        {
            const float width = 1.46f;
            const float originalClearWidth = 1.75f;
            const float front = 5.5f;
            float scale = width / originalClearWidth;
            var module = bunker.GetComponentsInChildren<MeshRenderer>()
                .Single(r => r.name == "wall.046_Wall1.001_0");
            var bounds = module.GetComponent<MeshFilter>().sharedMesh.bounds;
            var sourceOrigin = module.transform.TransformPoint(bounds.center);
            var converted = Group(parent, "Bunker_Doorway_Module");
            converted.localPosition = new Vector3(0, 0, front);
            converted.localScale = new Vector3(scale, 1, 1);
            var arch = Group(converted, "wall.046");
            // Fit the original solid throat by its imported bounds, keeping the original depth and lintel height.
            const float originalWidth = 3f;
            const float halfDepth = 0.15f;
            arch.localPosition = module.transform.position - sourceOrigin + new Vector3(0, 1.5f, halfDepth);
            arch.localRotation = module.transform.rotation;
            arch.localScale = module.transform.lossyScale;
            arch.gameObject.AddComponent<MeshFilter>().sharedMesh = module.GetComponent<MeshFilter>().sharedMesh;
            arch.gameObject.AddComponent<MeshRenderer>().sharedMaterials = module.sharedMaterials;

            float halfModule = originalWidth * scale / 2;
            float opening = width / 2;
            Box(parent, "ArchiveWestInfill", materials.Cracked, -1.35f, -halfModule, 0, HallHeight, front, HallFrontZ);
            Box(parent, "ArchiveEastInfill", materials.Cracked, halfModule, 1.35f, 0, HallHeight, front, HallFrontZ);
            CollisionBox(parent, "ArchiveWestJamb", -halfModule, -opening, 0, HallHeight, front, HallFrontZ);
            CollisionBox(parent, "ArchiveEastJamb", opening, halfModule, 0, HallHeight, front, HallFrontZ);
            CollisionBox(parent, "ArchiveLintel", -opening, opening, 2.2f, HallHeight, front, HallFrontZ);
            Liner(parent, "ArchiveDoor", new Opening("Entry", -opening, opening, 0, 2.2f), front + 0.038f, HallFrontZ - 0.038f);
        }

        public void MainHall(Transform zone)
        {
            var walls = Group(zone, "Walls");
            Box(zone, "MainFloor", materials.Floor, -5.26f, 5.26f, -0.24f, 0, 5.5f, RearRoomsZ);
            Box(walls, "HallWest", materials.Main, -5.26f, -5, 0, HallHeight, 5.5f, RearRoomsZ);
            Box(walls, "HallEast", materials.Main, 5, 5.26f, 0, HallHeight, 5.5f, RearRoomsZ);
            Box(walls, "HallEntryWest", materials.Main, -5, -1.35f, 0, HallHeight, 5.5f, HallFrontZ);
            Box(walls, "HallEntryEast", materials.Main, 1.35f, 5, 0, HallHeight, 5.5f, HallFrontZ);
            Box(walls, "HallRearWest", materials.Main, -5, -4.95f, 0, HallHeight, HallBackZ, RearRoomsZ);
            Box(walls, "RearPartitionPier", materials.Main, -0.69f, -0.28f, 0, HallHeight, HallBackZ, RearRoomsZ);
            Box(walls, "HallRearEast", materials.Main, 4.48f, 5, 0, HallHeight, HallBackZ, RearRoomsZ);
            var ceiling = Group(zone, "Ceiling");
            Box(ceiling, "HallSlab", materials.Ceiling, -5.26f, 5.26f, HallHeight, HallHeight + 0.25f, 5.5f, RearRoomsZ);
            Box(ceiling, "HallCrossbeamSouth", materials.Main, -5, 5, 2.72f, HallHeight, 8.05f, 8.31f);
            Box(ceiling, "HallCrossbeamNorth", materials.Main, -5, 5, 2.72f, HallHeight, 11.53f, 11.79f);

            var reserves = Group(zone, "ReservedZones");
            Marker(reserves, "Zone_02_FatherDesk", -3.3f, 0, 8.45f);
            Marker(reserves, "Zone_03_IvanDesk", 3.3f, 0, 8.45f);
            Marker(reserves, "ExperimentalInstallation_Reserved", -0.2f, 0, 10.1f);
            Marker(reserves, "Zone_04_K17", -0.42f, 0, 12.88f);
            Marker(reserves, "Zone_05_Recorder", 2.12f, 0, 12.95f);
        }

        public void ControlRoom(Transform zone)
        {
            RoomShell(zone, "Control", -4.69f, -0.69f, 17.62f, -4.95f, -0.485f);
            Portal(Group(zone, "HallPartition"), "ControlPartition", -4.95f, -0.69f, HallBackZ, RearRoomsZ, HallHeight,
                materials.Main, new Opening("ObservationWindowOpening", -4.30f, -2.62f, 1.08f, 2.18f),
                new Opening("DoorOpening", -2.12f, -0.92f, 0, 2.18f));
            Marker(zone, "ControlConsole_Reserved", -3.25f, 0, 16.48f);
        }

        public void Containment(Transform zone)
        {
            RoomShell(zone, "Containment", -0.28f, 4.22f, 18.12f, -0.485f, 4.48f);
            Portal(Group(zone, "HallPartition"), "ContainmentPartition", -0.28f, 4.48f, HallBackZ, RearRoomsZ, HallHeight,
                materials.Main, new Opening("DoorOpening", 0.29f, 1.55f, 0, 2.24f),
                new Opening("ObservationWindowOpening", 2.22f, 4.04f, 1.09f, 2.16f));
            Marker(zone, "MedicalBed_Reserved", 2.63f, 0, 16.37f);
        }

        private void RoomShell(Transform parent, string prefix, float x0, float x1, float back, float outerLeft, float outerRight)
        {
            const float wall = 0.26f;
            var walls = Group(parent, "Walls");
            Box(parent, prefix + "Floor", materials.Floor, outerLeft, outerRight, -0.24f, 0, RearRoomsZ, back + wall);
            Box(parent, prefix + "Ceiling", materials.Ceiling, outerLeft, outerRight, RoomHeight, RoomHeight + 0.24f, RearRoomsZ, back + wall);
            Box(walls, prefix + "West", materials.Main, outerLeft, x0, 0, RoomHeight, RearRoomsZ, back);
            Box(walls, prefix + "East", materials.Main, x1, outerRight, 0, RoomHeight, RearRoomsZ, back);
            Box(walls, prefix + "Back", materials.Main, outerLeft, outerRight, 0, RoomHeight, back, back + wall);
        }

        private readonly struct Opening
        {
            public readonly string Name;
            public readonly float Left, Right, Bottom, Top;

            public Opening(string name, float left, float right, float bottom, float top)
            {
                Name = name;
                Left = left;
                Right = right;
                Bottom = bottom;
                Top = top;
            }
        }

        private void Portal(Transform parent, string name, float minX, float maxX, float z0, float z1, float height,
            Material concrete, params Opening[] openings)
        {
            float cursor = minX;
            foreach (var opening in openings)
            {
                string prefix = name + "_" + opening.Name;
                var part = Group(parent, opening.Name);
                Box(parent, prefix + "_Pier", concrete, cursor, opening.Left, 0, height, z0, z1);
                Box(part, prefix + "_Lintel", concrete, opening.Left, opening.Right, opening.Top, height, z0, z1);
                if (opening.Bottom > 0)
                    Box(part, prefix + "_Sill", concrete, opening.Left, opening.Right, 0, opening.Bottom, z0, z1);
                Liner(part, prefix, opening, z0 + 0.038f, z1 - 0.038f);
                cursor = opening.Right;
            }
            Box(parent, name + "_EndPier", concrete, cursor, maxX, 0, height, z0, z1);
        }

        private void Liner(Transform parent, string name, Opening opening, float minZ, float maxZ)
        {
            const float gauge = 0.025f;
            float left = opening.Left + gauge;
            float right = opening.Right - gauge;
            Box(parent, name + "_LinerLeft", materials.Steel, opening.Left, left, opening.Bottom, opening.Top, minZ, maxZ);
            Box(parent, name + "_LinerRight", materials.Steel, right, opening.Right, opening.Bottom, opening.Top, minZ, maxZ);
            Box(parent, name + "_LinerTop", materials.Steel, left, right, opening.Top - gauge, opening.Top, minZ, maxZ);
            if (opening.Bottom > 0)
                Box(parent, name + "_LinerSill", materials.Steel, left, right, opening.Bottom, opening.Bottom + gauge, minZ, maxZ);
        }

        public void Lighting(Transform zone)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.19f, 0.205f, 0.22f);
            RenderSettings.reflectionIntensity = 0.12f;
            RenderSettings.fog = false;

            LightAt(zone, "Stair_Warm", new Vector3(0.62f, 3.27f, -2.12f),
                new Vector3(-0.6f, 2.67f, -2.62f), new Color(1, 0.79f, 0.61f), 4.4f, 6f, 157, 0.67f);
            LightAt(zone, "Archive_WallWash", new Vector3(0.79f, 2.57f, 3.32f),
                new Vector3(-1.35f, 1.38f, 3.32f), new Color(0.94f, 0.92f, 0.79f), 5.4f, 5f, 109);
            LightAt(zone, "Hall_South", new Vector3(-2.05f, 2.65f, 8.17f),
                new Vector3(-2.1f, 0, 8.9f), new Color(0.92f, 0.97f, 1), 9f, 7.2f, 148);
            LightAt(zone, "Hall_Center", new Vector3(1.83f, 2.65f, 11.65f),
                new Vector3(0.9f, 0, 10.63f), new Color(0.89f, 0.97f, 0.91f), 11f, 7.4f, 148);
            LightAt(zone, "Hall_Far", new Vector3(-2.6f, 2.67f, 12.2f),
                new Vector3(-1.8f, 1.05f, 13.8f), new Color(0.97f, 0.88f, 0.72f), 4.2f, 5.5f, 141);
            LightAt(zone, "Control_Cool", new Vector3(-2.96f, 2.63f, 15.95f),
                new Vector3(-2.9f, 0.35f, 16.2f), new Color(0.73f, 0.86f, 1), 7.1f, 5.5f, 145);
            LightAt(zone, "Containment_White", new Vector3(2.18f, 2.63f, 16.34f),
                new Vector3(2.55f, 0.5f, 16.63f), new Color(0.89f, 0.95f, 1), 7.4f, 5.4f, 148);
            LightAt(zone, "Containment_EntryAccent", new Vector3(1.77f, 2.45f, 13.72f),
                new Vector3(1.5f, 1.1f, 13.76f), new Color(1, 0.30f, 0.20f), 0.86f, 2.1f, 100);

            Fixture(zone, "ArchiveFixture", 0.79f, RoomHeight, 3.32f, 0.65f);
            Fixture(zone, "HallSouthFixture", -2.05f, 2.72f, 8.17f, 1.18f);
            Fixture(zone, "HallCenterFixture", 1.83f, 2.72f, 11.65f, 1.18f);
            Fixture(zone, "HallFarFixture", -2.6f, HallHeight, 12.2f, 0.93f);
            Fixture(zone, "ControlFixture", -2.96f, RoomHeight, 15.95f, 0.91f);
            Fixture(zone, "ContainmentFixture", 2.18f, RoomHeight, 16.34f, 0.91f);
            Box(zone, "ShaftLampHousing", materials.Steel, 0.759f, 0.88f, 3.20f, 3.43f, -2.29f, -1.95f);
            Box(zone, "ShaftLampLens", materials.WarmDiffuser, 0.735f, 0.759f, 3.225f, 3.405f, -2.26f, -1.98f);
            Box(zone, "EntryPilotHousing", materials.Steel, 1.68f, 1.86f, 2.40f, 2.52f, 13.75f, 13.8f);
            Box(zone, "EntryPilotLens", materials.RedDiffuser, 1.70f, 1.84f, 2.42f, 2.50f, 13.73f, 13.75f);
        }

        private void Fixture(Transform parent, string name, float x, float y, float z, float width)
        {
            Box(parent, name + "_Housing", materials.Steel, x - width / 2, x + width / 2, y - 0.04f, y, z - 0.09f, z + 0.09f);
            Box(parent, name + "_Diffuser", materials.Diffuser, x - width / 2 + 0.035f, x + width / 2 - 0.035f,
                y - 0.062f, y - 0.04f, z - 0.063f, z + 0.063f);
        }

        private static void LightAt(Transform parent, string name, Vector3 position, Vector3 target,
            Color color, float intensity, float range, float angle, float innerFraction = 0.46f)
        {
            var item = Group(parent, name);
            item.position = position;
            item.rotation = Quaternion.LookRotation(target - position);
            var light = item.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * innerFraction;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.83f;
            light.shadowBias = 0.045f;
            light.shadowNormalBias = 0.26f;
            var urp = new SerializedObject(light.GetUniversalAdditionalLightData());
            urp.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
                UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
            urp.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Instance(GameObject asset, Transform parent)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            return model;
        }

        private static Bounds ModelBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException("Model has no mesh renderers: " + model.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private void Box(Transform parent, string name, Material material, float minX, float maxX,
            float minY, float maxY, float minZ, float maxZ)
        {
            var collider = CollisionBox(parent, name, minX, maxX, minY, maxY, minZ, maxZ);
            var item = collider.transform;
            var vertices = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 v = Mathf.Abs(normal.y) > 0 ? Vector3.forward : Vector3.up;
                Vector3 u = Vector3.Cross(v, normal);
                Vector3 half = collider.size * 0.5f;
                Vector3 c = Vector3.Scale(normal, half);
                Vector3 h = u * Mathf.Abs(Vector3.Dot(u, half));
                Vector3 k = v * Mathf.Abs(Vector3.Dot(v, half));
                int first = vertices.Count;
                foreach (var vertex in new[] { c - h - k, c + h - k, c + h + k, c - h + k })
                {
                    Vector3 world = item.position + vertex;
                    vertices.Add(vertex);
                    uv.Add(new Vector2(Vector3.Dot(world, u), Vector3.Dot(world, v)) / 2.25f);
                }
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var mesh = SaveMesh(name, vertices.ToArray(), triangles.ToArray(), uv.ToArray());
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static BoxCollider CollisionBox(Transform parent, string name, float minX, float maxX,
            float minY, float maxY, float minZ, float maxZ)
        {
            var item = Group(parent, name);
            var min = new Vector3(minX, minY, minZ);
            var max = new Vector3(maxX, maxY, maxZ);
            if (max.x <= min.x || max.y <= min.y || max.z <= min.z)
                throw new ArgumentException("Invalid bounds for " + name);
            item.localPosition = (min + max) * 0.5f;
            var collider = item.gameObject.AddComponent<BoxCollider>();
            collider.size = max - min;
            return collider;
        }

        private Mesh SaveMesh(string name, Vector3[] vertices, int[] triangles, Vector2[] uv = null)
        {
            bool created = !meshes.TryGetValue(name, out Mesh mesh);
            if (created)
                mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            if (uv != null)
                mesh.uv = uv;
            mesh.RecalculateNormals();
            if (uv != null)
                mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (created)
            {
                if (meshes.Count == 0)
                    AssetDatabase.CreateAsset(mesh, MeshPath);
                else
                    AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                meshes.Add(name, mesh);
            }
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
