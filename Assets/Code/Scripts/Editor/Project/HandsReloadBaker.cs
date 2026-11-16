using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Offline retargeting only. The game plays the resulting transform clips without an IK solver.
public static class HandsReloadBaker
{
    public const string Folder = "Assets/Art/Animations/Weapons/Hands/Retargeted";
    public const string Model = "Assets/Level/Prefabs/Weapons/HandsReload.prefab";
    private const float Rate = 60f;
    private sealed class Pose
    {
        public Vector3 p, localP, scale; public Quaternion q, localQ;
        public Pose(Transform t) { p=t.position; q=t.rotation; localP=t.localPosition; localQ=t.localRotation; scale=t.localScale; }
    }
    private sealed class Rig
    {
        public GameObject model; public Dictionary<string,Transform> bones; public Dictionary<string,Pose> reference, bind;
        public Rig(string path, string clip)
        {
            model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            FindClip(path,clip).SampleAnimation(model,0);
            bones=model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
            reference=bones.ToDictionary(k=>k.Key,k=>new Pose(k.Value));
            bind=new Dictionary<string,Pose>();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            for(int i=0;i<skin.bones.Length;i++)
            {
                var matrix=skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[i].inverse;
                bind[skin.bones[i].name]=new Pose(skin.bones[i]){p=matrix.GetColumn(3),q=matrix.rotation};
            }
        }
        public Transform this[string name]=>bones[name];
        public Pose Ref(string name)=>reference[name];
        public void Reset() { foreach(var kv in reference) { var t=bones[kv.Key]; t.localPosition=kv.Value.localP; t.localRotation=kv.Value.localQ; t.localScale=kv.Value.scale; } }
    }
    public static AnimationClip FindClip(string path,string suffix)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__")&&c.name.EndsWith(suffix,StringComparison.Ordinal));
    public static void BakeAndPreview() { Run(); HandsReloadPreview.Run(); }
    [MenuItem("Tools/Weapon/Bake Arms reloads to Akito")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(HandsReloadAudit.Output);
        var source=new Rig(HandsReloadAudit.Source,"MK_Idle"); var target=new Rig(HandsReloadAudit.Target,"Idle");
        try
        {
            BuildModel();
            Bake(source,target,"MK_ReloadFast"); Bake(source,target,"MK_ReloadFull");
            AssetDatabase.SaveAssets(); Debug.Log("HANDS_RELOAD_BAKE_PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(source.model); UnityEngine.Object.DestroyImmediate(target.model); }
    }
    private static Quaternion Frame(Rig rig, bool source)
    {
        Vector3 forward=source ? rig["Slide_end"].position-rig["Slide"].position : rig["Base"].forward;
        Vector3 up=source ? rig["Mag_end"].position-rig["Mag"].position : -rig["Base"].up;
        return Quaternion.LookRotation(forward,up);
    }
    private static void Bake(Rig s,Rig t,string name)
    {
        s.Reset(); t.Reset();
        Quaternion align=Frame(t,false)*Quaternion.Inverse(Frame(s,true));
        var source=FindClip(HandsReloadAudit.Source,name);
        source.SampleAnimation(s.model,1.4f);
        var magazineSource=s.bones.ToDictionary(k=>k.Key,k=>new Pose(k.Value));
        FindClip(HandsReloadAudit.Target,"Reload").SampleAnimation(t.model,1.8f);
        var magazineTarget=t.bones.ToDictionary(k=>k.Key,k=>new Pose(k.Value));
        source.SampleAnimation(s.model,1.78f);
        var insertionSource=s.bones.ToDictionary(k=>k.Key,k=>new Pose(k.Value));
        FindClip(HandsReloadAudit.Target,"Reload").SampleAnimation(t.model,2.4f);
        var insertionTarget=t.bones.ToDictionary(k=>k.Key,k=>new Pose(k.Value));
        s.Reset();t.Reset();
        var clip=new AnimationClip { name=name+"_Akito",frameRate=Rate };
        var animated=t.model.GetComponentsInChildren<Transform>(true).Where(b=>b.name=="Akito Rig"||b.IsChildOf(t["Akito Rig"])).ToArray();
        var curves=animated.ToDictionary(b=>b,b=>Enumerable.Range(0,10).Select(_=>new AnimationCurve()).ToArray());
        var frames=new List<HandsReloadAudit.Frame>();
        float armScale=(Vector3.Distance(t.Ref("Upperarm.L").p,t.Ref("Forearm.L").p)+Vector3.Distance(t.Ref("Forearm.L").p,t.Ref("Hand.L").p)) /
            (Vector3.Distance(s.Ref("UpArm_L").p,s.Ref("Forearm_L").p)+Vector3.Distance(s.Ref("Forearm_L").p,s.Ref("Hand_L").p));
        float maxReachCorrection=0;
        int count=Mathf.RoundToInt(source.length*Rate);
        for(int i=0;i<=count;i++)
        {
            float time=Mathf.Min(i/Rate,source.length); s.Reset(); t.Reset(); source.SampleAnimation(s.model,time);
            var sr=s.Ref("Body"); var tr=t.Ref("Base");
            Quaternion delta=align*s["Body"].rotation*Quaternion.Inverse(sr.q)*Quaternion.Inverse(align);
            t["Base"].SetPositionAndRotation(tr.p+align*(s["Body"].position-sr.p)*armScale,delta*tr.q);
            // Target's misleadingly named Trigger actually skins the slide and its sights (503 vertices).
            TransferRelative(s,t,"Mag","Mag",align,1f);
            // Align the magazine to the target's well during the final insertion stroke.
            // The old clip keeps a six-degree tilt after its pivot has already reached the socket.
            if(time>1.5f)
            {
                Vector3 seated=t["Base"].position+delta*(t.Ref("Mag").p-tr.p);
                float gap=Vector3.Distance(t["Mag"].position,seated);
                float socket=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.0015f,.025f,gap));
                t["Mag"].rotation=Quaternion.Slerp(t["Mag"].rotation,delta*t.Ref("Mag").q,socket);
                if(gap<.0015f) t["Mag"].position=seated;
            }
            TransferRelative(s,t,"Slide","Trigger",align,1f);
            foreach(string side in new[]{"L","R"})
            {
                string sh="Hand_"+side, th="Hand."+side;
                var hand=RelativePose(s,t,sh,th,align,armScale);
                float grip=side=="L" ? Window(time,1.04f,1.2f,1.82f,1.98f) :
                    Window(time,.08f,.3f,source.length-.25f,source.length);
                float seating=side=="L"?Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.6f,1.73f,time)):0;
                if(grip>0)
                {
                    var contact=Contact(s,t,side=="L"?"Mag":"Body",side=="L"?"Mag":"Base",sh,th,magazineSource,magazineTarget,align,.85f);
                    if(seating>0)
                    {
                        var seated=Contact(s,t,"Mag","Mag",sh,th,insertionSource,insertionTarget,align,.85f);
                        contact.p=Vector3.Lerp(contact.p,seated.p,seating);contact.q=Quaternion.Slerp(contact.q,seated.q,seating);
                    }
                    hand.p=Vector3.Lerp(hand.p,contact.p,grip); hand.q=Quaternion.Slerp(hand.q,contact.q,grip);
                }
                if(side=="L" && name=="MK_ReloadFull")
                {
                    float slideGrip=Window(time,1.95f,2.1f,2.36f,2.57f);
                    Vector3 slidePosition=t["Trigger"].position+align*(s[sh].position-s["Slide"].position)*.85f;
                    Quaternion anatomical=BindFrame(t,side,false)*Quaternion.Inverse(BindFrame(s,side,true));
                    Quaternion axes=Quaternion.Inverse(s.bind[sh].q)*Quaternion.Inverse(anatomical)*t.bind[th].q;
                    Quaternion slideRotation=align*s[sh].rotation*axes;
                    hand.p=Vector3.Lerp(hand.p,slidePosition,slideGrip);hand.q=Quaternion.Slerp(hand.q,slideRotation,slideGrip);
                }
                maxReachCorrection=Mathf.Max(maxReachCorrection,SolveArm(s,t,side,hand.p,hand.q,align));
                Fingers(s,t,side,grip,seating,magazineSource,magazineTarget,insertionSource,insertionTarget);
                Twist(t,side);
            }
            // Match native Idle at both boundaries, including elbow/sleeve roll.
            // Contacts and the original insertion stroke are outside these short windows.
            float boundary=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.1f,time))*
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(source.length,source.length-.12f,time));
            foreach(var bone in animated)
            {
                var rest=t.Ref(bone.name);
                bone.localPosition=Vector3.Lerp(rest.localP,bone.localPosition,boundary);
                bone.localRotation=Quaternion.Slerp(rest.localQ,bone.localRotation,boundary);
                bone.localScale=Vector3.Lerp(rest.scale,bone.localScale,boundary);
            }
            foreach(var b in animated)
            {
                var c=curves[b]; var p=b.localPosition;var q=b.localRotation;var sc=b.localScale;
                if(i>0 && c[3][i-1].value*q.x+c[4][i-1].value*q.y+c[5][i-1].value*q.z+c[6][i-1].value*q.w<0) q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,sc.x,sc.y,sc.z};
                for(int j=0;j<10;j++) c[j].AddKey(time,values[j]);
            }
            frames.Add(new HandsReloadAudit.Frame{time=time,bones=HandsReloadAudit.Poses(t.model)});
        }
        string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
        foreach(var b in animated)
        for(int j=0;j<10;j++)
        {
            // Quaternion components must share key times: Mecanim resampling independent
            // reduced components introduced visible accumulated finger rotation errors.
            var curve=j>=3&&j<7?curves[b][j]:Reduce(curves[b][j],j<3?1e-8f:1e-7f);
            for(int k=0;k<curve.length;k++) { AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear); }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(b,t.model.transform),typeof(Transform),properties[j]),curve);
        }
        clip.EnsureQuaternionContinuity();
        float insertion=FindInsertion(frames);
        AnimationUtility.SetAnimationEvents(clip,new[]{new AnimationEvent{functionName="OnMagazineInserted",time=insertion,intParameter=0,messageOptions=SendMessageOptions.RequireReceiver}});
        var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=false; settings.loopBlend=false;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        string path=Folder+"/"+clip.name+".anim";
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing==null) AssetDatabase.CreateAsset(clip,path); else { EditorUtility.CopySerialized(clip,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(clip); }
        File.WriteAllText(Path.Combine(HandsReloadAudit.Output,name+"-baked.json"),JsonUtility.ToJson(new HandsReloadAudit.Clip {name=name,length=source.length,fps=Rate,frames=frames.ToArray()}));
        File.WriteAllText(Path.Combine(HandsReloadAudit.Output,name+"-metrics.txt"),"Arm length ratio="+armScale+"; maximum shoulder reach correction (m)="+maxReachCorrection+"; insertion seconds="+insertion+"; insertion phase="+(insertion/source.length));
    }
    private static AnimationCurve Reduce(AnimationCurve source,float tolerance)
    {
        var keys=source.keys;var keep=new SortedSet<int>{0,keys.Length-1};
        Action<int,int> split=null;
        split=(a,b)=> {
            int worst=-1;float error=tolerance;
            for(int i=a+1;i<b;i++)
            {
                float value=Mathf.Lerp(keys[a].value,keys[b].value,(keys[i].time-keys[a].time)/(keys[b].time-keys[a].time));
                float difference=Mathf.Abs(value-keys[i].value);
                if(difference>error) {error=difference;worst=i;}
            }
            if(worst<0)return;keep.Add(worst);split(a,worst);split(worst,b);
        };
        split(0,keys.Length-1);
        return new AnimationCurve(keep.Select(i=>new Keyframe(keys[i].time,keys[i].value)).ToArray());
    }
    private static float FindInsertion(List<HandsReloadAudit.Frame> frames)
    {
        Func<HandsReloadAudit.Frame,Vector3> relative=f=> {
            var b=f.bones.Single(p=>p.path=="Akito Rig/Base");var m=f.bones.Single(p=>p.path=="Akito Rig/Mag");
            return Quaternion.Inverse(b.worldRotation)*(m.worldPosition-b.worldPosition); };
        Vector3 seated=relative(frames[0]); bool departed=false;
        for(int i=0;i<frames.Count-6;i++)
        {
            float distance=Vector3.Distance(relative(frames[i]),seated);
            if(distance>.08f) departed=true;
            if(departed && distance<.0005f && frames.Skip(i).Take(7).All(f=>Vector3.Distance(relative(f),seated)<.0005f)) return frames[i].time;
        }
        throw new InvalidOperationException("No stable magazine seating frame in baked reload");
    }
    private static void BuildModel()
    {
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HandsReloadAudit.Target));
        model.name="HandsReload";
        try
        {
            var gun=model.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="Object001");
            var source=gun.sharedMesh;var parts=HandsReloadPreview.Components(source);
            // The original contains a second, static copy of the magazine shell and floorplate.
            // Keep the 180 vertices weighted to Mag; remove only the duplicate Base-weighted faces.
            int[] duplicates={6,16};int[] counts={114,66};
            var hidden=new HashSet<int>();
            for(int i=0;i<duplicates.Length;i++)
            {
                var part=parts[duplicates[i]];
                if(part.Length!=counts[i]||part.Any(v=>gun.bones[source.boneWeights[v].boneIndex0].name!="Base"))
                    throw new InvalidOperationException("Pistol topology changed; inspect duplicate magazine before baking");
                foreach(int v in part) hidden.Add(v);
            }
            var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Hands pistol - single animated magazine";
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var triangles=source.GetTriangles(sub); var keep=new List<int>();
                for(int i=0;i<triangles.Length;i+=3) if(!hidden.Contains(triangles[i])) keep.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});
                mesh.SetTriangles(keep,sub);
            }
            string path="Assets/Art/Models/Weapons/Hands/Derived/HandsPistolReloadMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) AssetDatabase.CreateAsset(mesh,path); else { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
            gun.sharedMesh=mesh;
            PrefabUtility.SaveAsPrefabAsset(model,Model);
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }
    private static Pose RelativePose(Rig s,Rig t,string source,string target,Quaternion align,float scale)
    {
        var sb=s.Ref("Body");var tb=t.Ref("Base");var sp=s.Ref(source);var tp=t.Ref(target);
        var bodyDelta=t["Base"].rotation*Quaternion.Inverse(tb.q);
        Vector3 current=Quaternion.Inverse(s["Body"].rotation)*(s[source].position-s["Body"].position);
        Vector3 original=Quaternion.Inverse(sb.q)*(sp.p-sb.p);
        var result=new Pose(t[target]);
        result.p=t["Base"].position+bodyDelta*(tp.p-tb.p+align*sb.q*(current-original)*scale);
        Quaternion relative=Quaternion.Inverse(s["Body"].rotation)*s[source].rotation;
        Quaternion reference=Quaternion.Inverse(sb.q)*sp.q;
        result.q=bodyDelta*align*sb.q*relative*Quaternion.Inverse(reference)*Quaternion.Inverse(sb.q)*Quaternion.Inverse(align)*tp.q;
        return result;
    }
    private static void TransferRelative(Rig s,Rig t,string source,string target,Quaternion align,float scale)
    {
        var p=RelativePose(s,t,source,target,align,scale); t[target].SetPositionAndRotation(p.p,p.q);
    }
    private static float SolveArm(Rig s,Rig t,string side,Vector3 wrist,Quaternion handRotation,Quaternion align)
    {
        var upper=t["Upperarm."+side];var elbow=t["Forearm."+side];var hand=t["Hand."+side];
        var a=t.Ref(upper.name);var b=t.Ref(elbow.name);var h=t.Ref(hand.name);
        float length1=Vector3.Distance(a.p,b.p),length2=Vector3.Distance(b.p,h.p);
        Vector3 shoulder=a.p, direction=(wrist-shoulder).normalized;
        float distance=Vector3.Distance(shoulder,wrist), correction=Mathf.Max(0,distance-(length1+length2)*.999f);
        shoulder+=direction*correction; distance-=correction;
        Vector3 sourcePlane=align*(s["Forearm_"+side].position-s["UpArm_"+side].position);
        Vector3 pole=Vector3.ProjectOnPlane(sourcePlane,direction).normalized;
        if(pole.sqrMagnitude<.01f) pole=Vector3.ProjectOnPlane(b.p-a.p,direction).normalized;
        float along=(length1*length1-length2*length2+distance*distance)/(2*Mathf.Max(distance,.0001f));
        Vector3 elbowPoint=shoulder+direction*along+pole*Mathf.Sqrt(Mathf.Max(0,length1*length1-along*along));
        upper.position=shoulder;
        upper.rotation=Quaternion.FromToRotation(b.p-a.p,elbowPoint-shoulder)*a.q;
        elbow.rotation=Quaternion.FromToRotation(h.p-b.p,wrist-elbowPoint)*b.q;
        hand.rotation=handRotation;
        return correction;
    }
    private static float Window(float time,float start,float full,float release,float end)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,full,time))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(release,end,time)));
    private static Pose Contact(Rig s,Rig t,string sa,string ta,string sh,string th,Dictionary<string,Pose> sr,Dictionary<string,Pose> tr,Quaternion align,float scale)
    {
        Quaternion axes=Quaternion.Inverse(tr[ta].q)*align*sr[sa].q;
        Vector3 current=Quaternion.Inverse(s[sa].rotation)*(s[sh].position-s[sa].position);
        Vector3 original=Quaternion.Inverse(sr[sa].q)*(sr[sh].p-sr[sa].p);
        Vector3 target=Quaternion.Inverse(tr[ta].q)*(tr[th].p-tr[ta].p);
        Quaternion rotation=Quaternion.Inverse(s[sa].rotation)*s[sh].rotation;
        Quaternion originalRotation=Quaternion.Inverse(sr[sa].q)*sr[sh].q;
        return new Pose(t[th]) {
            p=t[ta].position+t[ta].rotation*(target+axes*(current-original)*scale),
            q=t[ta].rotation*axes*rotation*Quaternion.Inverse(originalRotation)*Quaternion.Inverse(axes)*Quaternion.Inverse(tr[ta].q)*tr[th].q };
    }
    private static Quaternion BindFrame(Rig r,string side,bool source)
    {
        var hand=r.bind[source?"Hand_"+side:"Hand."+side].p;
        var index=r.bind[source?"Bone_"+side+".005":"Index."+side+".004"].p;
        var middle=r.bind[source?"Bone_"+side+".009":"Middle."+side+".001"].p;
        var pinky=r.bind[source?"Bone_"+side+".017":"Pinky."+side+".001"].p;
        return Quaternion.LookRotation(Vector3.Cross(index-pinky,middle-hand).normalized,(middle-hand).normalized);
    }
    private static void Fingers(Rig s,Rig t,string side,float grip,float seating,Dictionary<string,Pose> sr,Dictionary<string,Pose> tr,Dictionary<string,Pose> si,Dictionary<string,Pose> ti)
    {
        string[] targetNames={"Index","Middle","Ring","Pinky"}; int[] starts={4,8,12,16};
        for(int finger=0;finger<4;finger++)
        for(int joint=0;joint<4;joint++)
        {
            string stem=targetNames[finger]+"."+side;
            string target=joint==0?stem:stem+"."+(finger==0?joint+3:joint).ToString("D3");
            Finger(s,t,"Bone_"+side+"."+(starts[finger]+joint).ToString("D3"),target,side,grip,seating,sr,tr,si,ti);
        }
        t["Thumb."+side].localRotation=Quaternion.Slerp(t.Ref("Thumb."+side).localQ,Quaternion.Slerp(tr["Thumb."+side].localQ,ti["Thumb."+side].localQ,seating),grip);
        for(int joint=0;joint<3;joint++) Finger(s,t,"Bone_"+side+"."+(20+joint).ToString("D3"),"Thumb."+side+"."+(joint+1).ToString("D3"),side,grip,seating,sr,tr,si,ti);
    }
    private static void Finger(Rig s,Rig t,string source,string target,string side,float grip,float seating,Dictionary<string,Pose> sr,Dictionary<string,Pose> tr,Dictionary<string,Pose> si,Dictionary<string,Pose> ti)
    {
        Quaternion anatomical=BindFrame(t,side,false)*Quaternion.Inverse(BindFrame(s,side,true));
        Quaternion axes=Quaternion.Inverse(t.bind[target].q)*anatomical*s.bind[source].q;
        Quaternion delta=Quaternion.Inverse(s.Ref(source).localQ)*s[source].localRotation;
        Quaternion free=t.Ref(target).localQ*axes*delta*Quaternion.Inverse(axes);
        Quaternion contactDelta=Quaternion.Inverse(sr[source].localQ)*s[source].localRotation;
        Quaternion held=tr[target].localQ*axes*contactDelta*Quaternion.Inverse(axes);
        Quaternion inserted=ti[target].localQ*axes*(Quaternion.Inverse(si[source].localQ)*s[source].localRotation)*Quaternion.Inverse(axes);
        held=Quaternion.Slerp(held,inserted,seating);
        t[target].localRotation=Quaternion.Slerp(free,held,grip);
    }
    private static void Twist(Rig t,string side)
    {
        var forearm=t["Forearm."+side];var hand=t["Hand."+side];
        Quaternion swing=forearm.rotation*Quaternion.Inverse(t.Ref(forearm.name).q);
        Quaternion change=hand.rotation*Quaternion.Inverse(swing*t.Ref(hand.name).q);
        Vector3 axis=(hand.position-forearm.position).normalized;
        Vector3 vector=Vector3.Project(new Vector3(change.x,change.y,change.z),axis);
        Quaternion twist=new Quaternion(vector.x,vector.y,vector.z,change.w).normalized;
        string[] names={"Twist 3.","Twist 2.","Twist."};float[] blend={.25f,.5f,.85f};
        for(int i=0;i<3;i++) t[names[i]+side].rotation=Quaternion.Slerp(Quaternion.identity,twist,blend[i])*swing*t.Ref(names[i]+side).q;
    }
}
