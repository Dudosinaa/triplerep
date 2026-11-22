using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(TrainHostileEncounter))]
public sealed class TrainCheckpoint : MonoBehaviour
{
    private const double DeathScreenDuration = 5d;

    [SerializeField] private FirstPersonPlayer player;

    private TrainHostileEncounter encounter;
    private PlayerStress stress;
    private PistolController pistol;
    private GameObject deathScreen;
    private PlayerSuspension suspension;
    private PlayerSnapshot entry, checkpoint;
    private EnemySnapshot[] enemies = System.Array.Empty<EnemySnapshot>();
    private bool dying;
    private double restartAt;
    private float timeScaleBeforeDeath;
    private bool wasAudioPaused;

    private sealed class PlayerSnapshot
    {
        internal FirstPersonPlayer.CheckpointPose Pose;
        internal float Stress;
        internal bool Equipped;
        internal int Rounds, Reserve;
    }

    private struct EnemySnapshot
    {
        internal HostileTrainNpc Enemy;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal int Health;
        internal bool Enabled;
    }

    private void Awake()
    {
        encounter = GetComponent<TrainHostileEncounter>();
        if (player != null)
        {
            stress = player.GetComponent<PlayerStress>();
            pistol = player.GetComponent<PistolController>();
        }
        if (stress == null || pistol == null)
        {
            Debug.LogError("TrainCheckpoint needs the Train player with stress and a pistol.", this);
            enabled = false;
            return;
        }
        deathScreen = CreateDeathScreen();
    }

    private void OnEnable()
    {
        encounter.Activated += CaptureEncounter;
        stress.MaximumReached += BeginDeath;
    }

    private void OnDisable()
    {
        if (encounter != null) encounter.Activated -= CaptureEncounter;
        if (stress != null) stress.MaximumReached -= BeginDeath;
    }

    private void Start() => entry = CapturePlayer();

    private PlayerSnapshot CapturePlayer() => new PlayerSnapshot
    {
        Pose = player.CaptureCheckpointPose(),
        // A checkpoint must be playable even if the stress debug input reaches 100 on this frame.
        Stress = stress.stress < 100f ? stress.stress : entry?.Stress ?? 0f,
        Equipped = pistol.IsEquipped,
        Rounds = pistol.Rounds,
        Reserve = pistol.ReserveAmmo
    };

    private void CaptureEncounter()
    {
        checkpoint = CapturePlayer();
        var activeEnemies = encounter.GetActiveEnemies();
        enemies = new EnemySnapshot[activeEnemies.Length];
        for (int i = 0; i < activeEnemies.Length; i++)
        {
            HostileTrainNpc enemy = activeEnemies[i];
            enemies[i] = new EnemySnapshot
            {
                Enemy = enemy,
                Position = enemy.transform.position,
                Rotation = enemy.transform.rotation,
                Health = enemy.Health,
                Enabled = enemy.enabled
            };
        }
    }

    private void BeginDeath()
    {
        if (dying) return;
        dying = true;
        timeScaleBeforeDeath = Time.timeScale;
        wasAudioPaused = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        suspension = new PlayerSuspension(player);
        Cursor.visible = false;
        foreach (var enemy in encounter.GetActiveEnemies())
            enemy.enabled = false;

        deathScreen.SetActive(true);
        restartAt = Time.realtimeSinceStartupAsDouble + DeathScreenDuration;
    }

    private void Update()
    {
        if (!dying || Time.realtimeSinceStartupAsDouble < restartAt) return;

        // Before the story event there is only the Train entry point.
        PlayerSnapshot saved = checkpoint ?? entry;
        player.RestoreCheckpointPose(saved.Pose);
        stress.RestoreCheckpoint(saved.Stress);
        foreach (EnemySnapshot state in enemies)
        {
            HostileTrainNpc enemy = state.Enemy;
            if (enemy == null) continue;
            enemy.gameObject.SetActive(true);
            enemy.enabled = state.Enabled;
            enemy.RestoreEncounterStart(state.Position, state.Rotation, state.Health);
        }
        Physics.SyncTransforms();
        suspension.Restore();
        suspension = null;
        pistol.RestoreCheckpoint(saved.Equipped, saved.Rounds, saved.Reserve);

        Time.timeScale = timeScaleBeforeDeath > 0f ? timeScaleBeforeDeath : 1f;
        AudioListener.pause = wasAudioPaused;
        deathScreen.SetActive(false);
        dying = false;
    }

    private void OnDestroy()
    {
        if (!dying) return;
        Time.timeScale = timeScaleBeforeDeath;
        AudioListener.pause = wasAudioPaused;
    }

    private GameObject CreateDeathScreen()
    {
        var screen = new GameObject("Train Death Screen", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = screen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        FillParent(background.rectTransform, screen.transform);
        background.color = Color.black;
        var title = new GameObject("Death Message", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        FillParent(title.rectTransform, background.transform);
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.text = "Ты умер";
        title.fontSize = 64;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        title.raycastTarget = false;

        screen.SetActive(false);
        return screen;
    }

    private static void FillParent(RectTransform child, Transform parent)
    {
        child.SetParent(parent, false);
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;
        child.offsetMin = child.offsetMax = Vector2.zero;
    }
}
