using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandsReloadPreview
{
    public static void Run()
    {
        Directory.CreateDirectory(HandsReloadAudit.Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientLight = new Color(.55f,.55f,.55f);
        var light = new GameObject("Preview light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(35, -25, 0);
        Inspect(HandsReloadAudit.Source, "source");
        Inspect(HandsReloadAudit.Target, "target");
    }
    public static Material Mat(Color color)
    {
        var m = new Material(Shader.Find("Standard")); m.color = color;
        m.SetFloat("_Glossiness", .3f); return m;
    }
    private static void Inspect(string path, string label)
    {
        string modelPath=label=="target"&&File.Exists(HandsReloadBaker.Model)?HandsReloadBaker.Model:path;
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (label == "target" && Directory.Exists(HandsReloadBaker.Folder))
            clips = clips.Concat(Directory.GetFiles(HandsReloadBaker.Folder,"*.anim").Select(p=>AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\','/')))).ToArray();
        clips.First(c => c.name.EndsWith("Idle")).SampleAnimation(model, 0);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.enabled = r is SkinnedMeshRenderer;
            var color = r.name.Contains("Sleeves") ? new Color(.12f,.18f,.28f) : r.name == "Object001" || r.name == "Makarov_base" ? new Color(.28f,.3f,.32f) : new Color(.72f,.47f,.34f);
            r.sharedMaterials = r.sharedMaterials.Select(_ => Mat(color)).ToArray();
            if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
        }
        var authored = model.GetComponentInChildren<Camera>();
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(authored.transform.position, authored.transform.rotation);
        camera.fieldOfView = 70; camera.nearClipPlane = .005f; camera.farClipPlane = 10;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f,.11f,.14f);
        foreach (var c in model.GetComponentsInChildren<Camera>()) c.enabled = false;
        foreach (var l in model.GetComponentsInChildren<Light>()) l.enabled = false;
        foreach (var clip in clips.Where(c => c.name.Contains("Reload") || c.name.EndsWith("Idle")))
        {
            float[] times = clip.name.EndsWith("Idle") ? new[] {0f} : new[] {0f,.3f,.6f,.9f,1.2f,1.5f,1.8f,2.1f,2.4f,2.7f,3f};
            foreach (float t in times.Where(t => t <= clip.length).Concat(new[]{clip.length}).Distinct())
            {
                clip.SampleAnimation(model, t);
                Render(camera, label + "-" + clip.name.Split('|').Last() + "-" + t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        if (label == "target")
        {
            clips.First(c => c.name.EndsWith("Idle")).SampleAnimation(model, 0);
            InspectGun(model, camera);
        }
        UnityEngine.Object.DestroyImmediate(model); UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }
    public static void Render(Camera camera, string name, int width = 960, int height = 640)
    {
        // Camera.Render does not advance the player loop: refresh CPU skinning for every sampled pose.
        var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(s=>s.enabled&&s.gameObject.activeInHierarchy).ToArray();
        var snapshots=new List<GameObject>(); var meshes=new List<Mesh>();
        foreach(var skin in skins)
        {
            var mesh=new Mesh(); skin.BakeMesh(mesh,false); meshes.Add(mesh);
            var snapshot=new GameObject("Sampled "+skin.name,typeof(MeshFilter),typeof(MeshRenderer));
            snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
            snapshot.GetComponent<MeshFilter>().sharedMesh=mesh;
            snapshot.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
            skin.enabled=false; snapshots.Add(snapshot);
        }
        var rt = new RenderTexture(width,height,24); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(width,height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
        File.WriteAllBytes(Path.Combine(HandsReloadAudit.Output,name + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
        foreach(var snapshot in snapshots) UnityEngine.Object.DestroyImmediate(snapshot);
        foreach(var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
        foreach(var skin in skins) skin.enabled=true;
    }
    public static List<int[]> Components(Mesh mesh)
    {
        var verts = mesh.vertices; var parents = Enumerable.Range(0,verts.Length).ToArray();
        Func<int,int> root = null; root = i => parents[i] == i ? i : parents[i] = root(parents[i]);
        var weld = new Dictionary<Vector3Int,int>();
        for (int i=0;i<verts.Length;i++)
        {
            var key = Vector3Int.RoundToInt(verts[i] * 100000f);
            if (weld.TryGetValue(key,out int j)) parents[root(i)] = root(j); else weld[key]=i;
        }
        var tri=mesh.triangles;
        for(int i=0;i<tri.Length;i+=3) { parents[root(tri[i+1])]=root(tri[i]); parents[root(tri[i+2])]=root(tri[i]); }
        return Enumerable.Range(0,verts.Length).GroupBy(i=>root(i)).Select(g=>g.ToArray()).OrderByDescending(g=>g.Length).ToList();
    }
    private static void InspectGun(GameObject model, Camera camera)
    {
        var skin = model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Object001");
        foreach(var r in model.GetComponentsInChildren<Renderer>()) r.enabled = r == skin;
        var parts = Components(skin.sharedMesh);
        var baked = new Mesh(); skin.BakeMesh(baked);
        var verts = baked.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).ToArray();
        var body = model.GetComponentsInChildren<Transform>().Single(t=>t.name=="Base");
        var report = new List<string>();
        var mesh = UnityEngine.Object.Instantiate(skin.sharedMesh);
        mesh.subMeshCount=parts.Count;
        var sourceTriangles = skin.sharedMesh.triangles;
        var materials = new Material[parts.Count];
        for (int p=0;p<parts.Count;p++)
        {
            var set=new HashSet<int>(parts[p]); var tris=new List<int>();
            for(int j=0;j<sourceTriangles.Length;j+=3) if(set.Contains(sourceTriangles[j])) tris.AddRange(sourceTriangles.Skip(j).Take(3));
            mesh.SetTriangles(tris,p);
            materials[p]=Mat(Color.HSVToRGB((p*.6180339f)%1f,.7f,.8f));
            var bounds = new Bounds(verts[parts[p][0]],Vector3.zero);
            foreach(var i in parts[p]) bounds.Encapsulate(verts[i]);
            var influences=parts[p].GroupBy(i=>skin.bones[skin.sharedMesh.boneWeights[i].boneIndex0].name).Select(g=>g.Key+":"+g.Count());
            report.Add(p+" verts="+parts[p].Length+" world center="+bounds.center.ToString("F4")+" size="+bounds.size.ToString("F4")+" bones="+string.Join(",",influences));
        }
        skin.sharedMesh=mesh; skin.sharedMaterials=materials;
        camera.transform.position=body.position+new Vector3(.65f,.08f,.08f); camera.transform.LookAt(body.position+Vector3.up*.03f);
        camera.fieldOfView=30; Render(camera,"gun-components-side");
        camera.transform.position=body.position+new Vector3(.22f,.3f,-.4f); camera.transform.LookAt(body.position+Vector3.up*.03f);
        Render(camera,"gun-components-back");
        File.WriteAllLines(Path.Combine(HandsReloadAudit.Output,"gun-components.txt"),report);
    }
}
