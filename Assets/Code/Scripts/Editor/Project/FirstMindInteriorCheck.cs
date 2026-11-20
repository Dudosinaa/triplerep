using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class FirstMindInteriorCheck
{
    private const string KeyName = "FirstMindInterior.Run";
    private const string Output = "Logs/FirstMind/Interior";
    private static IEnumerator work;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static int frame = -1;
    private static double deadline;

    static FirstMindInteriorCheck()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
    }

    public static void RunStored()
    {
        EditorSceneManager.OpenScene(FirstMindInteriorBuilder.ScenePath, OpenSceneMode.Single);
        Begin();
    }

    internal static void Begin()
    {
        SessionState.SetBool(KeyName, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(KeyName, false)) return;
        deadline = EditorApplication.timeSinceStartup + 420;
        work = Validate();
    }

    private static void Tick()
    {
        if (work == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Interior Play Mode timed out");
            if (!work.MoveNext()) End(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            End(1);
        }
    }

    internal static void End(int code)
    {
        work = null;
        SessionState.EraseBool(KeyName);
        Time.captureDeltaTime = 0f;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        keyboard = null;
        mouse = null;
        Debug.Log(code == 0 ? "FIRST_MIND_INTERIOR_PLAY_PASS" : "FIRST_MIND_INTERIOR_PLAY_FAIL");
        EditorApplication.Exit(code);
    }

    private static IEnumerator Validate()
    {
        keyboard = InputSystem.AddDevice<Keyboard>("InteriorValidationKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("InteriorValidationMouse");
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Time.captureDeltaTime = 1f / 60f;
        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var character = player.GetComponent<CharacterController>();
        var view = player.GetComponentInChildren<Camera>();
        var ground = Object.FindFirstObjectByType<Terrain>();
        var group = GameObject.Find("House").transform;
        var model = group.GetChild(0);
        var hint = GameObject.Find("Door Prompt").GetComponent<CanvasGroup>();
        var label = hint.GetComponentInChildren<TMPro.TMP_Text>();
        var surfaces = Object.FindObjectsByType<HouseFootstepSurface>(FindObjectsSortMode.None);
        Vector3 spawn = player.transform.position;
        Quaternion spawnRotation = player.transform.rotation;
        float floorHeight = surfaces[0].GetComponent<Collider>().bounds.max.y;
        for (int i = 0; i < 24; i++) yield return null;
        Check(character.isGrounded && hint.alpha == 0f,
            $"Spawn preserved outside, with no out-of-range door prompt (grounded={character.isGrounded}, alpha={hint.alpha})");
        Preview(view, "01-exterior");
        Aim(view, model, floorHeight, -2.64f, 0.70f, -1.17f, -0.8f, "02-living");
        Aim(view, model, floorHeight, -1.90f, -0.25f, -0.88f, 1.47f, "03-living-reverse");
        Aim(view, model, floorHeight, 0.90f, -2.50f, -1.45f, -2.95f, "04-kitchen");
        Aim(view, model, floorHeight, -3.02f, -2.43f, -4.44f, -3.44f, "05-bathroom");

        foreach (HouseDoor door in group.Find("Doors").GetComponentsInChildren<HouseDoor>())
        {
            var focus = door.GetComponent<BoxCollider>();
            Vector3 entrance = focus.bounds.center;
            float hallOffset = 1.50f;
            var sound = door.GetComponent<AudioSource>();
            Vector3 start = new Vector3(entrance.x, floorHeight, entrance.z + hallOffset);
            if (door.name == "EntranceDoor") start.y = ground.SampleHeight(start) + ground.transform.position.y;
            player.Teleport(start + Vector3.up * 0.026f);
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            view.transform.localRotation = Quaternion.identity;
            for (int i = 0; i < 7; i++) yield return null;
            Check(hint.alpha > 0.98f && label.text == HouseDoorInteraction.OpenText, door.name + " shows the open prompt within reach");
            Check(door.Shut, door.name + " starts shut");

            for (int i = 0; i < 52; i++) character.Move(new Vector3(0f, -0.06f, -0.075f));
            Check(player.transform.position.z > entrance.z + 0.17f, door.name + " blocks passage while shut");
            player.Teleport(start + Vector3.up * 0.026f);
            for (int i = 0; i < 4; i++) yield return null;

            Send(Key.E);
            for (int i = 0; i < 3; i++) yield return null;
            bool doorAudio = sound.isPlaying;
            Check(door.WantsOpen && label.text == HouseDoorInteraction.CloseText && sound.clip != null,
                door.name + " opens with E and updates the prompt");
            Check(doorAudio && sound.clip.name == "Door - opening", door.name + " starts 3D opening audio");
            Send();
            for (int i = 0; !door.Open && i < 145; i++) yield return null;
            Check(door.Open && Quaternion.Angle(Quaternion.identity, door.transform.GetChild(0).localRotation) > 85f,
                door.name + " opens fully at its hinge");

            for (int i = 0; i < 25; i++) character.Move(new Vector3(0f, -0.055f, -0.10f));
            float crossed = entrance.z - player.transform.position.z;
            Check(crossed > 0.37f && character.isGrounded,
                $"{door.name} allows the grounded player to cross (distance={crossed:F2}, grounded={character.isGrounded}, pos={player.transform.position})");
            for (int i = 0; i < 25; i++) character.Move(new Vector3(0f, -0.055f, 0.10f));
            Check(player.transform.position.z > entrance.z + 0.58f, door.name + " also lets the player return");

            player.Teleport(start + Vector3.up * 0.026f);
            for (int i = 0; i < 4; i++) yield return null;
            Send(Key.E);
            for (int i = 0; i < 3; i++) yield return null;
            Check(!door.WantsOpen && sound.isPlaying && sound.clip.name == "Door - closing", door.name + " closes with audible motion on the next E");
            Send();
            for (int i = 0; !door.Shut && i < 145; i++) yield return null;
            Check(door.Shut, door.name + " returns to the closed position");
        }

        var lastBank = typeof(FirstPersonPlayer).GetField("previousStepClips", BindingFlags.NonPublic | BindingFlags.Instance);
        player.Teleport(spawn + Vector3.up * 0.02f);
        player.transform.rotation = Quaternion.identity;
        Send(Key.W);
        for (int i = 0; i < 46; i++) yield return null;
        Check(lastBank.GetValue(player) is AudioClip[] dirt && dirt[0].name.Contains("DirtyGround"), "Actual grounded outdoor movement emits the original dirt steps");
        Send();
        yield return null;

        Vector3 floorStart = FirstMindInteriorBuilder.PlanPoint(model, -2.55f, 0.28f, floorHeight + 0.02f);
        player.Teleport(floorStart);
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        Send(Key.W);
        for (int i = 0; i < 49; i++) yield return null;
        Check(lastBank.GetValue(player) is AudioClip[] boards && boards[0].name.StartsWith("Footsteps_Wood"),
            "Actual grounded indoor movement changes to recorded wood steps");
        Send();
        yield return null;

        player.Teleport(spawn);
        player.transform.rotation = spawnRotation;
    }

    private static void Send(params Key[] keys)
    {
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    private static void Aim(Camera camera, Transform model, float floor, float x, float z, float lookX, float lookZ, string label)
    {
        Vector3 position = FirstMindInteriorBuilder.PlanPoint(model, x, z, floor + 1.65f);
        Vector3 target = FirstMindInteriorBuilder.PlanPoint(model, lookX, lookZ, floor + 1.16f);
        Vector3 old = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float fov = camera.fieldOfView;
        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        camera.fieldOfView = 70f;
        Preview(camera, label);
        camera.transform.SetPositionAndRotation(old, rotation);
        camera.fieldOfView = fov;
    }

    private static void Preview(Camera camera, string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        const int width = 1380;
        const int height = 920;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return;
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            Directory.CreateDirectory(Output);
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(texture);
        }
    }

    private static void Check(bool good, string label)
    {
        if (!good) throw new InvalidOperationException(label);
        Debug.Log("INTERIOR_CHECK: " + label);
    }
}
