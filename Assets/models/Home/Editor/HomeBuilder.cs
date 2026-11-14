using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TripleRep.Home.Editor
{
    // Plan coordinates are metres: x goes right, d goes down from the inside
    // face of the window wall. World Y is up, world Z = Depth - d.
    public static class HomeBuilder
    {
        const string Folder = "Assets/models/Home";
        const string ScenePath = "Assets/Scenes/Home.unity";
        const string PrefabPath = Folder + "/Home.prefab";
        const float Width = 5.725f;
        const float Depth = 5.933f;
        const float Height = 2.7f;
        const float DoorHeight = 2.1f;
        const float Sill = 0.9f;
        const float WindowTop = 2.4f;
        static readonly List<Solid> Solids = new List<Solid>();

        struct Solid
        {
            public string Group, Name;
            public Bounds Bounds;
            public Solid(string group, string name, float x0, float x1,
                float d0, float d1, float y0, float y1)
            {
                if (x1 <= x0 || d1 <= d0 || y1 <= y0)
                    throw new ArgumentException("Invalid solid: " + name);
                Group = group;
                Name = name;
                Bounds = new Bounds(new Vector3((x0 + x1) / 2, (y0 + y1) / 2,
                    Depth - (d0 + d1) / 2), new Vector3(x1 - x0, y1 - y0, d1 - d0));
            }
        }

        [MenuItem("Tools/Home/Rebuild model and place in Home scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(Folder + "/Meshes");
            Directory.CreateDirectory(Folder + "/Materials");
            AssetDatabase.Refresh();
            DefineGeometry();

            var wall = Material("Warm plaster", new Color(0.83f, 0.81f, 0.76f), 0.16f);
            var floor = Material("Floor", new Color(0.51f, 0.39f, 0.26f), 0.25f);
            var tile = Material("Bathroom floor", new Color(0.47f, 0.52f, 0.53f), 0.3f);
            var ceiling = Material("Ceiling", new Color(0.91f, 0.91f, 0.88f), 0.1f);
            var concrete = Material("Slab", new Color(0.4f, 0.41f, 0.42f), 0.1f);
            var root = new GameObject("Home");
            foreach (var group in Solids.GroupBy(s => s.Group))
            {
                var parent = FindOrCreate(root.transform, group.Key);
                var material = group.Key == "Ceiling" ? ceiling :
                    group.Key == "Floors/Bathroom" ? tile :
                    group.Key == "Floors/Finish" ? floor :
                    group.Key == "Floors/Slab" ? concrete : wall;
                var mesh = BuildSurfaceMesh(group.Key.Replace('/', '_'), group.ToList());
                string meshPath = Folder + "/Meshes/" + mesh.name + ".asset";
                var stored = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (stored == null) { AssetDatabase.CreateAsset(mesh, meshPath); stored = mesh; }
                else { EditorUtility.CopySerialized(mesh, stored); UnityEngine.Object.DestroyImmediate(mesh); }
                parent.gameObject.AddComponent<MeshFilter>().sharedMesh = stored;
                parent.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                foreach (var solid in group)
                {
                    var collision = new GameObject(solid.Name);
                    collision.transform.SetParent(parent, false);
                    collision.transform.localPosition = solid.Bounds.center;
                    collision.AddComponent<BoxCollider>().size = solid.Bounds.size;
                    GameObjectUtility.SetStaticEditorFlags(collision, StaticEditorFlags.BatchingStatic);
                }
                GameObjectUtility.SetStaticEditorFlags(parent.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
            Marker(root, "Window Bedroom - 1500 x 1500 - sill 900", 1.58f, -0.21f, 1.65f);
            Marker(root, "Window Living - 1500 x 1500 - sill 900", 4.185f, -0.21f, 1.65f);
            Marker(root, "Door Entrance - 950 x 2100", 2.805f, Depth + 0.125f, 1.05f);
            Marker(root, "Door Bedroom pocket - 750 x 2100", 2.85f, 2.425f, 1.05f);
            Marker(root, "Door Bathroom - 700 x 2100", 1.95f, 4.75f, 1.05f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var old in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old) == PrefabPath)
                    UnityEngine.Object.DestroyImmediate(old);
                else if (old.name == "Home Preview Lighting")
                    UnityEngine.Object.DestroyImmediate(old);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ConfigureScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = instance;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(Width / 2, 0.7f, Depth / 2),
                    Quaternion.Euler(62, 0, 0), 7f);
            Validate();
            Debug.Log("HOME_BUILD_OK: " + PrefabPath + " placed in " + ScenePath);
        }

        static void DefineGeometry()
        {
            Solids.Clear();
            // Explicit drawing dimensions: 2800 + 100 + 2825 across;
            // 3583 + 100 + 1900 + 350 from north to south.
            Add("Walls/Exterior", "West", -0.25f, 0, -0.42f, Depth + 0.25f);
            Add("Walls/Exterior", "East", Width, Width + 0.25f, -0.42f, Depth + 0.25f);
            Add("Walls/Exterior", "North west pier", 0, 0.83f, -0.42f, 0);
            Add("Walls/Exterior", "North centre pier", 2.33f, 3.435f, -0.42f, 0);
            Add("Walls/Exterior", "North east pier", 4.935f, Width, -0.42f, 0);
            foreach (float x in new[] { 0.83f, 3.435f })
            {
                Add("Walls/Exterior", "Window sill " + x, x, x + 1.5f, -0.42f, 0, 0, Sill);
                Add("Walls/Exterior", "Window lintel " + x, x, x + 1.5f, -0.42f, 0, WindowTop, Height);
            }
            Add("Walls/Exterior", "Entrance west", 0, 2.33f, Depth, Depth + 0.25f);
            Add("Walls/Exterior", "Entrance east", 3.28f, Width, Depth, Depth + 0.25f);
            Add("Walls/Exterior", "Entrance lintel", 2.33f, 3.28f, Depth, Depth + 0.25f, DoorHeight, Height);

            // The 900 dimension by the bed is a horizontal circulation width,
            // not the width of the pocket door in the perpendicular partition.
            Add("Walls/Partitions", "Bedroom pocket wall", 2.8f, 2.9f, 0, 2.05f);
            Add("Walls/Partitions", "Bedroom door south jamb", 2.8f, 2.9f, 2.8f, 2.983f);
            Add("Walls/Partitions", "Bedroom door lintel", 2.8f, 2.9f, 2.05f, 2.8f, DoorHeight, Height);
            Add("Walls/Partitions", "Bedroom south return", 1.9f, 2.9f, 2.983f, 3.083f);
            Add("Walls/Partitions", "Wardrobe and bathroom east", 1.9f, 2, 3.083f, 4.4f);
            Add("Walls/Partitions", "Bathroom north", 0, 1.9f, 3.583f, 3.683f);
            Add("Walls/Partitions", "Bathroom south jamb", 1.9f, 2, 5.1f, Depth);
            Add("Walls/Partitions", "Bathroom door lintel", 1.9f, 2, 4.4f, 5.1f, DoorHeight, Height);

            // Hatched C-shaped service enclosure; dashed east side stays open.
            Add("Walls/ServiceCore", "Service north", 3.28f, 3.98f, 4.483f, 4.583f);
            Add("Walls/ServiceCore", "Service west", 3.28f, 3.38f, 4.583f, 5.183f);
            Add("Walls/ServiceCore", "Service south", 3.28f, 3.98f, 5.183f, 5.283f);
            Add("Walls/ServiceCore", "Service south return", 3.88f, 3.98f, 5.283f, Depth);
            // Short hatched service plinths under bath and kitchen equipment.
            Add("Walls/ServiceCore", "Bathroom service plinth", 0, 0.75f, 5.583f, Depth, 0, 0.35f);
            Add("Walls/ServiceCore", "Kitchen service plinth", 5.2f, Width, 5.583f, Depth, 0, 0.35f);

            Add("Floors/Slab", "Continuous structural slab", -0.25f, Width + 0.25f, -0.42f, Depth + 0.25f, -0.18f, -0.01f);
            Add("Floors/Finish", "North finish", -0.25f, 1.9f, -0.42f, 3.683f, -0.01f, 0);
            Add("Floors/Finish", "East finish", 1.9f, Width + 0.25f, -0.42f, Depth + 0.25f, -0.01f, 0);
            Add("Floors/Finish", "West edge finish", -0.25f, 0, 3.683f, Depth, -0.01f, 0);
            Add("Floors/Finish", "South edge finish", -0.25f, 1.9f, Depth, Depth + 0.25f, -0.01f, 0);
            Add("Floors/Bathroom", "Bathroom finish", 0, 1.9f, 3.683f, Depth, -0.01f, 0);
            Add("Ceiling", "Ceiling slab", -0.25f, Width + 0.25f, -0.42f, Depth + 0.25f, Height, Height + 0.15f);
        }

        static void Add(string group, string name, float x0, float x1, float d0, float d1,
            float y0 = 0, float y1 = Height)
        { Solids.Add(new Solid(group, name, x0, x1, d0, d1, y0, y1)); }

        // Extract the boundary of the union on the exact box-coordinate grid.
        // This removes all internal faces between adjacent wall sections.
        static Mesh BuildSurfaceMesh(string name, List<Solid> solids)
        {
            float[] xs = Coordinates(solids, 0), ys = Coordinates(solids, 1), zs = Coordinates(solids, 2);
            int nx = xs.Length - 1, ny = ys.Length - 1, nz = zs.Length - 1;
            var filled = new bool[nx, ny, nz];
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            for (int z = 0; z < nz; z++)
            {
                var p = new Vector3((xs[x] + xs[x + 1]) / 2, (ys[y] + ys[y + 1]) / 2, (zs[z] + zs[z + 1]) / 2);
                filled[x, y, z] = solids.Any(s => s.Bounds.Contains(p));
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            for (int z = 0; z < nz; z++)
            {
                if (!filled[x, y, z]) continue;
                float a = xs[x], b = xs[x + 1], c = ys[y], d = ys[y + 1], e = zs[z], f = zs[z + 1];
                if (x == 0 || !filled[x - 1, y, z]) Face(new Vector3(a,c,e), new Vector3(a,c,f), new Vector3(a,d,f), new Vector3(a,d,e), Vector3.left);
                if (x == nx - 1 || !filled[x + 1, y, z]) Face(new Vector3(b,c,f), new Vector3(b,c,e), new Vector3(b,d,e), new Vector3(b,d,f), Vector3.right);
                if (y == 0 || !filled[x, y - 1, z]) Face(new Vector3(a,c,e), new Vector3(b,c,e), new Vector3(b,c,f), new Vector3(a,c,f), Vector3.down);
                if (y == ny - 1 || !filled[x, y + 1, z]) Face(new Vector3(a,d,f), new Vector3(b,d,f), new Vector3(b,d,e), new Vector3(a,d,e), Vector3.up);
                if (z == 0 || !filled[x, y, z - 1]) Face(new Vector3(b,c,e), new Vector3(a,c,e), new Vector3(a,d,e), new Vector3(b,d,e), Vector3.back);
                if (z == nz - 1 || !filled[x, y, z + 1]) Face(new Vector3(a,c,f), new Vector3(b,c,f), new Vector3(b,d,f), new Vector3(a,d,f), Vector3.forward);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            return mesh;

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                normals.AddRange(new[] { normal, normal, normal, normal });
                foreach (var p in new[] { a, b, c, d })
                    uv.Add(Mathf.Abs(normal.y) > 0.5f ? new Vector2(p.x, p.z) :
                        Mathf.Abs(normal.x) > 0.5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
        }

        static float[] Coordinates(List<Solid> solids, int axis)
        {
            // Millimetre rounding prevents sliver cells from float arithmetic.
            return solids.SelectMany(s => new[] { s.Bounds.min[axis], s.Bounds.max[axis] })
                .Select(v => Mathf.Round(v * 1000) / 1000).Distinct().OrderBy(v => v).ToArray();
        }

        static Material Material(string name, Color color, float smoothness)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform FindOrCreate(Transform parent, string path)
        {
            foreach (var part in path.Split('/'))
            {
                var next = parent.Find(part);
                if (next == null) { next = new GameObject(part).transform; next.SetParent(parent, false); }
                parent = next;
            }
            return parent;
        }

        static void Marker(GameObject root, string name, float x, float d, float y)
        { FindOrCreate(root.transform, "Openings/" + name).localPosition = new Vector3(x, y, Depth - d); }

        static void ConfigureScene(Scene scene)
        {
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera == null) { camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; }
            camera.transform.position = new Vector3(4.85f, 1.6f, 1.8f);
            camera.transform.LookAt(new Vector3(2.9f, 1.4f, 4.7f));
            camera.fieldOfView = 75; camera.nearClipPlane = 0.05f; camera.farClipPlane = 100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rig = new GameObject("Home Preview Lighting");
            AddLight("Living", new Vector3(4.25f, 2.38f, 3.8f), 2.1f, 4.8f);
            AddLight("Bedroom", new Vector3(1.4f, 2.38f, 4.35f), 1.6f, 3.8f);
            AddLight("Hall", new Vector3(2.65f, 2.38f, 1.1f), 1.1f, 3f);
            AddLight("Bathroom", new Vector3(0.95f, 2.38f, 1.05f), 1.2f, 3f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.47f, 0.51f, 0.57f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.33f, 0.30f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.15f);
            RenderSettings.ambientIntensity = 1;

            void AddLight(string name, Vector3 p, float intensity, float range)
            {
                var light = new GameObject(name).AddComponent<Light>();
                light.transform.SetParent(rig.transform, false); light.transform.localPosition = p;
                light.type = LightType.Point; light.color = new Color(1, 0.94f, 0.84f);
                light.intensity = intensity; light.range = range; light.shadows = LightShadows.Soft;
                var lightData = new SerializedObject(light.GetUniversalAdditionalLightData());
                lightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue =
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
                lightData.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [MenuItem("Tools/Home/Validate saved model and scene")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var instances = scene.GetRootGameObjects().Where(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == PrefabPath).ToArray();
            Require(instances.Length == 1, "Exactly one Home prefab instance must exist.");
            var root = instances[0];
            Require(root.transform.Find("Ceiling").gameObject.activeSelf, "Ceiling must be enabled.");
            Require(root.transform.Find("Openings").childCount == 5, "Expected 2 windows and 3 doors.");
            Require(root.GetComponentsInChildren<MeshFilter>().Length == 7, "Expected 7 saved mesh groups.");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                Require(mesh != null && mesh.vertexCount > 0 && mesh.uv2.Length == mesh.vertexCount, "Missing mesh/UV2: " + filter.name);
                var vertices = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    Require(cross.sqrMagnitude > 1e-12f && Vector3.Dot(cross, normals[a]) > 0, "Invalid mesh winding: " + filter.name);
                }
            }
            Physics.SyncTransforms();
            // Check the actual saved colliders, not just the construction inputs.
            CheckOpening(new Vector3(1.58f, 1.65f, Depth + 0.5f), Vector3.back, 0.75f, "Bedroom window");
            CheckOpening(new Vector3(4.185f, 1.65f, Depth + 0.5f), Vector3.back, 0.75f, "Living window");
            CheckOpening(new Vector3(2.805f, 1.05f, -0.4f), Vector3.forward, 0.7f, "Entrance");
            CheckOpening(new Vector3(2.6f, 1.05f, Depth - 2.425f), Vector3.right, 0.6f, "Bedroom door");
            CheckOpening(new Vector3(1.7f, 1.05f, Depth - 4.75f), Vector3.right, 0.6f, "Bathroom door");
            Require(Physics.Raycast(new Vector3(1.58f, 0.45f, Depth + 0.5f), Vector3.back, 0.75f), "Missing window sill collision.");
            Require(Physics.Raycast(new Vector3(2.805f, 2.3f, -0.4f), Vector3.forward, 0.7f), "Missing door lintel collision.");
            foreach (var p in new[] { new Vector3(1,1,4), new Vector3(4.5f,1,4), new Vector3(0.95f,1,1), new Vector3(2.7f,1,1) })
            {
                Require(Physics.Raycast(p, Vector3.down, out var hit, 1.1f) && Mathf.Abs(hit.point.y) < 0.002f, "Missing level floor: " + p);
                Require(Physics.Raycast(p, Vector3.up, out hit, 1.8f) && Mathf.Abs(hit.point.y - Height) < 0.002f, "Missing ceiling: " + p);
            }
            Directory.CreateDirectory("Logs/Home");
            File.WriteAllText("Logs/Home/validation.txt", "PASS\nSaved scene: " + ScenePath + "\nPrefab: " + PrefabPath +
                "\n7 mesh groups; " + root.GetComponentsInChildren<BoxCollider>().Length + " solid colliders.\n" +
                "All 5 openings unobstructed. Sill/lintel collision verified.\nFloor level 0 and ceiling 2.7 verified in 4 rooms. Mesh winding and UV2 verified.\n");
            Debug.Log("HOME_VALIDATION_OK");

            void CheckOpening(Vector3 origin, Vector3 direction, float distance, string name)
            { Require(!Physics.Raycast(origin, direction, distance), name + " is blocked by a collider."); }
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Tools/Home/Build and render previews")]
        public static void BuildAndPreview()
        {
            Build();
            // Rendering needs a graphics device; do not use -nographics.
            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == PrefabPath);
            var ceiling = root.transform.Find("Ceiling").gameObject;
            var camera = Camera.main;
            Directory.CreateDirectory("Logs/Home");
            var position = camera.transform.position; var rotation = camera.transform.rotation;
            try
            {
                // Warm up renderer registration after loading the saved scene.
                Render(camera, "Logs/Home/interior.png", 1440, 1080);
                ceiling.SetActive(false);
                camera.orthographic = true; camera.orthographicSize = 4.6f;
                camera.transform.position = new Vector3(2.86f, 14, 2.88f);
                camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                Render(camera, "Logs/Home/plan.png", 1200, 1200);
                camera.orthographicSize = 4.9f;
                camera.transform.position = new Vector3(11, 13, -10);
                camera.transform.LookAt(new Vector3(2.86f, 0.6f, 2.88f));
                Render(camera, "Logs/Home/overview.png", 1440, 1200);
                ceiling.SetActive(true);
                camera.orthographic = false;
                camera.transform.SetPositionAndRotation(position, rotation);
                Render(camera, "Logs/Home/interior.png", 1440, 1080);
            }
            finally
            {
                // Preview changes are discarded even if rendering fails.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Debug.Log("HOME_PREVIEW_OK");
        }

        static void Render(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                // StandardRequest is the supported synchronous URP camera render path.
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
