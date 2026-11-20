using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

public static partial class FirstMindForestBuilder
{
    private static Terrain CreateTerrain(Transform parent, Bounds footprint, Vector2[] trail)
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>($"{DataFolder}/Forest Terrain.asset");
        if (data == null)
        {
            data = new TerrainData { name = "Forest Terrain" };
            AssetDatabase.CreateAsset(data, $"{DataFolder}/Forest Terrain.asset");
        }

        data.heightmapResolution = 513;
        data.alphamapResolution = 512;
        data.baseMapResolution = 512;
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
        int resolution = data.heightmapResolution;
        var heights = new float[resolution, resolution];
        float low = float.MaxValue;
        float high = float.MinValue;
        for (int z = 0; z < resolution; z++)
        for (int x = 0; x < resolution; x++)
        {
            Vector2 point = TerrainPoint(x, z, resolution);
            float elevation = GroundHeight(point, footprint, trail);
            heights[z, x] = (elevation - TerrainBottom) / TerrainHeight;
            low = Mathf.Min(low, elevation);
            high = Mathf.Max(high, elevation);
        }
        data.SetHeights(0, 0, heights);
        Require(high - low > 3f && low > TerrainBottom && high < TerrainBottom + TerrainHeight,
            "Terrain height levels are invalid");
        Debug.Log($"FIRST_MIND_TERRAIN: {TerrainSize:F0} x {TerrainSize:F0} m, {resolution} height samples per side, " +
            $"elevation from {low:F2} to {high:F2} m");

        data.terrainLayers = new[]
        {
            MakeLayer("Needle Litter", 2.7f, new Color32(53, 43, 31, 255), new Color32(119, 93, 61, 255), 1.0f, 0.11f, 0.42f),
            MakeLayer("Moss and Summer Grass", 2.25f, new Color32(44, 56, 29, 255), new Color32(109, 128, 66, 255), 0.35f, 0.10f, 0.51f),
            MakeLayer("Damp Forest Soil", 2.1f, new Color32(46, 39, 31, 255), new Color32(101, 82, 63, 255), 0.28f, 0.17f, 0.36f),
            MakeLayer("Worn Trail", 2.45f, new Color32(71, 60, 46, 255), new Color32(128, 112, 87, 255), 0.13f, 0.09f, 0.29f)
        };
        int maskResolution = data.alphamapResolution;
        var weights = new float[maskResolution, maskResolution, 4];
        for (int z = 0; z < maskResolution; z++)
        for (int x = 0; x < maskResolution; x++)
        {
            Vector2 point = TerrainPoint(x, z, maskResolution);
            float patch = Noise(point, 0.165f, 72f) * 0.64f + Noise(point, 0.48f, 19f) * 0.36f;
            float clearing = 1f - SmoothRange(4f, 13f, DistanceToFootprint(point, footprint));
            float moss = SmoothRange(0.34f, 0.67f, patch) * Mathf.Lerp(0.66f, 0.92f, clearing);
            float soil = Mathf.Lerp(0.17f, 0.63f, SmoothRange(0.19f, 0.78f, Noise(point, 0.23f, 48f)));
            float trailDistance = TrailDistance(point, trail, out float pathProgress);
            float raggedEdge = (Noise(point, 2.4f, 7f) - 0.5f) * 0.50f;
            float worn = (1f - SmoothRange(0.30f, 0.85f, trailDistance - raggedEdge)) *
                (1f - SmoothRange(0.69f, 1f, pathProgress)) * 0.92f;
            float vegetation = (1f - worn);
            weights[z, x, 0] = vegetation * (1f - moss) * (1f - soil);
            weights[z, x, 1] = vegetation * moss;
            weights[z, x, 2] = vegetation * (1f - moss) * soil;
            weights[z, x, 3] = worn;
        }
        data.SetAlphamaps(0, 0, weights);
        EditorUtility.SetDirty(data);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Forest Ground";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-TerrainSize / 2f, TerrainBottom, -TerrainSize / 2f);
        var terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3.75f;
        terrain.basemapDistance = 72f;
        terrain.shadowCastingMode = ShadowCastingMode.On;
        return terrain;
    }

    private static float GroundHeight(Vector2 point, Bounds footprint, Vector2[] trail)
    {
        float edge = DistanceToFootprint(point, footprint);
        float flatter = SmoothRange(0.80f, 11.5f, edge);
        float rise = (Noise(point, 0.031f, 83f) - 0.48f) * 5.5f;
        float hollows = (Noise(point, 0.079f, 21f) - 0.52f) * 2.6f;
        float bumps = (Noise(point, 0.24f, 49f) - 0.48f) * 0.85f;
        float small = (Noise(point, 0.72f, 143f) - 0.50f) * 0.18f;
        float pathBlend = 1f - SmoothRange(0.6f, 3.0f, TrailDistance(point, trail, out _));
        float relief = rise + hollows + bumps + small;
        return flatter * (relief - pathBlend * (bumps + small) * 0.68f);
    }

    private static TerrainLayer MakeLayer(string name, float metres, Color dark, Color light,
        float litterStrength, float smoothness, float normalStrength)
    {
        const int size = 384;
        var rng = new Random(Seed + Mathf.RoundToInt(metres * 100f));
        var grains = new float[size, size];
        int needles = Mathf.RoundToInt(5450 * litterStrength);
        for (int i = 0; i < needles; i++)
        {
            int x = rng.Next(size);
            int z = rng.Next(size);
            float angle = Range(rng, 0f, Mathf.PI * 2f);
            float dx = Mathf.Cos(angle);
            float dz = Mathf.Sin(angle);
            int length = rng.Next(3, 15);
            float intensity = Range(rng, -0.13f, 0.27f);
            for (int j = 0; j < length; j++)
            {
                int px = (x + Mathf.RoundToInt(j * dx) + size) % size;
                int pz = (z + Mathf.RoundToInt(j * dz) + size) % size;
                grains[pz, px] += intensity * (0.56f + 0.44f * Mathf.Sin(j * Mathf.PI / length));
            }
        }

        var colors = new Color[size * size];
        var height = new float[size, size];
        float offset = metres * 100f;
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size;
            float v = z / (float)size;
            float broad = TileNoise(u, v, 7f, offset);
            float small = TileNoise(u, v, 39f, offset + 61f);
            float fine = TileNoise(u, v, 123f, offset + 157f);
            float randomGrain = Range(rng, -0.08f, 0.08f);
            float strand = grains[z, x];
            float tone = Mathf.Clamp01(broad * 0.39f + small * 0.38f + fine * 0.23f + strand + randomGrain);
            Color albedo = Color.Lerp(dark, light, tone);
            albedo.a = smoothness; // URP uses diffuse alpha for smoothness when the texture has an alpha channel.
            colors[z * size + x] = albedo;
            height[z, x] = broad * 0.10f + small * 0.42f + fine * 0.18f + strand * 0.95f + randomGrain * 0.25f;
        }

        var normals = new Color[size * size];
        for (int z = 0; z < size; z++)
        for (int x = 0; x < size; x++)
        {
            float xGradient = height[z, (x + size - 1) % size] - height[z, (x + 1) % size];
            float zGradient = height[(z + size - 1) % size, x] - height[(z + 1) % size, x];
            Vector3 normal = new Vector3(xGradient * 1.8f, zGradient * 1.8f, 1f).normalized;
            normals[z * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, 1f, 1f);
        }

        var layer = new TerrainLayer
        {
            name = name,
            diffuseTexture = GroundTexture(name + " Color", colors, size, false),
            normalMapTexture = GroundTexture(name + " Normal", normals, size, true),
            tileSize = Vector2.one * metres,
            metallic = 0f,
            smoothness = smoothness,
            normalScale = normalStrength
        };
        return Store(layer, name + ".terrainlayer");
    }

    private static Texture2D GroundTexture(string name, Color[] pixels, int size, bool linear)
    {
        var texture = new Texture2D(size, size, linear ? TextureFormat.RGBA32 : TextureFormat.RGB24, true, linear)
        {
            name = name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8
        };
        texture.SetPixels(pixels);
        texture.Apply(true, false);
        return Store(texture, name + ".asset");
    }

    private static int[] PlantForest(Transform parent, Terrain terrain, GameObject[] assets, Bounds footprint, Vector2[] trail)
    {
        const int target = 840;
        var placed = new Vector2[target];
        var spacing = new float[target];
        var counts = new int[assets.Length];
        var rng = new Random(Seed);

        int total = 0;
        for (int attempt = 0; attempt < 16000 && total < target; attempt++)
        {
            const float edge = TerrainSize * 0.5f - 2f;
            var point = new Vector2(Range(rng, -edge, edge), Range(rng, -edge, edge));
            float fromHouse = DistanceToFootprint(point, footprint);
            float margin = 8.8f + Noise(point, 0.15f, 215f) * 3.9f;
            if (fromHouse < margin || TrailDistance(point, trail, out _) < 2.35f) continue;
            if (Mathf.Abs(fromHouse - BoundaryRadius) < 1.35f) continue;
            float grove = Noise(point, 0.056f, 35f);
            if (Range(rng, 0f, 1f) > Mathf.Lerp(0.32f, 1f, SmoothRange(0.29f, 0.76f, grove))) continue;

            float roll = Range(rng, 0f, 1f);
            int type = fromHouse < 15f
                ? (roll < 0.54f ? 1 : 0)
                : (roll < 0.52f ? 0 : roll < 0.73f ? 2 : roll < 0.97f ? 1 : 3);
            float treeSpacing = type == 1 ? 2.65f : type == 2 ? 3.65f : 3.15f;
            treeSpacing += (1f - grove) * 1.05f + (1f - SmoothRange(12f, 27f, fromHouse)) * 0.85f;
            if (HasNearbyTree(point, treeSpacing, placed, spacing, total)) continue;

            var tree = (GameObject)PrefabUtility.InstantiatePrefab(assets[type], parent);
            tree.transform.position = OnGround(terrain, point) - Vector3.up * 0.075f;
            tree.transform.rotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            float scale = Range(rng, 0.84f, 1.13f);
            tree.transform.localScale *= scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(tree.transform);
            ConfigureTree(tree, fromHouse, type);
            placed[total] = point;
            spacing[total] = treeSpacing;
            counts[type]++;
            total++;
        }
        Require(total == target, $"Only {total} of {target} conifers were placed");
        return counts;
    }

    private static void ConfigureTree(GameObject tree, float distanceFromHouse, int type)
    {
        var group = tree.GetComponent<LODGroup>();
        LOD[] lods = group.GetLODs();
        lods[0].screenRelativeTransitionHeight = type == 1 ? 0.49f : 0.65f;
        lods[1].screenRelativeTransitionHeight = type == 1 ? 0.14f : 0.24f;
        foreach (Renderer board in lods[2].renderers)
        {
            board.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(board);
        }
        group.SetLODs(lods);
        PrefabUtility.RecordPrefabInstancePropertyModifications(group);

        CapsuleCollider trunk = tree.GetComponent<CapsuleCollider>();
        if (distanceFromHouse >= BoundaryRadius)
        {
            if (trunk != null)
            {
                trunk.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(trunk);
            }
            return;
        }
        if (trunk == null)
        {
            trunk = tree.AddComponent<CapsuleCollider>();
            trunk.radius = 0.23f;
            trunk.height = 3.3f;
            trunk.center = Vector3.up * 1.43f;
        }
    }

    private static bool HasNearbyTree(Vector2 point, float minimum, Vector2[] placed, float[] spacing, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float required = (minimum + spacing[i]) * 0.5f;
            if ((placed[i] - point).sqrMagnitude < required * required) return true;
        }
        return false;
    }

    private static Vector3 OnGround(Terrain terrain, Vector2 point)
    {
        var position = new Vector3(point.x, 0f, point.y);
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        return position;
    }

    private static float DistanceToFootprint(Vector3 point, Bounds bounds) =>
        DistanceToFootprint(new Vector2(point.x, point.z), bounds);

    private static float DistanceToFootprint(Vector2 point, Bounds bounds)
    {
        float x = Mathf.Max(0f, Mathf.Abs(point.x - bounds.center.x) - bounds.extents.x);
        float z = Mathf.Max(0f, Mathf.Abs(point.y - bounds.center.z) - bounds.extents.z);
        return Mathf.Sqrt(x * x + z * z);
    }

    private static float TrailDistance(Vector2 point, Vector2[] trail, out float progress)
    {
        float best = float.MaxValue;
        progress = 0f;
        for (int i = 1; i < trail.Length; i++)
        {
            Vector2 segment = trail[i] - trail[i - 1];
            float fraction = Mathf.Clamp01(Vector2.Dot(point - trail[i - 1], segment) / segment.sqrMagnitude);
            float distance = (point - (trail[i - 1] + fraction * segment)).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            progress = (i - 1 + fraction) / (trail.Length - 1);
        }
        return Mathf.Sqrt(best);
    }

    private static Vector2 TerrainPoint(int x, int z, int resolution) =>
        new Vector2(x, z) * (TerrainSize / (resolution - 1)) - Vector2.one * (TerrainSize * 0.5f);

    private static float SmoothRange(float start, float end, float value) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, value));

    private static float Noise(Vector2 point, float frequency, float offset) =>
        Mathf.PerlinNoise(point.x * frequency + offset, point.y * frequency + offset + 28f);

    private static float TileNoise(float x, float y, float period, float offset)
    {
        float sx = Mathf.SmoothStep(0f, 1f, x);
        float sy = Mathf.SmoothStep(0f, 1f, y);
        float x0 = x * period + offset;
        float z0 = y * period + offset;
        return Mathf.Lerp(
            Mathf.Lerp(Mathf.PerlinNoise(x0, z0), Mathf.PerlinNoise(x0 - period, z0), sx),
            Mathf.Lerp(Mathf.PerlinNoise(x0, z0 - period), Mathf.PerlinNoise(x0 - period, z0 - period), sx), sy);
    }

    private static float Range(Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
