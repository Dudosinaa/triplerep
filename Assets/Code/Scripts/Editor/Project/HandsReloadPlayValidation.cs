using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Batch-only integration checks. Run on a disposable copy, never on the user's open scene.
[InitializeOnLoad]
public static class HandsReloadPlayValidation
{
    private const string Running="HandsReloadPlayValidation.Running";
    private static IEnumerator routine;
    private static Func<bool> pending;
    private static int lastFrame=-1, assertions;
    private static double deadline;
    private static readonly List<string> results=new List<string>();
    private static readonly List<string> errors=new List<string>();
    static HandsReloadPlayValidation() { EditorApplication.playModeStateChanged+=Changed; EditorApplication.update+=Tick; }
    [MenuItem("Tools/Weapon/Run reload play checks")]
    public static void RunBatch()
    {
        if(!Application.isBatchMode&&!Environment.GetCommandLineArgs().Contains("-handsReloadValidation")) throw new InvalidOperationException("Run checks in a separate validation copy with -handsReloadValidation");
        HandsReloadIntegration.Run();
        Directory.CreateDirectory(HandsReloadAudit.Output);
        EditorSceneManager.OpenScene("Assets/Level/Scenes/Gameplay/CarxDemo.unity");
        SessionState.SetBool(Running,true);
        EditorApplication.EnterPlaymode();
    }
    private static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Running,false))return;
        Application.logMessageReceived+=Log;
        // Batch Editor has no focused Game view. Supply focus only in this test process,
        // immediately before gameplay Update; all modal/component gates remain real.
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var loop=UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        for(int i=0;i<loop.subSystemList.Length;i++)
        {
            if(loop.subSystemList[i].type!=typeof(UnityEngine.PlayerLoop.Update))continue;
            var steps=loop.subSystemList[i].subSystemList.ToList();
            steps.Insert(0,new UnityEngine.LowLevel.PlayerLoopSystem{type=typeof(HandsReloadPlayValidation),updateDelegate=SupplyBatchFocus});
            loop.subSystemList[i].subSystemList=steps.ToArray();
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
        deadline=EditorApplication.timeSinceStartup+600;
        routine=Checks();
    }
    private static readonly MethodInfo AllowCursor = typeof(EditorApplication).Assembly.GetTypes().SelectMany(t=>t.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)).FirstOrDefault(m=>m.Name=="SetAllowCursorLock"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(bool));
    private static void SupplyBatchFocus()
    {
        AllowCursor?.Invoke(null,new object[]{true});
        var pistol=UnityEngine.Object.FindFirstObjectByType<PistolController>();
        if(pistol!=null&&pistol.enabled&&pistol.GetComponent<FirstPersonPlayer>().enabled)
            Cursor.lockState=CursorLockMode.Locked;
    }
    private static void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message);
    }
    private static void Tick()
    {
        if(routine==null)return;
        if(!Application.isBatchMode&&!File.Exists(Path.Combine(HandsReloadAudit.Output,"allow-ui-tests.txt")))return;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Play validation timed out");
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            if(pending!=null&&!pending())return;pending=null;
            if(!routine.MoveNext()) { Finish(null);return; }
            pending=routine.Current as Func<bool>;
        }
        catch(Exception e) { Finish(e.ToString()); }
    }
    private static void Finish(string failure)
    {
        routine=null;SessionState.SetBool(Running,false);Time.timeScale=1;
        Application.logMessageReceived-=Log;
        var lines=new List<string>{failure==null?"PLAY_CHECKS_PASS":"PLAY_CHECKS_FAIL", "Assertions: "+assertions};
        lines.AddRange(results);if(failure!=null)lines.Add(failure);
        lines.Add("Runtime errors observed: "+errors.Count);lines.AddRange(errors.Distinct());
        File.WriteAllLines(Path.Combine(HandsReloadAudit.Output,"play-validation.txt"),lines);
        Debug.Log(lines[0]);EditorApplication.Exit(failure==null?0:1);
    }
    private static Func<bool> Delay(float seconds) { float until=Time.time+seconds;return ()=>Time.time>=until; }
    private static void Check(bool condition,string message) { assertions++;if(!condition)throw new InvalidOperationException(message); }
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).SetValue(target,value);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);
    private static void Key(Key key,bool down)
    {
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(Keyboard.current,down?new KeyboardState(key):new KeyboardState());
    }
    private static void SetupAmmo(PistolController pistol,int rounds)
    {
        pistol.CancelAction();var magazine=new WeaponMagazine(8,40);
        for(int i=rounds;i<8;i++)magazine.TryFire();Set(pistol,"magazine",magazine);Set(pistol,"nextShot",0f);Call(pistol,"UpdateHud");
    }
    private static void Capture(Camera camera,string name)
    {
        var texture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
        var previous=RenderTexture.active;RenderTexture.active=texture;
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
        File.WriteAllBytes(Path.Combine(HandsReloadAudit.Output,name+".png"),pixels.EncodeToPNG());
        RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(texture);
    }
    private static IEnumerator Checks()
    {
        Time.timeScale=2f;
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        foreach(int sceneIndex in new[]{3,0,1})
        {
            if(SceneManager.GetActiveScene().buildIndex!=sceneIndex)SceneSwitch.LoadSceneIndex(sceneIndex);
            yield return Delay(2f);
            // Explicit test fixture only: production locations never create players.
            if (UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayer>() == null)
            {
                UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Player"));
                var services = UnityEngine.Object.FindFirstObjectByType<MergedGameBootstrap>();
                if (services != null) Call(services, "Start");
                yield return Delay(1f);
            }
            // Standalone location players no longer bring test fixtures with them.
            var demo = Resources.Load<GameObject>("MergedGameplay");
            if (UnityEngine.Object.FindFirstObjectByType<NpcDialogue>() == null)
                UnityEngine.Object.Instantiate(demo.GetComponentInChildren<NpcDialogue>().gameObject);
            if (UnityEngine.Object.FindFirstObjectByType<ReadableDocument>() == null)
                UnityEngine.Object.Instantiate(demo.GetComponentInChildren<ReadableDocument>().gameObject);
            if (UnityEngine.Object.FindFirstObjectByType<InspectableItem>() == null)
                UnityEngine.Object.Instantiate(demo.GetComponentInChildren<InspectableItem>().gameObject);
            Cursor.lockState=CursorLockMode.Locked;
            var pistol=UnityEngine.Object.FindFirstObjectByType<PistolController>();
            Check(pistol!=null,"Missing player in scene "+sceneIndex);
            var movement=pistol.GetComponent<FirstPersonPlayer>();
            var camera=(Camera)Field(pistol,"playerCamera");
            var view=pistol.WeaponView;
            var definition=(WeaponDefinition)Field(pistol,"definition");
            yield return Delay(1.1f);
            Check(pistol.CanUseWeapon&&!pistol.IsEquipping,"Weapon unavailable after scene spawn: "+sceneIndex+" ready="+Field(pistol,"ready")+" enabled="+pistol.isActiveAndEnabled+" movement="+movement.enabled+" camera="+camera.enabled+" cursor="+Cursor.lockState+" action="+Field(pistol,"actionState")+" equipOnShow="+Field(pistol,"equipOnShow")+" view="+view.gameObject.activeInHierarchy);
            Check(UnityEngine.Object.FindObjectsByType<FirstPersonWeaponView>(FindObjectsSortMode.None).Length==1,"Duplicate weapon view");
            Check(view.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(r=>r.gameObject.activeInHierarchy)==3,"Expected Hands, sleeves, pistol only");
            Check(!view.Model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Rig"||t.name=="Makarov_base"),"Old model present");
            SetupAmmo(pistol,8);
            var target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Reload test hit target";
            target.transform.position=camera.transform.position+camera.transform.forward*.4f;target.transform.localScale=Vector3.one*.15f;
            var hit=target.AddComponent<ShootingTarget>();Physics.SyncTransforms();
            Check(pistol.TryFire(),"Shot rejected");Check(pistol.Rounds==7,"Shot did not consume round");
            Check((float)Field(hit,"flashEnd")>Time.time,"Raycast did not hit target");
            Check(((Text)Field(pistol,"ammoText")).text=="7 / 40","HUD did not update on shot");
            UnityEngine.Object.Destroy(target);
            yield return Delay(.8f);
            Vector3 hip=((Transform)Field(pistol,"viewModel")).localPosition;
            InputSystem.QueueStateEvent(Mouse.current,new MouseState{buttons=2});yield return Delay(.5f);
            Check(Vector3.Distance(((Transform)Field(pistol,"viewModel")).localPosition,view.AimOffset)<.002f,"Aim holder did not reach sight alignment");
            InputSystem.QueueStateEvent(Mouse.current,new MouseState());yield return Delay(.4f);
            Check(Vector3.Distance(((Transform)Field(pistol,"viewModel")).localPosition,hip)<.002f,"Aim did not return");
            Capture(camera,SceneManager.GetActiveScene().name+"-idle");
            foreach(bool empty in new[]{false,true})
            {
                SetupAmmo(pistol,empty?0:7);
                var clip=empty?definition.emptyReloadClip:definition.reloadClip;
                float insertion=clip.events.Single(e=>e.functionName=="OnMagazineInserted").time;
                Check(pistol.TryReload(),"Reload did not start");Check(!pistol.TryReload(),"Repeated reload accepted");
                Check(view.IsInState(empty?"ReloadEmpty":"Reload"),"Wrong reload state");
                Check(!pistol.TryFire(),"Shot allowed during reload");
                yield return Delay(insertion-.15f);
                Check(pistol.Rounds==(empty?0:7)&&pistol.ReserveAmmo==40,"Ammo changed before insertion");
                Capture(camera,SceneManager.GetActiveScene().name+"-"+clip.name+"-before-insertion");
                yield return Delay(.3f);
                Check(pistol.Rounds==8&&pistol.ReserveAmmo==(empty?32:39),"Insertion event did not transfer deficit");
                int token=((WeaponMagazine)Field(pistol,"magazine")).ReloadOperationId;
                view.ReceiveMagazineInsertion(token);
                Check(pistol.ReserveAmmo==(empty?32:39),"Duplicate event transferred ammo");
                yield return Delay(clip.length-insertion+.25f);
                Check(!pistol.IsReloading&&view.IsInState("Idle"),"Reload did not return to Idle");
                results.Add(SceneManager.GetActiveScene().name+": "+clip.name+" normal reload, Animator exit, insertion, duplicate-event rejection PASS");
            }
            foreach(bool empty in new[]{false,true})
            foreach(bool after in new[]{false,true})
            foreach(string reason in new[]{"Q","dialogue","reading","inspection","smoking","consciousness","disable"})
            {
                if(reason=="smoking"&&pistol.GetComponent<SmokingController>()==null)
                { results.Add(SceneManager.GetActiveScene().name+": smoking scenario unavailable (scene has no SmokingController); no gameplay component added by test");continue; }
                SetupAmmo(pistol,empty?0:7);Cursor.lockState=CursorLockMode.Locked;
                Check(pistol.TryReload(),"Cancel setup reload rejected: "+reason);
                var magazine=(WeaponMagazine)Field(pistol,"magazine");int oldToken=magazine.ReloadOperationId;
                var clip=empty?definition.emptyReloadClip:definition.reloadClip;
                float insertion=clip.events.Single(e=>e.functionName=="OnMagazineInserted").time;
                yield return Delay(after?insertion+.12f:.5f);
                int rounds=after?8:empty?0:7,reserve=after?(empty?32:39):40;
                Action restore=null;
                if(reason=="Q") { Key(UnityEngine.InputSystem.Key.Q,true);restore=()=>Key(UnityEngine.InputSystem.Key.Q,true); }
                else if(reason=="disable") { pistol.enabled=false;restore=()=>pistol.enabled=true; }
                else if(reason=="dialogue")
                {
                    var dialogue=pistol.GetComponent<DialogueController>();var npc=UnityEngine.Object.FindFirstObjectByType<NpcDialogue>();
                    Call(dialogue,"BeginDialogue",npc);Check(dialogue.IsTalking,"Dialogue not entered");restore=()=>Call(dialogue,"EndDialogue");
                }
                else if(reason=="reading")
                {
                    var reader=pistol.GetComponent<DocumentReader>();var document=UnityEngine.Object.FindFirstObjectByType<ReadableDocument>();
                    Call(reader,"OpenDocument",document);Check(reader.IsReading,"Reading not entered");restore=()=>Call(reader,"CloseDocument");
                }
                else if(reason=="inspection")
                {
                    var inspector=pistol.GetComponent<ItemInspector>();var item=UnityEngine.Object.FindFirstObjectByType<InspectableItem>();
                    Call(inspector,"OpenInspection",item);Check(inspector.IsInspecting,"Inspection not entered");restore=()=>Call(inspector,"CloseInspection");
                }
                else if(reason=="smoking")
                {
                    var smoker=pistol.GetComponent<SmokingController>();Check((bool)Field(smoker,"modelReady"),"Smoking model not loaded");
                    pistol.GetComponent<PlayerStress>().stress=100;Key(UnityEngine.InputSystem.Key.Y,true);
                    restore=()=> { Check(smoker.IsSmoking,"Smoking not entered");Call(smoker,"FinishSmoking"); };
                }
                else
                {
                    var obj=new GameObject("Consciousness cancellation probe");var sw=obj.AddComponent<SceneSwitch>();var point=obj.AddComponent<FocusPoint>();
                    point.transform.position=camera.transform.position+camera.transform.forward;
                    var states=pistol.GetComponents<MonoBehaviour>().ToDictionary(b=>b,b=>b.enabled);
                    Vector3 pos=camera.transform.position;Quaternion rot=camera.transform.rotation;
                    var entry=(IEnumerator)Call(sw,"MoveToNPC",camera,point);Check(entry.MoveNext(),"Consciousness entry did not begin");
                    restore=()=> { foreach(var state in states)state.Key.enabled=state.Value;camera.transform.SetPositionAndRotation(pos,rot);UnityEngine.Object.Destroy(obj); };
                }
                yield return Delay(.08f);Key(UnityEngine.InputSystem.Key.Q,false);yield return Delay(.05f);
                Check(!pistol.IsReloading,"Reload remains locked on "+reason);
                Check(pistol.Rounds==rounds&&pistol.ReserveAmmo==reserve,"Cancel changed ammunition: "+reason);
                Check(!pistol.TryFire()&&!pistol.TryReload(),"Blocked interaction allowed weapon: "+reason);
                view.ReceiveMagazineInsertion(oldToken);
                Check(pistol.Rounds==rounds&&pistol.ReserveAmmo==reserve,"Late event transferred ammo: "+reason);
                restore();yield return Delay(.08f);Key(UnityEngine.InputSystem.Key.Q,false);
                Cursor.lockState=CursorLockMode.Locked;yield return Delay(1.1f);
                Check(pistol.CanUseWeapon&&!pistol.IsEquipping&&!pistol.IsReloading,"Weapon did not unlock: "+reason);
                if(pistol.Rounds==0)
                {
                    Check(pistol.TryReload(),"Empty weapon cannot reload after cancellation");
                }
                else
                {
                    Set(pistol,"nextShot",0f);Check(pistol.TryFire(),"Cannot fire after cancellation: "+reason);
                    Check(pistol.TryReload(),"Cannot reload after cancellation: "+reason);
                }
                int current=magazine.ReloadOperationId;Check(current!=oldToken,"Reload token reused");
                int beforeRounds=pistol.Rounds,beforeReserve=pistol.ReserveAmmo;
                view.ReceiveMagazineInsertion(oldToken);Check(pistol.Rounds==beforeRounds&&pistol.ReserveAmmo==beforeReserve,"Old event committed a new reload");
                pistol.CancelAction();
                results.Add(SceneManager.GetActiveScene().name+": "+(empty?"Full":"Fast")+" cancel "+(after?"after":"before")+" insertion via "+reason+" PASS");
                File.WriteAllLines(Path.Combine(HandsReloadAudit.Output,"play-progress.txt"),results);
            }
            results.Add(SceneManager.GetActiveScene().name+": single kit, native Take/Idle/Shoot, aim, ray hit, ammo HUD PASS");
        }
        Check(!errors.Any(e=>e.Contains("PistolController")||e.Contains("FirstPersonWeaponView")||e.Contains("OnMagazineInserted")||e.Contains("Reload finished")),"Weapon runtime error logged");
    }
}



