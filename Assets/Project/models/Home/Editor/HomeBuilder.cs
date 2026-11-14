using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TripleRep.Home.Editor
{
 public static class HomeBuilder
 {
  const string Folder="Assets/Project/models/Home", PrefabPath=Folder+"/Home.prefab", ScenePath="Assets/Project/Scenes/Home.unity";
  const float H=2.9f;
  [InitializeOnLoadMethod] static void PendingBuild(){if(File.Exists("Logs/Home/build.request")) EditorApplication.delayCall+=()=>{File.Delete("Logs/Home/build.request");try{BuildAndPreview();}catch(Exception e){File.WriteAllText("Logs/Home/error.txt",e.ToString());Debug.LogException(e);}};}

  static GameObject root;
  static Material plaster,wood,tile,white;
  static readonly List<(Vector3 p, Vector3 normal,float thickness,string name)> openings=new();
  // Digitized plan coordinates, calibrated to the labelled clear room dimensions.
  static float X(float p)=> (p-35)*3.556f/332;
  static float D(float p) {
   float[] px={50,525,720,992,1060,1433,1577,1590};
   float[] m={0,4.8768f,6.97f,9.9164f,10.4612f,14.6412f,16.1f,16.445f};
   int i=0; while(i<px.Length-2 && p>px[i+1]) i++;
   return Mathf.LerpUnclamped(m[i],m[i+1],(p-px[i])/(px[i+1]-px[i]));
  }
  static Vector3 P(float x,float d,float y=0)=>new(X(x),y,-D(d));
  static Transform Group(string name) {var t=root.transform.Find(name); if(t!=null)return t; var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g.transform;}
  static Material Mat(string name,Color color,float smooth) {
   string path=Folder+"/Materials/"+name+".mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
   m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",0);return m;
  }
  static void Box(string group,string name,Vector3 center,Vector3 size,Material mat,Quaternion rotation) {
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(Group(group),false);
   go.transform.localPosition=center;go.transform.localRotation=rotation;
   // Bake dimensions into a saved mesh, keeping metre-scale UVs and unit transforms.
   var source=go.GetComponent<MeshFilter>().sharedMesh; var mesh=UnityEngine.Object.Instantiate(source);mesh.name=name;
   var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];
   for(int i=0;i<v.Length;i++){v[i]=Vector3.Scale(v[i],size);uv[i]=Mathf.Abs(n[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(n[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y);}
   mesh.vertices=v;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateTangents();Unwrapping.GenerateSecondaryUVSet(mesh);
   SaveMesh(mesh,group+"_"+name,go,mat);go.GetComponent<BoxCollider>().size=size;
  }
  static void SaveMesh(Mesh mesh,string name,GameObject go,Material mat){
   string path=Folder+"/Meshes/NY_"+name+".asset";var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(stored==null){AssetDatabase.CreateAsset(mesh,path);stored=mesh;}else{EditorUtility.CopySerialized(mesh,stored);UnityEngine.Object.DestroyImmediate(mesh);}
   go.GetComponent<MeshFilter>().sharedMesh=stored;go.GetComponent<MeshRenderer>().sharedMaterial=mat;
  }
  static void Segment(string group,string name,Vector3 a,Vector3 b,float thick,float low=0,float high=H,Material mat=null){
   Vector3 delta=b-a;float len=delta.magnitude;
   Box(group,name,(a+b)/2+Vector3.up*((low+high)/2),new Vector3(len,high-low,thick),mat??plaster,Quaternion.FromToRotation(Vector3.right,delta.normalized));
  }
  // A genuine opening is made from jamb sections, lintel and (for windows) sill.
  static void Wall(string name,float x0,float d0,float x1,float d1,float thick=.12f,float start=-1,float width=.85f,bool window=false){
   var a=P(x0,d0);var b=P(x1,d1);var dir=(b-a).normalized;float length=Vector3.Distance(a,b);
   string group=thick>.25f?"Exterior":"Partitions";
   if(start<0){Segment(group,name,a,b,thick);return;}
   if(start+width>length+.001f)throw new Exception("Opening exceeds wall "+name);
   var c=a+dir*start;var e=c+dir*width;
   if(start>.001f)Segment(group,name+"_jambA",a,c,thick);
   if(length-start-width>.001f)Segment(group,name+"_jambB",e,b,thick);
   Segment(group,name+"_lintel",c,e,thick,window?2.4f:2.1f,H);
   if(window){Segment(group,name+"_sill",c,e,thick,0,.8f);Segment("WindowFrames",name+"_bottom",c,e,.075f,.8f,.85f,white);Segment("WindowFrames",name+"_top",c,e,.075f,2.35f,2.4f,white);
    Segment("WindowFrames",name+"_left",c,c+dir*.045f,.075f,.85f,2.35f,white);Segment("WindowFrames",name+"_right",e-dir*.045f,e,.075f,.85f,2.35f,white);
   }
   var normal=Vector3.Cross(dir,Vector3.up);openings.Add(((c+e)/2+Vector3.up*(window?1.6f:1),normal,thick,name));
   var marker=new GameObject(name+ (window?" Window":" Door")+" "+width.ToString("F3")+"m");marker.transform.SetParent(Group("Openings"),false);marker.transform.position=(c+e)/2;
  }
  static void Geometry(){
   // External centre lines. North polygonal bay and south curved bay.
   Wall("West",24,142,24,1454,.35f);Wall("East",582,142,582,1454,.35f);
   Wall("NorthBayLeft",24,142,133,49,.35f,.28f,.85f,true);
   Wall("NorthBayFront",133,49,275,49,.35f,.34f,.8f,true);
   Wall("NorthBayRight",275,49,358,142,.35f,.21f,.88f,true);
   Wall("BathWindow",358,142,582,142,.35f,.64f,.78f,true);
   Wall("OfficeWindow",365,1454,582,1454,.35f,.85f,.72f,true);
   float[,] curve={{24,1454},{65,1502},{113,1541},{158,1566},{207,1577},{256,1567},{305,1543},{345,1508},{365,1454}};
   for(int i=0;i<8;i++){
    float x0=curve[i,0],d0=curve[i,1],x1=curve[i+1,0],d1=curve[i+1,1];float l=Vector3.Distance(P(x0,d0),P(x1,d1));
    bool win=i==1||i==3||i==4||i==6;
    Wall("SouthBay"+i,x0,d0,x1,d1,.35f,win?.06f:-1,win?l-.12f:.85f,win);
   }
   // Upper bedroom, ensuite with chamfer, roof access vestibule.
   Wall("BedroomEast",373,142,373,438,.12f,.7f,.78f);
   Wall("EnsuiteChamfer",373,426,469,467);
   Wall("RoofWest",469,467,469,677,.12f,.95f,.8f);
   Wall("RoofNorth",469,524,582,524);
   Wall("RoofSouth",469,677,582,677);
   Wall("BedroomSouthWest",24,531,174,531,.12f,.65f,.78f);
   Wall("BedroomSouth",174,531,373,531);
   Wall("UpperHallDoor",373,438,373,713,.12f,1.1f,.82f);
   Wall("BathWest",174,531,174,713);
   Wall("BathSouth",174,713,373,713);
   // Den clear width 3.1242 m (x=75.3 to 367); clear depth 2.9464 m.
   Wall("DenEast",373,713,373,998,.12f,1.85f,.85f);
   Wall("DenSouth",145,998,373,998);
   Wall("DenWardrobeLeft",187,713,187,765);Wall("DenWardrobeFront",187,765,373,765,.10f,.18f,1.62f);
   Wall("ClosetUpperTop",24,713,80,713);Wall("ClosetUpperFront",81,713,81,820,.12f,.08f,.8f);Wall("ClosetUpperBottom",24,820,81,820);
   Wall("ClosetLowerTop",24,878,81,878);Wall("ClosetLowerFront",81,878,81,998,.12f,.10f,.85f);Wall("ClosetLowerBottom",24,998,145,998);
   Wall("PrimaryClosetLeft",145,998,145,1066);Wall("PrimaryClosetBottom",24,1066,145,1066,.12f,.12f,.82f);
   Wall("PrimaryClosetRight",269,998,269,1066);Wall("PrimaryClosetRightBottom",269,1066,373,1066,.12f,.12f,.78f);
   Wall("PrimaryEntry",373,998,373,1163,.12f,.75f,.86f);
   Wall("OfficeDivider",373,1163,373,1454);
   Wall("OfficeEntry",373,1163,490,1163,.12f,.2f,.85f);
   Wall("OfficeClosetLeft",490,1066,490,1163);Wall("OfficeClosetTop",490,1066,582,1066);
   Wall("OfficeClosetDoor",490,1163,582,1163,.12f,.08f,.78f);
   Slabs();
  }
  // Horizontal strips cover the irregular footprint without filling the stairwell.
  static void Slabs(){
   float[] ds={49,142,426,531,713,785,998,1066,1163,1454,1502,1508,1541,1543,1566,1567,1577};
   for(int i=0;i<ds.Length-1;i++){
    float a=ds[i],b=ds[i+1];
    if(a>=785 && b<=1066){Strip("Floors","F"+i,24,465,24,465,a,b,-.18f,0,wood);Strip("Floors","R"+i,575,582,575,582,a,b,-.18f,0,wood);}
    else Strip("Floors","F"+i,Left(a),(a==1454?365:Right(a)),Left(b),(b==142?358:Right(b)),a,b,-.18f,0,wood);
    Strip("Ceiling","C"+i,Left(a),(a==1454?365:Right(a)),Left(b),(b==142?358:Right(b)),a,b,H,H+.16f,white);
   }
   Strip("FloorTiles","Ensuite",379,570,379,570,151,420,0,.008f,tile);
   Strip("FloorTiles","Bathroom",180,367,180,367,537,707,0,.008f,tile);
   // Stairs descend below this floor to the unmodelled lower storey.
   for(int i=0;i<16;i++) Box("Stairs","Tread"+i,P(520,800+i*16.5f,-.18f*(i+1)),new Vector3(1.1f,.18f,.18f),wood,Quaternion.identity);
   Segment("Stairs","Guardrail",P(462,795),P(462,1060),.045f,.98f,1.03f,white);
   for(int i=0;i<10;i++)Box("Stairs","Baluster"+i,P(462,800+i*28, .5f),new Vector3(.035f,1,.035f),white,Quaternion.identity);
  }
  static float Left(float d){if(d<=142)return Mathf.Lerp(133,24,(d-49)/93);if(d<=1454)return 24;float[] ds={1454,1502,1541,1566,1577};float[] xs={24,65,113,158,207};for(int i=0;i<4;i++)if(d<=ds[i+1])return Mathf.Lerp(xs[i],xs[i+1],(d-ds[i])/(ds[i+1]-ds[i]));return 207;}
  static float Right(float d){if(d<142)return Mathf.Lerp(275,358,(d-49)/93);if(d<=1454)return 582;float[] ds={1454,1508,1543,1567,1577};float[] xs={365,345,305,256,207};for(int i=0;i<4;i++)if(d<=ds[i+1])return Mathf.Lerp(xs[i],xs[i+1],(d-ds[i])/(ds[i+1]-ds[i]));return 207;}
  static void Strip(string group,string name,float l0,float r0,float l1,float r1,float d0,float d1,float low,float high,Material mat){
   var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
   Vector3 a=P(l0,d0,low),b=P(r0,d0,low),c=P(r1,d1,low),d=P(l1,d1,low);Vector3 up=Vector3.up*(high-low);
   Face(a,b,c,d);Face(d+up,c+up,b+up,a+up);Face(a,a+up,b+up,b);Face(b,b+up,c+up,c);Face(c,c+up,d+up,d);Face(d,d+up,a+up,a);
   var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
   var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(Group(group),false);SaveMesh(mesh,group+"_"+name,go,mat);go.GetComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
   void Face(Vector3 p,Vector3 q,Vector3 r,Vector3 s){int n=v.Count;v.AddRange(new[]{p,q,r,s});var normal=Vector3.Cross(q-p,r-p).normalized;foreach(var pos in new[]{p,q,r,s})uv.Add(Mathf.Abs(normal.y)>.5f?new Vector2(pos.x,pos.z):new Vector2(Vector3.Dot(pos,(q-p).normalized),pos.y));if(Vector3.Cross(q-p,r-p).sqrMagnitude>1e-10f)tri.AddRange(new[]{n,n+2,n+1});if(Vector3.Cross(r-p,s-p).sqrMagnitude>1e-10f)tri.AddRange(new[]{n,n+3,n+2});}
  }
  [MenuItem("Tools/Home/Rebuild model and place in Home scene")]
  public static void Build(){
   if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");
   plaster=Mat("Warm plaster",new Color(.82f,.79f,.71f),.15f);wood=Mat("Floor",new Color(.42f,.27f,.14f),.25f);tile=Mat("Bathroom floor",new Color(.48f,.57f,.59f),.3f);white=Mat("Ceiling",new Color(.93f,.92f,.88f),.18f);
   root=new GameObject("Home");openings.Clear();Geometry();HomeLightmapRepair.PrepareModel(root);
   PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
   var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
   foreach(var old in scene.GetRootGameObjects())if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old)==PrefabPath||old.name=="Home Preview Lighting")UnityEngine.Object.DestroyImmediate(old);
   root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);
   var rig=new GameObject("Home Preview Lighting");
   foreach(var pos in new[]{P(200,300,2.5f),P(490,290,2.5f),P(270,620,2.5f),P(220,880,2.5f),P(200,1270,2.5f),P(480,1290,2.5f),P(420,800,2.5f)}){
    var light=new GameObject("Interior fill").AddComponent<Light>();light.transform.SetParent(rig.transform);light.transform.position=pos;light.type=LightType.Point;light.intensity=2;light.range=5;light.shadows=LightShadows.Soft;
   }
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.48f,.48f);
   var cam=Camera.main;if(cam==null){cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";}
   cam.transform.position=P(200,450,1.65f);cam.transform.LookAt(P(210,110,1.4f));cam.nearClipPlane=.03f;cam.farClipPlane=100;cam.fieldOfView=75;cam.orthographic=false;
   EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Validate();Debug.Log("HOME_BUILD_OK");
  }
  [MenuItem("Tools/Home/Validate saved model and scene")]
  public static void Validate(){
   root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==PrefabPath);
   Physics.SyncTransforms();
   foreach(var o in openings)if(Physics.Raycast(o.p-o.normal*(o.thickness/2+.04f),o.normal,o.thickness+.08f))throw new Exception("Blocked opening: "+o.name);
   foreach(var f in root.GetComponentsInChildren<MeshFilter>()){
    if(f.sharedMesh==null||f.sharedMesh.uv.Length!=f.sharedMesh.vertexCount||f.sharedMesh.uv2.Length!=f.sharedMesh.vertexCount)throw new Exception("Invalid mesh UVs "+f.name);
    var mat=f.GetComponent<MeshRenderer>().sharedMaterial;if(mat==null||mat.shader.name!="Universal Render Pipeline/Lit"||!mat.shader.isSupported)throw new Exception("Invalid material "+f.name);
   }
   foreach(var p in new[]{P(200,300,1),P(220,850,1),P(200,1300,1),P(480,1300,1)}){
    if(!Physics.Raycast(p,Vector3.down,1.05f))throw new Exception("Missing floor");
    if(!Physics.Raycast(p,Vector3.up,1.95f))throw new Exception("Missing ceiling");
   }
   if(Physics.Raycast(P(520,900,.1f),Vector3.down,.15f))throw new Exception("Stairwell filled");
   Directory.CreateDirectory("Logs/Home");File.WriteAllText("Logs/Home/validation.txt","PASS: "+openings.Count+" openings clear; saved mesh UV0/UV2 and URP materials valid; floors and ceilings verified in four rooms; stairwell clear.\n");Debug.Log("HOME_VALIDATION_OK");
  }
  [MenuItem("Tools/Home/Build and render previews")]
  public static void BuildAndPreview(){Build();var cam=Camera.main;Directory.CreateDirectory("Logs/Home");Render(cam,"Logs/Home/interior.png",1400,1000);
   root.transform.Find("LightmapReady/Ceiling").gameObject.SetActive(false);cam.orthographic=true;cam.orthographicSize=9.3f;cam.transform.position=new Vector3(2.8f,24,-8);cam.transform.rotation=Quaternion.Euler(90,0,0);Render(cam,"Logs/Home/plan.png",900,1700);
   cam.orthographicSize=10;cam.transform.position=new Vector3(19,23,-25);cam.transform.LookAt(new Vector3(2.8f,0,-8));Render(cam,"Logs/Home/overview.png",1400,1400);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Debug.Log("HOME_PREVIEW_OK");}
  static void Render(Camera camera,string path,int width,int height){var target=new RenderTexture(width,height,24);var old=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);camera.targetTexture=target;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
 }
}
