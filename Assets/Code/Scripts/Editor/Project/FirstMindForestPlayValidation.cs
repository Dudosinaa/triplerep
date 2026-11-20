using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FirstMindForestPlayValidation
{
    private const string SceneKey = "FirstMindForest.ValidationScene";
    private static IEnumerator checks;
    private static Keyboard keyboard;
    private static Mouse mouse;
    private static double deadline;
    private static int frame = -1;

    static FirstMindForestPlayValidation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += Tick;
    }

    public static void Start(string path)
    {
        SessionState.SetString(SceneKey, path);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        string scene = SessionState.GetString(SceneKey, "");
        if (state != PlayModeStateChange.EnteredPlayMode || scene.Length == 0) return;
        if (SceneManager.GetActiveScene().path != scene)
        {
            Finish(new InvalidOperationException("Play Mode opened the wrong scene"));
            return;
        }

        deadline = EditorApplication.timeSinceStartup + 120d;
        checks = Validate();
    }

    private static void Tick()
    {
        if (checks == null || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("The forest scene's Play Mode checks timed out");
            if (!checks.MoveNext()) Finish(null);
        }
        catch (Exception error)
        {
            Finish(error);
        }
    }

    private static IEnumerator Validate()
    {
        keyboard = InputSystem.AddDevice<Keyboard>("FirstMindValidationKeyboard");
        mouse = InputSystem.AddDevice<Mouse>("FirstMindValidationMouse");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Time.captureDeltaTime = 1f / 60f;

        var player = Object.FindFirstObjectByType<FirstPersonPlayer>();
        var boundary = Object.FindFirstObjectByType<ForestHouseBoundary>();
        var controller = player.GetComponent<CharacterController>();
        var camera = player.GetComponentInChildren<Camera>();
        var ground = Object.FindFirstObjectByType<Terrain>();
        var warning = Object.FindFirstObjectByType<CanvasGroup>();
        Vector3 spawn = player.transform.position;
        Quaternion facing = player.transform.rotation;
        for (int i = 0; i < 20; i++) yield return null;
        Check(controller.isGrounded && warning.alpha < 0.001f, "Spawn grounds correctly and has no warning");
        Preview(camera, "entrance");

        KeyState(Key.W);
        Vector3 before = player.transform.position;
        for (int i = 0; i < 22; i++) yield return null;
        Vector3 step = player.transform.position - before;
        float walk = Vector3.Dot(facing * Vector3.forward, step);
        Check(walk > 0.65f && walk < 1.5f, $"W moves toward the door ({walk:F2} m)");

        KeyState(Key.S, Key.LeftShift);
        before = player.transform.position;
        for (int i = 0; i < 22; i++) yield return null;
        float sprint = Vector3.Dot(facing * Vector3.back, player.transform.position - before);
        Check(sprint > walk * 1.25f, $"S moves backward and Shift accelerates ({sprint:F2} m)");
        KeyState(Key.A);
        before = player.transform.position;
        for (int i = 0; i < 12; i++) yield return null;
        Check(Vector3.Dot(facing * Vector3.left, player.transform.position - before) > 0.38f, "A strafes left");
        KeyState(Key.D);
        before = player.transform.position;
        for (int i = 0; i < 12; i++) yield return null;
        Check(Vector3.Dot(facing * Vector3.right, player.transform.position - before) > 0.38f, "D strafes right");
        KeyState();

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            mouse.MakeCurrent();
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(190f, -85f));
            for (int i = 0; i < 15; i++) yield return null;
            Check(Quaternion.Angle(facing, player.transform.rotation) > 8f &&
                Quaternion.Angle(Quaternion.identity, camera.transform.localRotation) > 3f,
                "Mouse changes yaw and pitch");
            KeyState(Key.Escape);
            yield return null;
            yield return null;
            Check(Cursor.lockState == CursorLockMode.None, "Escape releases the cursor");
            KeyState();
        }
        else Debug.Log("FIRST_MIND_MANUAL_CHECK: native cursor lock and mouse look require a focused Game View");

        player.enabled = false;
        player.Teleport(AtDistance(boundary, ground, Vector2.up, 20f));
        yield return null;
        yield return null;
        Check(warning.alpha >= 0f && warning.alpha < 1f, "Warning fades in");
        double until = EditorApplication.timeSinceStartup + 0.6d;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(warning.alpha > 0.95f, "Warning appears beyond 19 m");
        player.Teleport(AtDistance(boundary, ground, Vector2.up, 18f));
        yield return null;
        yield return null;
        Check(warning.alpha > 0.95f, "18 m stays within the warning hysteresis band");
        player.Teleport(AtDistance(boundary, ground, Vector2.up, 16.8f));
        until = EditorApplication.timeSinceStartup + 0.6d;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(warning.alpha < 0.05f, "Warning fades out inside 17 m");

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            player.Teleport(AtDistance(boundary, ground, direction, 26.45f));
            Vector3 outward = new Vector3(direction.x, 0f, direction.y);
            CollisionFlags flags = 0;
            for (int stepIndex = 0; stepIndex < 30; stepIndex++)
                flags |= controller.Move(outward * 0.09f - Vector3.up * 0.08f);
            float stopped = boundary.DistanceFromHouse(player.transform.position);
            Check((flags & CollisionFlags.Sides) != 0 && stopped > 26.5f && stopped < 27.01f,
                $"Boundary stops the capsule at heading {angle * Mathf.Rad2Deg:F0}° ({stopped:F2} m)");

            Vector3 tangent = Vector3.Cross(outward, Vector3.up);
            before = player.transform.position;
            for (int stepIndex = 0; stepIndex < 13; stepIndex++)
                controller.Move(tangent * 0.07f + outward * 0.018f - Vector3.up * 0.08f);
            Check(Vector3.Dot(player.transform.position - before, tangent) > 0.65f &&
                boundary.DistanceFromHouse(player.transform.position) < 27.01f, "Tangent movement remains available");
            for (int stepIndex = 0; stepIndex < 11; stepIndex++)
                controller.Move(-outward * 0.08f - Vector3.up * 0.08f);
            Check(boundary.DistanceFromHouse(player.transform.position) < stopped - 0.58f, "The player can walk back");
        }

        player.Teleport(AtDistance(boundary, ground, new Vector2(0.65f, 0.76f), 15f));
        player.transform.rotation = Quaternion.Euler(0f, 14f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        yield return null;
        Preview(camera, "forest");
        player.Teleport(spawn);
        player.transform.rotation = facing;
    }

    private static Vector3 AtDistance(ForestHouseBoundary boundary, Terrain ground, Vector2 direction, float distance)
    {
        Vector3 ray = new Vector3(direction.x, 0, direction.y).normalized;
        float lo = 0f;
        float hi = distance + 10f;
        for (int i = 0; i < 24; i++)
        {
            float middle = (lo + hi) * 0.5f;
            if (boundary.DistanceFromHouse(ray * middle) < distance) lo = middle;
            else hi = middle;
        }
        Vector3 position = ray * hi;
        position.y = ground.SampleHeight(position) + ground.transform.position.y + 0.025f;
        return position;
    }

    private static void KeyState(params Key[] keys)
    {
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    }

    private static void Preview(Camera camera, string name)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        const int width = 1440;
        const int height = 900;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (!RenderPipeline.SupportsRenderRequest(camera, request)) return;
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            Directory.CreateDirectory("Logs/FirstMind");
            File.WriteAllBytes($"Logs/FirstMind/{name}.png", texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(texture);
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Debug.Log("FIRST_MIND_PLAY_PASS: " + description);
    }

    private static void Finish(Exception error)
    {
        checks = null;
        SessionState.EraseString(SceneKey);
        Time.captureDeltaTime = 0f;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        if (error != null) Debug.LogException(error);
        else Debug.Log("FIRST_MIND_PLAY_VALIDATION_PASS");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
