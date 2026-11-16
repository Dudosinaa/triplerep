using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TripleRep.Home.Editor
{
    // Repairs the existing model in place: no recreation of the scene, materials or windows.
    public static class HomeLightmapRepair
    {
        const string Log = "Logs/HomeSeams";
        const string ScenePath = "Assets/Level/Scenes/Locations/Home.unity";
        const string PrefabPath = "Assets/Level/Prefabs/Environment/Home.prefab";
        const string MeshFolder = "Assets/Art/Models/Environment/Home/Meshes/LightmapReady";
        const float Eps = 0.00002f;
        static GameObject home;
        static readonly List<Material> materials = new();
        static readonly List<string> report = new();

        [InitializeOnLoadMethod]
        static void Pending()
        {
            if (!File.Exists(Log + "/repair.request")) {
                if(File.Exists(Log+"/status.txt")&&File.ReadAllText(Log+"/status.txt")=="BAKING") EditorApplication.delayCall+=ResumeBakeWatch;
                return;
            }
            EditorApplication.delayCall += () => {
                File.Delete(Log + "/repair.request");
                try { RepairAndBake(); }
                catch (Exception e) { File.WriteAllText(Log + "/error.txt", e.ToString()); Debug.LogException(e); }
            };
        }

        static void ResumeBakeWatch(){if(Lightmapping.isRunning){Lightmapping.bakeCompleted-=Complete;Lightmapping.bakeCompleted+=Complete;}else {home=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==PrefabPath);if(home!=null)Complete();}}
        [MenuItem("Tools/Home/Repair lightmap seams and rebake (preserve scene)")]
        public static void RepairAndBake()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Log);
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            if (!File.Exists(Log + "/Home.before.unity")) File.Copy(ScenePath, Log + "/Home.before.unity");
            if (!File.Exists(Log + "/Home.before.prefab")) File.Copy(PrefabPath, Log + "/Home.before.prefab");
            home = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == PrefabPath);
            if (!File.Exists(Log + "/before-interior.png")) Preview("before");
            PrepareModel(home);
            Physics.SyncTransforms();
            CheckOpenings();
            // Keep all user overrides, including texture assignments, on the saved prefab.
            PrefabUtility.ApplyPrefabInstance(home, InteractionMode.AutomatedAction);
            var settings = Lightmapping.lightingSettings;
            if (settings != null)
            {
                var data = new SerializedObject(settings);
                Set("m_Padding", 8); Set("m_AOMaxDistance", .2f); Set("m_PVRDirectSampleCount", 64);
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings);
                void Set(string property, float value) { var p = data.FindProperty(property); if(p.propertyType == SerializedPropertyType.Integer) p.intValue=(int)value; else p.floatValue=value; }
            }
            // Invalidate the previous atlas after changing the topology and UV charts.
            // Disabled construction renderers must not retain old GI contributions.
            Lightmapping.Clear(); Lightmapping.ClearLightingDataAsset();
            foreach(var r in home.transform.Find("LightmapReady").GetComponentsInChildren<MeshRenderer>()){r.lightmapIndex=-1;r.lightmapScaleOffset=new Vector4(1,1,0,0);}
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            File.WriteAllLines(Log + "/geometry-validation.txt", report);
            Lightmapping.bakeCompleted += Complete;
            File.WriteAllText(Log + "/status.txt", "BAKING");
            if (!Lightmapping.BakeAsync()) { Lightmapping.bakeCompleted -= Complete; throw new Exception("Unity could not start the lighting bake."); }
        }
        static void Complete()
        {
            Lightmapping.bakeCompleted -= Complete;
            try {
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Preview("after"); File.WriteAllText(Log + "/status.txt", "COMPLETE"); Debug.Log("HOME_SEAMS_REPAIR_OK");
            } catch(Exception e) {File.WriteAllText(Log+"/error.txt",e.ToString());Debug.LogException(e);}
        }
        // Also called by the model generator so a rebuild cannot bring the seams back.
        public static void PrepareModel(GameObject model)
        {
            home=model; materials.Clear(); report.Clear();
            Directory.CreateDirectory(Log); Directory.CreateDirectory(MeshFolder);
            var previous=home.transform.Find("LightmapReady");
            var parent=previous;
            if(parent==null){parent=new GameObject("LightmapReady").transform;parent.SetParent(home.transform,false);}
            RepairGroup(parent,"Walls",new[]{"Exterior","Partitions"},true);
            RepairGroup(parent,"Floor",new[]{"Floors"},false);
            RepairGroup(parent,"Ceiling",new[]{"Ceiling"},false);
        }
        static void RepairGroup(Transform parent, string name, string[] groups, bool walls)
        {
            var filters = groups.SelectMany(g => home.transform.Find(g).GetComponentsInChildren<MeshFilter>())
                .Where(f => f.GetComponent<MeshRenderer>() != null).ToArray();
            var solids = new List<List<Poly>>();
            foreach (var f in filters) solids.Add(Read(f));
            if(walls) AddCornerClosures(solids, filters.Where(f=>f.transform.parent.name=="Exterior").ToArray());
            // Balanced unions avoid deeply skewed BSP trees for long chains of strips.
            while(solids.Count>1) {
                var next=new List<List<Poly>>();
                for(int i=0;i<solids.Count;i+=2) next.Add(i+1<solids.Count?Union(solids[i],solids[i+1]):solids[i]);
                solids=next;
            }
            var polygons=solids.Single();
            var mesh=MakeMesh(polygons, name);
            string path=MeshFolder+"/"+name+"_Boundary.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null) AssetDatabase.CreateAsset(mesh,path);
            else {
                // Mesh setters invalidate native/GPU buffers. CopySerialized alone can leave
                // the previous UV buffer resident while the lightmapper uses the new UVs.
                saved.Clear();saved.indexFormat=mesh.indexFormat;saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.tangents=mesh.tangents;saved.uv=mesh.uv;saved.uv2=mesh.uv2;saved.subMeshCount=mesh.subMeshCount;
                for(int sub=0;sub<mesh.subMeshCount;sub++)saved.SetTriangles(mesh.GetTriangles(sub),sub);
                saved.RecalculateBounds();saved.UploadMeshData(false);EditorUtility.SetDirty(saved);
                UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;
            }

            var existing=parent.Find(name);
            var go=existing!=null?existing.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterials=materials.ToArray();
            renderer.stitchLightmapSeams=true; renderer.receiveGI=ReceiveGI.Lightmaps; renderer.scaleInLightmap=1;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            go.GetComponent<MeshCollider>().sharedMesh=mesh;
            foreach(var f in filters) {
                f.GetComponent<MeshRenderer>().enabled=false;
                GameObjectUtility.SetStaticEditorFlags(f.gameObject,GameObjectUtility.GetStaticEditorFlags(f.gameObject)&~StaticEditorFlags.ContributeGI);
                foreach(var c in f.GetComponents<Collider>())c.enabled=false;
            }
            report.Add(name+": "+filters.Length+" separate renderers -> one boundary mesh; "+mesh.vertexCount+" vertices; "+mesh.triangles.Length/3+" triangles; UV2 generated; seam stitching enabled.");
            File.WriteAllText(Log+"/progress.txt",name+" complete");
        }
        static List<Poly> Read(MeshFilter filter)
        {
            var mesh=filter.sharedMesh;var matrix=home.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var vertices=mesh.vertices;var uv=mesh.uv;var result=new List<Poly>();
            var mats=filter.GetComponent<MeshRenderer>().sharedMaterials;
            for(int sub=0;sub<mesh.subMeshCount;sub++) {
                var mat=mats[Math.Min(sub,mats.Length-1)];int id=materials.IndexOf(mat);if(id<0){id=materials.Count;materials.Add(mat);}
                var triangles=mesh.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3) {
                    var v=new List<V>();for(int j=0;j<3;j++){int k=triangles[i+j];v.Add(new V(matrix.MultiplyPoint3x4(vertices[k]),uv.Length>k?uv[k]:Vector2.zero));}
                    if(Vector3.Cross(v[1].p-v[0].p,v[2].p-v[0].p).sqrMagnitude>1e-14f) result.Add(new Poly(v,id));
                }
            }
            return result;
        }
        struct End { public Vector3 p,dir; public float half,low,high; public int mat; }
        static void AddCornerClosures(List<List<Poly>> solids, MeshFilter[] filters)
        {
            var ends=new List<End>();
            foreach(var f in filters) {
                var b=f.sharedMesh.bounds;
                // Only full-height jambs and piers meet other exterior segments.
                if(b.size.y<2.8f)continue;
                var m=home.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;
                int mat=materials.IndexOf(f.GetComponent<MeshRenderer>().sharedMaterial);
                for(int side=-1;side<=1;side+=2)ends.Add(new End{p=m.MultiplyPoint3x4(new Vector3(side<0?b.min.x:b.max.x,0,0)),dir=m.MultiplyVector(Vector3.right*-side).normalized,half=b.extents.z,low=m.MultiplyPoint3x4(new Vector3(0,b.min.y,0)).y,high=m.MultiplyPoint3x4(new Vector3(0,b.max.y,0)).y,mat=mat});
            }
            int count=0;
            for(int i=0;i<ends.Count;i++) for(int j=i+1;j<ends.Count;j++) {
                var a=ends[i];var b=ends[j];if(Vector3.Distance(a.p,b.p)>.002f||Mathf.Abs(Vector3.Dot(a.dir,b.dir))>.9999f)continue;
                var n1=Vector3.Cross(a.dir,Vector3.up);var n2=Vector3.Cross(b.dir,Vector3.up);if(Vector3.Dot(n1,n2)<0)n2=-n2;
                var bisector=(n1+n2)*Mathf.Min(a.half,b.half)/(1+Vector3.Dot(n1,n2));
                Vector3 p=(a.p+b.p)/2;p.y=0;
                var points=new[]{p+n1*a.half,p-n1*a.half,p+n2*b.half,p-n2*b.half,p+bisector,p-bisector}.Select(v=>new Vector2(v.x,v.z)).ToList();
                var hull=Hull(points);float low=Mathf.Min(a.low,b.low),high=Mathf.Max(a.high,b.high);
                var bottom=hull.Select(v=>new V(new Vector3(v.x,low,v.y),v)).ToList();
                var top=hull.AsEnumerable().Reverse().Select(v=>new V(new Vector3(v.x,high,v.y),v)).ToList();
                var polys=new List<Poly>{new Poly(bottom,a.mat),new Poly(top,a.mat)};
                for(int k=0;k<hull.Count;k++){
                    var s=hull[k];var t=hull[(k+1)%hull.Count];
                    polys.Add(new Poly(new List<V>{new V(new Vector3(s.x,low,s.y),new Vector2(s.x,low)),new V(new Vector3(s.x,high,s.y),new Vector2(s.x,high)),new V(new Vector3(t.x,high,t.y),new Vector2(t.x,high)),new V(new Vector3(t.x,low,t.y),new Vector2(t.x,low))},a.mat));
                }
                solids.Add(polys);count++;
            }
            report.Add("Closed "+count+" exterior corner joints with watertight miter volumes.");
        }
        static List<Vector2> Hull(List<Vector2> points)
        {
            points=points.OrderBy(v=>v.x).ThenBy(v=>v.y).ToList();var hull=new List<Vector2>();
            foreach(var p in points){while(hull.Count>=2&&Cross(hull[^1]-hull[^2],p-hull[^1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&Cross(hull[^1]-hull[^2],p-hull[^1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);return hull;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

        // BSP solid union. Boundary polygons retain their source material and interpolated UV0.
        class V {public Vector3 p;public Vector2 uv;public V(Vector3 p,Vector2 uv){this.p=p;this.uv=uv;}public V Lerp(V b,float t)=>new(Vector3.LerpUnclamped(p,b.p,t),Vector2.LerpUnclamped(uv,b.uv,t));}
        class Poly {
            public List<V> v;public Vector3 n;public float w;public int mat;
            public Poly(List<V> v,int mat){this.v=v;this.mat=mat;n=Vector3.Cross(v[1].p-v[0].p,v[2].p-v[0].p).normalized;w=Vector3.Dot(n,v[0].p);}
            public Poly Clone()=>new(new List<V>(v),mat);
            public void Flip(){v.Reverse();n=-n;w=-w;}
        }
        class Node {
            Vector3 n;float w;bool hasPlane;List<Poly> polygons=new();Node front,back;
            public Node(){}public Node(List<Poly> p){Build(p);}
            void Split(Poly p,List<Poly> cf,List<Poly> cb,List<Poly> f,List<Poly> b){
                int type=0;var types=new int[p.v.Count];for(int i=0;i<types.Length;i++){float d=Vector3.Dot(n,p.v[i].p)-w;types[i]=d<-Eps?2:d>Eps?1:0;type|=types[i];}
                if(type==0){(Vector3.Dot(n,p.n)>0?cf:cb).Add(p);return;}if(type==1){f.Add(p);return;}if(type==2){b.Add(p);return;}
                var fv=new List<V>();var bv=new List<V>();
                for(int i=0;i<p.v.Count;i++){int j=(i+1)%p.v.Count;var a=p.v[i];var z=p.v[j];int ti=types[i],tj=types[j];if(ti!=2)fv.Add(a);if(ti!=1)bv.Add(a);if((ti|tj)==3){float t=(w-Vector3.Dot(n,a.p))/Vector3.Dot(n,z.p-a.p);var cut=a.Lerp(z,t);fv.Add(cut);bv.Add(cut);}}
                Add(fv,f);Add(bv,b);
                void Add(List<V> v,List<Poly> target){for(int i=v.Count-1;i>=0;i--)if((v[i].p-v[(i+1)%v.Count].p).sqrMagnitude<1e-12f)v.RemoveAt(i);if(v.Count<3)return;int best=1;float area=0;for(int k=1;k<v.Count-1;k++){float a=Vector3.Cross(v[k].p-v[0].p,v[k+1].p-v[0].p).sqrMagnitude;if(a>area){area=a;best=k;}}if(area<1e-14f)return; // Rotate to a non-collinear triple before constructing the plane.
                    if(best!=1){int index=best;v=v.Skip(index).Concat(v.Take(index)).ToList();}
                    var poly=new Poly(v,p.mat);poly.n=p.n;poly.w=p.w;target.Add(poly);}
            }
            public void Invert(){foreach(var p in polygons)p.Flip();n=-n;w=-w;front?.Invert();back?.Invert();(front,back)=(back,front);}
            public List<Poly> Clip(List<Poly> p){if(!hasPlane)return new List<Poly>(p);var f=new List<Poly>();var b=new List<Poly>();foreach(var poly in p)Split(poly,f,b,f,b);if(front!=null)f=front.Clip(f);if(back!=null)b=back.Clip(b);else b.Clear();f.AddRange(b);return f;}
            public void ClipTo(Node b){polygons=b.Clip(polygons);front?.ClipTo(b);back?.ClipTo(b);}
            public List<Poly> All(){var result=new List<Poly>(polygons);if(front!=null)result.AddRange(front.All());if(back!=null)result.AddRange(back.All());return result;}
            public void Build(List<Poly> p){if(p.Count==0)return;if(!hasPlane){n=p[0].n;w=p[0].w;hasPlane=true;}var f=new List<Poly>();var b=new List<Poly>();foreach(var poly in p)Split(poly,polygons,polygons,f,b);if(f.Count>0){front??=new Node();front.Build(f);}if(b.Count>0){back??=new Node();back.Build(b);}}
        }
        static List<Poly> Union(List<Poly> x,List<Poly> y){var a=new Node(x);var b=new Node(y);a.ClipTo(b);b.ClipTo(a);b.Invert();b.ClipTo(a);b.Invert();a.Build(b.All());return a.All();}
        static Mesh MakeMesh(List<Poly> polygons,string name)
        {
            // Boolean intersections can differ by a few micrometres. Canonicalize nearby
            // positions by distance (not rounding) before creating shared borders.
            const float snap=.0002f;
            var cells=new Dictionary<Vector3Int,List<Vector3>>();
            foreach(var polygon in polygons) for(int i=0;i<polygon.v.Count;i++) {
                var vertex=polygon.v[i];var p=vertex.p;var cell=new Vector3Int(Mathf.FloorToInt(p.x/snap),Mathf.FloorToInt(p.y/snap),Mathf.FloorToInt(p.z/snap));
                bool found=false;Vector3 canonical=p;
                for(int x=-1;x<=1&&!found;x++)for(int y=-1;y<=1&&!found;y++)for(int z=-1;z<=1&&!found;z++)
                    if(cells.TryGetValue(cell+new Vector3Int(x,y,z),out var bucket))foreach(var candidate in bucket)if((candidate-p).sqrMagnitude<snap*snap){canonical=candidate;found=true;break;}
                if(!found){if(!cells.TryGetValue(cell,out var bucket)){bucket=new List<Vector3>();cells.Add(cell,bucket);}bucket.Add(p);}
                polygon.v[i]=new V(canonical,vertex.uv);
            }
            // Split T junctions along polygon borders before triangulating, so UV unwrapping sees matching edges.
            var all=polygons.SelectMany(p=>p.v).Select(v=>v.p).GroupBy(Key).Select(g=>g.First()).ToArray();
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=Enumerable.Range(0,materials.Count).Select(_=>new List<int>()).ToArray();var weld=new Dictionary<string,int>();
            foreach(var poly in polygons){
                var border=new List<V>();for(int i=0;i<poly.v.Count;i++){
                    var a=poly.v[i];var b=poly.v[(i+1)%poly.v.Count];var delta=b.p-a.p;float length=delta.sqrMagnitude;if(length<1e-12f)continue;
                    border.Add(a);var cuts=new List<(float t,Vector3 p)>();foreach(var p in all){float t=Vector3.Dot(p-a.p,delta)/length;if(t>1e-5f&&t<.99999f&&(p-a.p).sqrMagnitude>snap*snap&&(p-b.p).sqrMagnitude>snap*snap&&(a.p+delta*t-p).sqrMagnitude<snap*snap)cuts.Add((t,p));}foreach(var cut in cuts.OrderBy(c=>c.t))border.Add(new V(cut.p,Vector2.LerpUnclamped(a.uv,b.uv,cut.t)));
                }
                if(border.Count<3)continue;
                // A centre fan preserves all split border vertices, including collinear ones.
                var center=new V(border.Aggregate(Vector3.zero,(s,v)=>s+v.p)/border.Count,border.Aggregate(Vector2.zero,(s,v)=>s+v.uv)/border.Count);
                int c=Vertex(center,poly.n);
                for(int i=0;i<border.Count;i++){var a=border[i];var b=border[(i+1)%border.Count];if(Vector3.Cross(a.p-center.p,b.p-center.p).sqrMagnitude<1e-14f)continue;indices[poly.mat].AddRange(new[]{c,Vertex(a,poly.n),Vertex(b,poly.n)});}
            }
            // Snapping can collapse a microscopic solid sliver to two coincident faces.
            // Cancel both sides; keeping either leaves a non-manifold internal sheet.
            var triangleCounts=new Dictionary<string,int>();
            string TriangleKey(int a,int b,int c){var keys=new[]{Key(vertices[a]),Key(vertices[b]),Key(vertices[c])};Array.Sort(keys,StringComparer.Ordinal);return string.Join("/",keys);}
            foreach(var list in indices)for(int i=0;i<list.Count;i+=3){string key=TriangleKey(list[i],list[i+1],list[i+2]);triangleCounts.TryGetValue(key,out int count);triangleCounts[key]=count+1;}
            for(int sub=0;sub<indices.Length;sub++){var list=indices[sub];var clean=new List<int>();for(int i=0;i<list.Count;i+=3)if(triangleCounts[TriangleKey(list[i],list[i+1],list[i+2])]==1)clean.AddRange(new[]{list[i],list[i+1],list[i+2]});indices[sub]=clean;}
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=materials.Count;for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            UnwrapParam.SetDefaults(out var param);param.packMargin=8f/1024;param.hardAngle=88;Unwrapping.GenerateSecondaryUVSet(mesh,param);
            if(mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Missing lightmap UVs");
            foreach(var v in mesh.uv2)if(v.x<0||v.x>1||v.y<0||v.y>1)throw new Exception("UV2 outside atlas");
            return mesh;
            int Vertex(V v,Vector3 n){string key=Key(v.p)+"/"+Key(n)+"/"+Mathf.RoundToInt(v.uv.x*100000)+","+Mathf.RoundToInt(v.uv.y*100000);if(weld.TryGetValue(key,out int id))return id;id=vertices.Count;weld.Add(key,id);vertices.Add(v.p);normals.Add(n);uv.Add(v.uv);return id;}
        }
        static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*100000)+","+Mathf.RoundToInt(p.y*100000)+","+Mathf.RoundToInt(p.z*100000);
        static void CheckOpenings()
        {
            // Source marker locations are on the floor at each original opening centre.
            int passed=0;
            foreach(Transform marker in home.transform.Find("Openings")){
                bool window=marker.name.Contains("Window");var p=marker.position+home.transform.up*(window?1.6f:1);
                // Find the wall direction from the closest disabled source collider.
                var wall=home.transform.Find("Exterior").GetComponentsInChildren<BoxCollider>().Concat(home.transform.Find("Partitions").GetComponentsInChildren<BoxCollider>()).OrderBy(c=>(c.transform.position-marker.position).sqrMagnitude).First();
                // Check all horizontal axes, choose a path which traverses the wall at the opening.
                bool clear=false;foreach(var n in new[]{wall.transform.forward,wall.transform.right})if(!Physics.Raycast(p-n*.24f,n,.48f)){clear=true;break;}
                if(!clear)throw new Exception("Opening blocked "+marker.name);passed++;
            }
            report.Add(passed+" original opening centres checked; original colliders disabled only on replaced architecture.");
        }
        static void Preview(string suffix)
        {
            var temp=new GameObject("Seam validation camera");var c=temp.AddComponent<Camera>();c.CopyFrom(Camera.main);c.enabled=false;c.targetTexture=null;
            try{c.transform.position=home.transform.TransformPoint(new Vector3(1.77f,1.65f,-3.9f));c.transform.LookAt(home.transform.TransformPoint(new Vector3(1.8f,1.4f,-.2f)));Capture(c,Log+"/"+suffix+"-interior.png");}
            finally{UnityEngine.Object.DestroyImmediate(temp);}
        }
        static void Capture(Camera c,string path){var rt=new RenderTexture(1400,1000,24);var prior=RenderTexture.active;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);try{c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new RenderPipeline.StandardRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}finally{c.targetTexture=null;RenderTexture.active=prior;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}}
    }
}
