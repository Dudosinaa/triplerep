using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HandsReloadRoundTrip
{
    public static void Run()
    {
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HandsReloadBaker.Model));
        float positionError=0,rotationError=0;int samples=0;string worst="";
        try
        {
            foreach(string name in new[]{"MK_ReloadFast","MK_ReloadFull"})
            {
                var expected=JsonUtility.FromJson<HandsReloadAudit.Clip>(File.ReadAllText(Path.Combine(HandsReloadAudit.Output,name+"-baked.json")));
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(HandsReloadBaker.Folder+"/"+name+"_Akito.anim");
                foreach(var frame in expected.frames)
                {
                    clip.SampleAnimation(model,frame.time);
                    foreach(var pose in frame.bones.Where(p=>p.path.StartsWith("Akito Rig/",StringComparison.Ordinal)))
                    {
                        var bone=model.transform.Find(pose.path);
                        positionError=Mathf.Max(positionError,Vector3.Distance(bone.position,pose.worldPosition));
                        float angle=Quaternion.Angle(bone.rotation,pose.worldRotation);
                        if(angle>rotationError){rotationError=angle;worst=name+" at "+frame.time+" "+pose.path;}
                        samples++;
                    }
                }
            }
            string report="Samples="+samples+"; max position error m="+positionError+"; max rotation error degrees="+rotationError+"; worst="+worst;
            File.WriteAllText(Path.Combine(HandsReloadAudit.Output,"roundtrip.txt"),report);
            if(positionError>.001f||rotationError>.15f)throw new InvalidOperationException(report);
            Debug.Log("HANDS_ROUNDTRIP_PASS "+report);
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
        HandsReloadPreview.Run();
    }
}
