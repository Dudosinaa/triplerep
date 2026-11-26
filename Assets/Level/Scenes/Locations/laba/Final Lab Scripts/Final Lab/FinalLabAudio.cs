using System;
using System.Collections;
using FinalLab.Investigation;
using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class FinalLabAudio : MonoBehaviour
{
    public enum Cue
    {
        PickUpPaper, PutDownPaper, OpenWoodDoor, CloseWoodDoor, OpenFatherDoor,
        ReserveSwitch, ReserveRestored, IsolationLever, FatherRoomIsolated,
        BedCreak, FatherStands, FatherHitsFloor, EntityEnters,
        SwitchClick, ModeB, CoverOpens, LowerCurrent, IsolationComplete,
        Instability, Overload, EntityDestroyed, Shutdown
    }

    [Serializable]
    private sealed class LoopClip
    {
        public AudioClip clip;
        public float start, end, overlap;
    }

    [Serializable]
    internal sealed class RecordedLine
    {
        [Serializable]
        internal struct Caption
        {
            public float start;
            [TextArea] public string text;
        }

        public string speaker;
        public AudioClip voiceClip;
        public Caption[] captions;
    }

    [SerializeField] private FinalLabFlowController flow;
    [SerializeField] private FinalLabIvanCutsceneController ivan;
    [SerializeField] private FinalLabInvestigationManager investigation;
    [SerializeField] private Transform lamp, resonator;
    [SerializeField] private AudioClip[] cues, ivanSteps;
    [SerializeField] private LoopClip ventilation, fluorescent, hum, alexeiMusic, ivanMusic;
    [SerializeField] private RecordedLine alexeiPossession, ivanIntroduction, ivanShutdown;

    private AudioSource[] effects;
    private AudioSource woodDoor, fatherDoor, walk, current, instability, overload;
    private Loop musicLoop, ventLoop, lampLoop, humLoop;
    private bool currentRequested;
    private float humLevel, overloadLevel, instabilityLevel;
    private float woodOpenness, fatherOpenness, stepDistance;
    private bool playedWoodOpen, playedWoodClose, playedFatherOpen, ivanWalking;
    private Vector3 lastIvanPosition;
    private int stepIndex;

    private void Awake()
    {
        effects = new AudioSource[5];
        for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource("Effect " + (i + 1), transform, 1.2f, 11f);
        woodDoor = MakeSource("Ivan's door", ivan.Door.Hinge, 1.7f, 11f);
        fatherDoor = MakeSource("Father's door", flow.FatherDoor.Hinge, 1.7f, 12f);
        walk = MakeSource("Ivan's cutscene footsteps", ivan.Ivan.transform, 1.3f, 9f);
        current = MakeSource("Current reduction", resonator, 2f, 11f);
        current.loop = true;
        instability = MakeSource("Emergency instability", resonator, 2.5f, 13f);
        overload = MakeSource("Resonator overload", resonator, 2.5f, 14f);
        musicLoop = MakeLoop("Music", transform);
        ventLoop = MakeLoop("Ventilation", transform);
        lampLoop = MakeLoop("Fluorescent lamp", lamp, .9f, 6f);
        humLoop = MakeLoop("K-17 high voltage", resonator, 1.8f, 9f);
    }

    private void OnEnable()
    {
        investigation.DocumentSound += OnDocumentSound;
        ventLoop.Begin(ventilation, .16f);
        lampLoop.Begin(fluorescent, .18f);
    }

    private void OnDisable()
    {
        if (investigation != null) investigation.DocumentSound -= OnDocumentSound;
        if (effects == null) return;
        foreach (var source in effects) StopSource(source);
        StopSource(woodDoor);
        StopSource(fatherDoor);
        StopSource(walk);
        StopSource(current);
        StopSource(instability);
        StopSource(overload);
        ventLoop.Stop();
        lampLoop.Stop();
        humLoop.Stop();
        musicLoop.Stop();
    }

    private void LateUpdate()
    {
        if (AudioListener.pause) return;
        float dt = Time.unscaledDeltaTime;
        bool speaking = flow.Father.Subtitles.IsVoicePlaying;
        musicLoop.Tick(dt, speaking ? .34f : 1f, 1.25f);
        ventLoop.Tick(dt, speaking ? .74f : 1f, .85f);
        lampLoop.Tick(dt, speaking ? .68f : 1f, .4f);
        humLoop.Tick(dt, humLevel * (speaking ? .62f : 1f), .5f);
        FadeEffect(current, currentRequested ? .40f : 0, dt, .12f);
        FadeEffect(instability, instabilityLevel, dt, .22f);
        FadeEffect(overload, overloadLevel, dt, .22f);

        var ivanStage = ivan.State;
        float woodenTravel = ivan.Door.Openness;
        if (ivanStage == FinalLabIvanCutsceneController.Stage.Opening && woodenTravel > woodOpenness && !playedWoodOpen)
        {
            playedWoodOpen = true;
            Play(woodDoor, Cue.OpenWoodDoor, .44f);
        }
        if (ivanStage == FinalLabIvanCutsceneController.Stage.Closing && woodenTravel < woodOpenness && !playedWoodClose)
        {
            playedWoodClose = true;
            Play(woodDoor, Cue.CloseWoodDoor, .46f);
        }
        woodOpenness = woodenTravel;
        float metalTravel = flow.FatherDoor.Openness;
        if (flow.State == FinalLabFlowController.Stage.FatherDoorOpening && metalTravel > fatherOpenness && !playedFatherOpen)
        {
            playedFatherOpen = true;
            Play(fatherDoor, Cue.OpenFatherDoor, .48f);
        }
        if (flow.State == FinalLabFlowController.Stage.FatherDoorReady && metalTravel <= 0f) playedFatherOpen = false;
        fatherOpenness = metalTravel;
        UpdateIvanFootsteps(ivanStage);
    }

    private void UpdateIvanFootsteps(FinalLabIvanCutsceneController.Stage stage)
    {
        if (stage != FinalLabIvanCutsceneController.Stage.Exiting &&
            stage != FinalLabIvanCutsceneController.Stage.Returning)
        {
            if (ivanWalking) walk.Stop();
            ivanWalking = false;
            return;
        }

        Vector3 position = ivan.Ivan.transform.position;
        if (!ivanWalking)
        {
            lastIvanPosition = position;
            stepDistance = .24f;
            ivanWalking = true;
        }
        Vector3 displacement = position - lastIvanPosition;
        displacement.y = 0;
        lastIvanPosition = position;
        stepDistance += displacement.magnitude;
        const float stride = .57f;
        if (stepDistance < stride) return;
        stepDistance -= stride;
        walk.clip = ivanSteps[stepIndex];
        stepIndex = (stepIndex + 1) % ivanSteps.Length;
        walk.volume = .5f;
        walk.Play();
    }

    private void OnDocumentSound(Vector3 position, bool pickedUp)
    {
        Spatial(pickedUp ? Cue.PickUpPaper : Cue.PutDownPaper, position, .37f);
    }

    internal void StartAlexeiMusic() => musicLoop.Begin(alexeiMusic, .18f);
    internal void StopMusic() => musicLoop.Release();

    internal void StartIvanGameplay()
    {
        musicLoop.Begin(ivanMusic, .18f);
        humLoop.Begin(hum, .15f);
        humLevel = 1f;
    }

    internal void LowerCurrent(bool holding, float progress)
    {
        if (holding && !currentRequested) Play(current, Cue.LowerCurrent, 0);
        currentRequested = holding;
        humLevel = Mathf.Lerp(.74f, .48f, progress);
    }

    internal void StartOverload()
    {
        Play(instability, Cue.Instability, 0);
        Play(overload, Cue.Overload, 0);
        instabilityLevel = .46f;
        overloadLevel = .46f;
    }

    internal void MatchOverload(float elapsed)
    {
        instabilityLevel = .46f * (1f - Mathf.InverseLerp(1.2f, 2.65f, elapsed));
        overloadLevel = .46f * (1f - Mathf.InverseLerp(3.55f, 4.2f, elapsed));
    }

    internal void Shutdown()
    {
        overloadLevel = instabilityLevel = humLevel = 0f;
        humLoop.Release();
        StopMusic();
        Spatial(Cue.EntityDestroyed, resonator.position, .52f);
        Spatial(Cue.Shutdown, resonator.position, .43f);
    }

    internal IEnumerator Possession(Action track) => flow.Father.Subtitles.PlayTimedLine(alexeiPossession, track);
    internal IEnumerator IntroduceIvan() => flow.Father.Subtitles.PlayTimedLine(ivanIntroduction);
    internal IEnumerator AfterShutdown() => flow.Father.Subtitles.PlayTimedLine(ivanShutdown);

    internal void Spatial(Cue cue, Vector3 position, float volume) => Effect(cue, position, volume, true);
    internal void Signal(Cue cue, float volume) => Effect(cue, transform.position, volume, false);

    private void Effect(Cue cue, Vector3 position, float volume, bool spatial)
    {
        foreach (var source in effects)
        {
            if (source.isPlaying) continue;
            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            Play(source, cue, volume);
            return;
        }
    }

    private void Play(AudioSource source, Cue cue, float volume)
    {
        source.Stop();
        source.clip = cues[(int)cue];
        source.volume = volume;
        source.Play();
    }

    private static void FadeEffect(AudioSource source, float target, float delta, float seconds)
    {
        if (!source.isPlaying) return;
        source.volume = Mathf.MoveTowards(source.volume, target, delta / seconds);
        if (source.volume <= 0 && target == 0) source.Stop();
    }

    private static void StopSource(AudioSource source)
    {
        if (source != null) source.Stop();
    }

    private static AudioSource MakeSource(string name, Transform parent, float near = 0, float far = 0)
    {
        var host = new GameObject(name);
        host.transform.SetParent(parent, false);
        var source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.dopplerLevel = 0f;
        source.priority = 142;
        if (far > 0)
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = near;
            source.maxDistance = far;
        }
        return source;
    }

    private static Loop MakeLoop(string name, Transform parent, float near = 0, float far = 0) =>
        new(MakeSource(name, parent, near, far), MakeSource(name + " loop transition", parent, near, far));

    private sealed class Loop
    {
        private readonly AudioSource[] sources;
        private LoopClip clip;
        private float volume, fade, duck = 1f;
        private double start, next;
        private int lane;
        private bool releasing, scheduled;

        public Loop(AudioSource first, AudioSource second) => sources = new[] { first, second };

        public void Begin(LoopClip recording, float level)
        {
            Stop();
            clip = recording;
            volume = level;
            releasing = false;
            lane = 0;
            start = AudioSettings.dspTime + .10;
            next = start + clip.end - clip.start - clip.overlap;
            Schedule(sources[lane], start);
        }

        public void Release() => releasing = true;

        public void Stop()
        {
            foreach (var source in sources) StopSource(source);
            clip = null;
            scheduled = false;
            fade = 0;
        }

        public void Tick(float dt, float level, float seconds)
        {
            if (clip == null) return;
            fade = Mathf.MoveTowards(fade, releasing ? 0f : 1f, dt / seconds);
            duck = Mathf.MoveTowards(duck, level, dt * (level < duck ? 3.7f : 1.8f));
            if (releasing && fade == 0) { Stop(); return; }

            double now = AudioSettings.dspTime;
            int other = 1 - lane;
            if (!scheduled && next - now < .55)
            {
                Schedule(sources[other], next);
                scheduled = true;
            }
            float mix = Mathf.Clamp01((float)((now - next) / clip.overlap));
            float gain = volume * fade * duck;
            sources[lane].volume = gain * (1f - mix);
            sources[other].volume = gain * mix;
            if (mix < 1f) return;
            lane = other;
            start = next;
            next = start + clip.end - clip.start - clip.overlap;
            scheduled = false;
        }

        private void Schedule(AudioSource source, double at)
        {
            source.clip = clip.clip;
            source.timeSamples = Mathf.RoundToInt(clip.start * clip.clip.frequency);
            source.volume = 0;
            source.PlayScheduled(at);
            source.SetScheduledEndTime(at + clip.end - clip.start);
        }
    }
}
