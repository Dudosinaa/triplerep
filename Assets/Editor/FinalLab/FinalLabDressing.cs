using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class FinalLabProps
{
    private sealed partial class Dressing
    {
        private readonly Transform root;
        private readonly Layout layout;
        private readonly Material steel, rubber, signal, pipeMetal, glass, pipette, liquid;
        private readonly Transform markers;
        private Prop ivanDesk, tesla, generator, panel, bed, medicalLamp;

        public Dressing(Transform root, Layout layout)
        {
            this.root = root;
            this.layout = layout;
            steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "FinalLab_OxidizedSteel.mat");
            rubber = Lit("FinalLab_Cable_Rubber", new Color(0.105f, 0.095f, 0.081f), 0.08f, 0.31f);
            signal = Lit("FinalLab_Cable_FadedBrown", new Color(0.22f, 0.146f, 0.097f), 0.03f, 0.37f);
            pipeMetal = Lit("FinalLab_Pipe_Elbows", new Color(0.31f, 0.335f, 0.31f), 0.42f, 0.29f);
            glass = Transparent("FinalLab_Flask_Glass", new Color(0.73f, 0.85f, 0.82f, 0.23f), 0.88f);
            glass.renderQueue = 3010;
            liquid = Transparent("FinalLab_Flask_Liquid", new Color(0.48f, 0.44f, 0.24f, 0.58f), 0.71f);
            var pipetteAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Glassware + "Pipette.prefab");
            pipette = Lit("FinalLab_Pipette", new Color(0.72f, 0.71f, 0.64f), 0.03f, 0.39f,
                pipetteAsset.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            markers = Group(root, "10_InspectionMarkers");
        }

        public void Place()
        {
            Archive();
            Ivan();
            Experiment();
            Utility();
            Storage();
            Control();
            Containment();
            Recorder();
        }

        private void Archive()
        {
            Room room = layout.Archive;
            var zone = Group(root, "01_ArchiveCorridor");
            var cabinet = FurnitureAt(zone, "ArchiveCabinet", "old_filing_cabinet", room.Right - 0.29f, room.Floor,
                room.Far - 0.68f, -90, width: 0.47f);
            Collider(cabinet);
            var boxes = PlaceBoxes(zone, "ArchiveBoxes", new Vector3(room.Right - 0.30f, room.Floor, room.Far - 1.79f),
                8, 0.44f, 0.67f);
            Collider(boxes);
            var wall = layout.Architecture.Find("02_Archive_Corridor/ArchiveWall_Reserved");
            Mark(markers, "Inspection_01_Archive_Reserved", wall.position);
        }

        private void Ivan()
        {
            Room room = layout.Hall;
            var zone = Group(root, "02_IvanWorkspace");
            float x = room.Left + 0.43f, z = room.Far - 2.50f;
            ivanDesk = FurnitureAt(zone, "Desk", "soviet_old_table", x, room.Floor, z, 180, 0.77f);
            Collider(ivanDesk);
            var chair = FurnitureAt(zone, "Chair", "old_chair", x + 1.11f, room.Floor, z - 0.15f, 81, 0.91f);
            Collider(chair);
            DeskLamp(zone, "DeskLamp", ivanDesk, x - 0.10f, z - 0.59f, 20, new Vector3(x + 0.05f, 0.77f, z - 0.21f));
            // The scan's shallow axis is tilted in the source. Level its equipment chassis in the fit transform.
            var leveled = Quaternion.Euler(0, 23, 0) * Quaternion.FromToRotation(new Vector3(0.063f, 0.725f, 0.686f), Vector3.up);
            FinalLabProps.Place(zone, "OldSchoolEquipment", Machines + "labratory_old-school_equipment.glb",
                Surface(ivanDesk, x - 0.075f, z + 0.44f), leveled.eulerAngles, 0.29f, 0.42f, 0.45f, exactBounds: true);
            Flask(zone, "SmallSampleFlask", 1, Surface(ivanDesk, x + 0.12f, z + 0.63f), 0.105f);
            Mark(markers, "Inspection_03_IvanDesk_Reserved", Surface(ivanDesk, x + 0.15f, z - 0.01f));
        }

        private void Experiment()
        {
            Room room = layout.Hall;
            var zone = Group(root, "03_ExperimentArea");
            float x = room.Center + 0.35f, z = room.Far - 3.25f;
            Block(zone, "EquipmentBase", new Vector3(x, room.Floor + 0.036f, z), new Vector3(1.26f, 0.072f, 1.26f),
                steel, solid: true);
            tesla = PlaceModel(zone, "TeslaCoil", Machines, "tesla_coil_old", new Vector3(x, room.Floor + 0.072f, z),
                -10, 2.08f);
            var alloy = Lit("FinalLab_Coil_OxidizedAlloy", new Color(0.44f, 0.45f, 0.42f), 0.54f, 0.41f);
            foreach (var renderer in tesla.Renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name == "1M.001" ? alloy : m).ToArray();
            Collider(tesla);
            generator = PlaceModel(zone, "Generator", Machines, "abandoned_soviet_generator_game_ready_ue5_prop",
                new Vector3(room.Center - 0.86f, room.Floor, layout.NorthBeam.center.z + 0.35f), 90, 0.91f);
            Collider(generator);
            Spot(zone, "Coil_WorkLight", new Vector3(x + 0.85f, 2.70f, z - 1.10f), new Vector3(x, 1.22f, z),
                new Color(0.78f, 0.91f, 0.88f), 2.4f, 3.5f, 63);
            Mark(markers, "Inspection_04_K17_Reserved", new Vector3(room.Right - 1.51f, room.Floor, room.Far - 1.48f));
        }

        private void Utility()
        {
            Room room = layout.Hall;
            var zone = Group(root, "04_LabUtilityTable");
            float x = room.Right - 0.43f, z = room.Near + 3.55f;
            var table = FurnitureAt(zone, "Table", "old_table (1)", x, room.Floor, z, -90, 0.78f);
            Collider(table, tabletop: true);
            var bottles = Group(zone, "Flasks");
            Flask(bottles, "StopperedVial", 1, Surface(table, x + 0.04f, z - 0.38f), 0.155f);
            Flask(bottles, "MeasuringFlask", 3, Surface(table, x + 0.17f, z - 0.17f), 0.245f);
            Flask(bottles, "Erlenmeyer", 4, Surface(table, x + 0.13f, z + 0.25f), 0.20f);
            Flask(bottles, "RoundBottomFlask", 5, Surface(table, x - 0.035f, z + 0.31f), 0.135f);
            var tools = Group(zone, "Pipettes");
            Pipette(tools, "Pipette", "Pipette", Surface(table, x - 0.13f, z - 0.07f), -79);
            Pipette(tools, "MultiPipette", "Pipette_Multy", Surface(table, x - 0.16f, z + 0.16f), -22);
            var funnel = FinalLabProps.Place(zone, "Funnel", "Assets/FreeLabAssets/Models/Funnel.fbx",
                Surface(table, x + 0.17f, z + 0.44f), new Vector3(180, 0, 0), 0.10f, exactBounds: true);
            SetMaterial(funnel, glass, noShadows: true);
            Spot(zone, "Glassware_CeilingWash", new Vector3(x - 0.39f, room.Ceiling - 0.16f, z - 0.29f),
                new Vector3(x + 0.04f, table.Bounds.max.y, z + 0.05f), new Color(0.89f, 0.95f, 0.85f), 2.38f, 3.0f, 63);
        }

        private void Storage()
        {
            Room room = layout.Hall;
            var zone = Group(root, "05_StorageArea");
            float x = room.Left + 0.34f, z = room.Near + 1.66f;
            var shelf = FurnitureAt(zone, "MetalShelf", "rusted_metal_shelf", x, room.Floor, z, 0, 1.86f);
            Collider(shelf);
            var lower = Surface(shelf, x, z + 0.05f, 0.25f, 0.62f);
            PlaceBoxes(zone, "ShelfBoxes", lower, 0, 0.32f, 0.54f);
            Flask(zone, "ShelfFlask", 6, Surface(shelf, x + 0.035f, z - 0.21f, 0.66f, 1.30f), 0.133f);
            Flask(zone, "ShelfMeasuringVial", 2, Surface(shelf, x - 0.03f, z + 0.18f, 0.66f, 1.30f), 0.185f);
            var boxes = PlaceBoxes(zone, "SupplyBoxes", new Vector3(room.Left + 0.53f, room.Floor, z + 1.10f),
                -12, 0.65f, 0.68f);
            Collider(boxes);
        }

        private void Control()
        {
            Room room = layout.Control;
            var zone = Group(root, "06_ControlRoom");
            float x = room.Left + 1.16f, z = room.Far - 0.51f;
            var desk = FurnitureAt(zone, "FatherDesk", "old_wooden_table (1)", x, room.Floor, z, 90, 0.75f);
            Collider(desk, tabletop: true);
            var chair = FurnitureAt(zone, "Chair", "old_chair (1)", x - 0.02f, room.Floor, z - 1.06f, 8, 0.83f);
            Collider(chair);
            PlaceModel(zone, "Oscilloscope", Machines, "laboratory_oscilloscope_20mhz",
                Surface(desk, x + 0.13f, z + 0.13f), 172, 0.30f, 0.49f, 0.50f);
            DeskLamp(zone, "DeskLamp", desk, x - 0.48f, z + 0.14f, 157, new Vector3(x - 0.10f, 0.76f, z - 0.12f));
            panel = FurnitureAt(zone, "ElectricalPanel", "rusty_abandoned_electrical_panel_game_readyue5", room.Right - 0.38f,
                room.Floor + 1.01f, room.Near + 1.65f, 0, 0.69f);
            Collider(panel);
            Mark(markers, "Inspection_02_FatherDesk_Reserved", Surface(desk, x + 0.08f, z - 0.22f));
            Mark(markers, "Inspection_02_HallAccess_Reserved", new Vector3(layout.ControlWindow.center.x, 1.30f, layout.Hall.Far - 0.07f));
            Pipes(zone);
        }

        private void Containment()
        {
            Room room = layout.Containment;
            var zone = Group(root, "07_FatherContainment");
            float x = room.Right - 1.65f, z = room.Far - 1.37f;
            bed = FinalLabProps.Place(zone, "BedWithBedding", Hospital + "/Prefab/P_BedBedding.prefab",
                new Vector3(x, room.Floor, z), new Vector3(0, 52, 0), height: 1.24f);
            Collider(bed);
            var crate = FinalLabProps.Place(zone, "MedicalBox", Hospital + "/Prefab/P_Med_box_01.prefab",
                new Vector3(room.Left + 0.99f, room.Floor, room.Far - 0.43f), new Vector3(0, -8, 0), 0.52f);
            Collider(crate);
            medicalLamp = FinalLabProps.Place(zone, "MedicalLamp", Hospital + "/Prefab/P_Lamp.prefab",
                new Vector3(x - 0.14f, room.Ceiling - 0.072f, z + 0.54f), Vector3.zero, width: 1.08f);
            Spot(zone, "Bed_WhitePool", new Vector3(x - 0.12f, room.Ceiling - 0.09f, z + 0.55f),
                new Vector3(x, 0.73f, z + 0.27f), new Color(0.81f, 0.90f, 1f), 2.1f, 3.2f, 72);
        }

        private void Recorder()
        {
            Room room = layout.Hall;
            var zone = Group(root, "08_TapeRecorder");
            float x = layout.ContainmentDoor.max.x + 0.48f, z = room.Far - 0.36f;
            var table = FurnitureAt(zone, "RecorderStand", "old_table", x, room.Floor, z, 180, 0.72f);
            Collider(table, tabletop: true);
            var tape = FurnitureAt(zone, "RecorderAndCassettes", "tape_recorder_with_cassettes", x,
                Surface(table, x, z).y, z, 164, width: 0.48f, depth: 0.39f);
            Spot(zone, "Recorder_WallWash", new Vector3(x + 0.14f, 2.28f, z - 0.14f), tape.Bounds.center,
                new Color(1f, 0.83f, 0.63f), 1.36f, 2.25f, 60);
            Mark(markers, "Inspection_05_Recorder_Reserved", tape.Bounds.center);
        }

        private void DeskLamp(Transform zone, string name, Prop desk, float x, float z, float yaw, Vector3 target)
        {
            var lamp = PlaceModel(zone, name, Furniture, "old_table_lamp_v01", Surface(desk, x, z), yaw, 0.30f);
            var shade = lamp.Renderers.OrderByDescending(r => r.bounds.max.y).First().bounds;
            Vector3 emitter = new(shade.center.x, shade.min.y - 0.012f, shade.center.z);
            Spot(zone, name + "_WarmPool", emitter, target, new Color(1f, 0.72f, 0.43f), 0.66f, 1.49f, 109);
        }

        private void Flask(Transform parent, string name, int type, Vector3 point, float height)
        {
            var item = FinalLabProps.Place(parent, name, Glassware + $"Flask_{type:00}.prefab", point, Vector3.zero, height);
            SetMaterial(item, glass, noShadows: true);
            if (type == 4 || type == 5)
                foreach (var contents in item.Model.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.name.StartsWith("F_Liquid", StringComparison.Ordinal)))
                {
                    contents.gameObject.SetActive(true);
                    contents.sharedMaterial = liquid;
                    contents.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
        }

        private void Pipette(Transform parent, string name, string asset, Vector3 point, float yaw)
        {
            var item = FinalLabProps.Place(parent, name, Glassware + asset + ".prefab", point, new Vector3(90, yaw, 0),
                depth: 0.19f, width: 0.24f, exactBounds: true);
            SetMaterial(item, pipette);
        }

        private static void SetMaterial(Prop item, Material material, bool noShadows = false)
        {
            foreach (var renderer in item.Renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                if (noShadows) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static Prop PlaceBoxes(Transform zone, string name, Vector3 point, float yaw, float width, float depth) =>
            FinalLabProps.Place(zone, name, Furniture + "old_paper__cardboard_boxes.glb", point, new Vector3(0, yaw, 0),
                width: width, depth: depth, selection: r => r.name == "Object_2");

        private static Prop FurnitureAt(Transform parent, string name, string asset, float x, float y, float z, float yaw,
            float height = 0, float width = 0, float depth = 0) =>
            PlaceModel(parent, name, Furniture, asset, new Vector3(x, y, z), yaw, height, width, depth);

        private static Prop PlaceModel(Transform parent, string name, string folder, string asset, Vector3 point, float yaw,
            float height = 0, float width = 0, float depth = 0) =>
            FinalLabProps.Place(parent, name, folder + asset + ".glb", point, new Vector3(0, yaw, 0), height, width, depth);
    }

    private static void Spot(Transform parent, string name, Vector3 position, Vector3 target, Color color,
        float intensity, float range, float angle)
    {
        var anchor = Mark(parent, name, position);
        anchor.rotation = Quaternion.LookRotation(target - position);
        if (!anchor.TryGetComponent<Light>(out var lamp)) lamp = anchor.gameObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.intensity = intensity;
        lamp.color = color;
        lamp.range = range;
        lamp.spotAngle = angle;
        lamp.innerSpotAngle = angle * 0.55f;
        lamp.shadows = LightShadows.None;
    }

    private static Transform Block(Transform parent, string name, Vector3 center, Vector3 size, Material material,
        bool solid = false)
    {
        var item = parent.Find(name);
        if (item == null)
        {
            item = new GameObject(name).transform;
            item.SetParent(parent, false);
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            item.gameObject.AddComponent<MeshRenderer>();
        }
        item.position = center;
        item.localScale = size;
        item.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (solid && !item.TryGetComponent<BoxCollider>(out _)) item.gameObject.AddComponent<BoxCollider>();
        return item;
    }
}
