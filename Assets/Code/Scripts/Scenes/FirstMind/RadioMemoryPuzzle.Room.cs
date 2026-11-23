using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public sealed partial class RadioMemoryPuzzle
{
    private const float CycleSeconds = 3.2f;
    private const float SafeStart = .56f;
    private const float SafeEnd = .84f;
    private const int SampleRate = 22050;

    [SerializeField] private Transform room;
    [SerializeField] private Transform wallClock;
    [SerializeField] private Vector3[] fuseSpots;

    private BoxCollider fusePickup;
    private Material fuseGlass;
    private Material fuseMetal;
    private int fuseSpot = -1;
    private float timingStartedAt;
    private float faultUntil;
    private string notice = "";
    private float noticeUntil;
    private AudioSource effects;
    private AudioClip contactClick, discharge, tapeNoise, clockLoop, draftLoop;

    private float TimingPhase => Mathf.Repeat(Time.time - timingStartedAt, CycleSeconds) / CycleSeconds;
    private static bool IsSafePhase(float phase) => phase >= SafeStart && phase <= SafeEnd;

    private void CreateFusePickup()
    {
        if (room == null || fuseSpots == null || fuseSpots.Length == 0)
        {
            Debug.LogError("RadioMemoryPuzzle needs fuse hiding spots in FirstMind.", this);
            return;
        }
        var pickup = new GameObject("Loose fuse · 0.5 A");
        pickup.transform.SetParent(transform, true);
        fusePickup = pickup.AddComponent<BoxCollider>();
        fusePickup.isTrigger = true;
        fusePickup.size = new Vector3(.23f, .14f, .18f);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        fuseGlass = new Material(shader) { color = new Color(.35f, .72f, .61f) };
        fuseGlass.EnableKeyword("_EMISSION");
        fuseGlass.SetColor("_EmissionColor", new Color(.035f, .09f, .07f));
        fuseMetal = new Material(shader) { color = new Color(.79f, .67f, .45f) };
        AddFusePart(Vector3.zero, new Vector3(.036f, .06f, .036f), fuseGlass);
        AddFusePart(Vector3.left * .054f, new Vector3(.043f, .013f, .043f), fuseMetal);
        AddFusePart(Vector3.right * .054f, new Vector3(.043f, .013f, .043f), fuseMetal);
        PlaceFuse();
    }

    private void AddFusePart(Vector3 offset, Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = "Fuse casing";
        part.transform.SetParent(fusePickup.transform, false);
        part.transform.localPosition = offset;
        part.transform.localRotation = Quaternion.Euler(0, 0, 90);
        part.transform.localScale = size;
        var collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void PlaceFuse()
    {
        if (fusePickup == null) return;
        int count = fuseSpots.Length;
        int next = Random.Range(0, fuseSpot >= 0 && count > 1 ? count - 1 : count);
        if (fuseSpot >= 0 && count > 1 && next >= fuseSpot) next++;
        fuseSpot = next;
        fusePickup.transform.SetPositionAndRotation(room.TransformPoint(fuseSpots[fuseSpot]), room.rotation);
        fusePickup.gameObject.SetActive(true);
    }

    private void ResetAttempt(string reason)
    {
        hasFuse = fuseInstalled = speakerConnected = externalInput = false;
        dragging = DragPart.None;
        PlayEffect(discharge);
        PlaceFuse();
        faultUntil = Time.time + .7f;
        feedback = reason + " Предохранитель перегорел. Нажми Esc и найди запасной.";
    }

    private void CreateRoomAudio()
    {
        contactClick = MakeClip("Radio contact click", .16f, 14, (t, noise) =>
            Mathf.Exp(-t * 42) * (.24f * noise + .18f * Mathf.Sin(t * 5400)));
        discharge = MakeClip("Radio fault", .48f, 37, (t, noise) =>
            Mathf.Exp(-t * 9) * (.37f * noise + .16f * Mathf.Sin(t * 485 + t * t * 680)));
        tapeNoise = MakeClip("Cassette engagement and hiss", 3f, 61, (t, noise) =>
            .22f * Mathf.Exp(-t * 30) * Mathf.Sin(t * 1860) + noise * .065f * (1f - t / 3f));
        clockLoop = MakeClip("Wall clock ticking", 2f, 42, (t, noise) =>
        {
            float age = Mathf.Repeat(t + .7f, 1f);
            float tone = t < 1f ? 8100 : 10200;
            return Mathf.Exp(-age * 78) * (.31f * Mathf.Sin(age * tone) + .28f * noise);
        });
        draftLoop = MakeClip("Draft through the window", 12f, 82, (t, noise) =>
        {
            float gust = .45f + .25f * Mathf.Sin(t * 1.1f) + .15f * Mathf.Sin(t * 2.2f + 1);
            return gust * (.27f * noise + .09f * Mathf.Sin(t * 380)) *
                Mathf.Clamp01(Mathf.Min(t, 12f - t) * 2f);
        }, .055f);

        effects = CreateSoundSource("Radio effects", transform.position, .85f, 0f);
        if (wallClock != null)
        {
            var face = wallClock.GetComponentInChildren<Renderer>();
            StartRoomLoop("Clock ticking", face != null ? face.bounds.center : wallClock.position, .52f, clockLoop);
        }
        if (room != null)
            StartRoomLoop("Window draft", room.TransformPoint(new Vector3(2.4f, 1.45f, -.55f)),
                .60f, draftLoop);
    }

    private static AudioClip MakeClip(string title, float seconds, int seed,
        Func<float, float, float> signal, float lowPass = 1f)
    {
        var rng = new System.Random(seed);
        var samples = new float[Mathf.RoundToInt(seconds * SampleRate)];
        float noise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            noise += ((float)rng.NextDouble() * 2f - 1f - noise) * lowPass;
            float time = i / (float)SampleRate;
            float edge = Mathf.Clamp01(Mathf.Min(time, seconds - time) * 200f);
            samples[i] = Mathf.Clamp(signal(time, noise) * edge, -1f, 1f);
        }
        var clip = AudioClip.Create(title, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void StartRoomLoop(string title, Vector3 position, float volume, AudioClip clip)
    {
        var source = CreateSoundSource(title, position, volume, 1f);
        source.loop = true;
        source.clip = clip;
        source.Play();
    }

    private AudioSource CreateSoundSource(string title, Vector3 position, float volume, float spatialBlend)
    {
        var source = new GameObject(title).AddComponent<AudioSource>();
        source.transform.SetParent(transform, true);
        source.transform.position = position;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = .65f;
        source.maxDistance = 6f;
        source.dopplerLevel = 0f;
        return source;
    }

    private void PlayEffect(AudioClip clip)
    {
        if (effects != null && clip != null) effects.PlayOneShot(clip);
    }

    private void DisposeRoomProps()
    {
        foreach (var asset in new UnityEngine.Object[] { fuseGlass, fuseMetal, contactClick,
            discharge, tapeNoise, clockLoop, draftLoop })
            if (asset != null) Destroy(asset);
    }
}
