using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string LanguageKey = "MainMenu.Language";
    private const string MusicVolumeKey = "MainMenu.MusicVolume";
    private const float DefaultMusicVolume = 0.5f;
    private const float BackgroundTravel = 0.009f;
    private const float BackgroundFollowSpeed = 8f;

    [SerializeField] private RawImage trainBackground;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValue;
    [SerializeField] private AudioSource menuMusic;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Text musicVolumeValue;
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Button applyButton;
    [SerializeField] private Dropdown languageDropdown;
    [SerializeField] private Text playLabel;
    [SerializeField] private Text settingsLabel;
    [SerializeField] private Text quitLabel;
    [SerializeField] private Text settingsTitle;
    [SerializeField] private Text volumeLabel;
    [SerializeField] private Text musicVolumeLabel;
    [SerializeField] private Text resolutionLabel;
    [SerializeField] private Text applyLabel;
    [SerializeField] private Text backLabel;

    private List<Vector2Int> resolutions;
    private Vector2Int currentResolution;
    private Vector2 backgroundOffset;
    private static Vector2Int ScreenSize => new Vector2Int(Screen.width, Screen.height);
    private static float SavedMusicVolume =>
        Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        UpdateBackground();
        menuMusic.volume = SavedMusicVolume;
        menuMusic.Play();
        volumeSlider.onValueChanged.AddListener(ChangeVolume);
        musicVolumeSlider.onValueChanged.AddListener(ChangeMusicVolume);
        resolutionDropdown.onValueChanged.AddListener(_ => UpdateApplyButton());
        applyButton.onClick.AddListener(ApplySettings);
        settingsButton.onClick.AddListener(OpenSettings);
        backButton.onClick.AddListener(CloseSettings);
        playButton.onClick.AddListener(Play);
        quitButton.onClick.AddListener(Quit);

        int language = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey), 0, languageDropdown.options.Count - 1);
        languageDropdown.SetValueWithoutNotify(language);
        ShowLanguage(language);
        languageDropdown.onValueChanged.AddListener(ChangeLanguage);
        playButton.Select();
    }

    private void ChangeLanguage(int language)
    {
        ShowLanguage(language);
        PlayerPrefs.SetInt(LanguageKey, language);
        PlayerPrefs.Save();
    }

    private void ShowLanguage(int language)
    {
        bool english = language == 1;
        playLabel.text = english ? "Play" : "Играть";
        settingsLabel.text = english ? "Settings" : "Настройки";
        quitLabel.text = english ? "Quit" : "Выход";
        settingsTitle.text = settingsLabel.text;
        volumeLabel.text = english ? "Master Volume" : "Общая громкость";
        musicVolumeLabel.text = english ? "Menu Music Volume" : "Громкость музыки меню";
        resolutionLabel.text = english ? "Screen Resolution" : "Разрешение экрана";
        applyLabel.text = english ? "Apply" : "Применить";
        backLabel.text = english ? "Back" : "Назад";
    }

    private void Update()
    {
        UpdateBackground();
        if (settingsPanel.activeSelf && ScreenSize != currentResolution)
            RefreshResolutions();
    }

    private void UpdateBackground()
    {
        Vector2 target = Vector2.zero;
        if (Application.isFocused && Mouse.current != null && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            target.x = Mathf.Clamp(2f * pointer.x / Screen.width - 1f, -1f, 1f);
            target.y = Mathf.Clamp(2f * pointer.y / Screen.height - 1f, -1f, 1f);
        }

        float blend = 1f - Mathf.Exp(-BackgroundFollowSpeed * Time.unscaledDeltaTime);
        backgroundOffset = Vector2.Lerp(backgroundOffset, target * BackgroundTravel, blend);
        float visibleSize = 1f - 2f * BackgroundTravel;
        trainBackground.uvRect = new Rect(
            BackgroundTravel - backgroundOffset.x, BackgroundTravel - backgroundOffset.y,
            visibleSize, visibleSize);
    }

    private void OpenSettings()
    {
        float volume = GameSettings.MasterVolume;
        volumeSlider.SetValueWithoutNotify(volume);
        ShowVolume(volumeValue, volume);
        float musicVolume = SavedMusicVolume;
        musicVolumeSlider.SetValueWithoutNotify(musicVolume);
        ShowVolume(musicVolumeValue, musicVolume);
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshResolutions();
        volumeSlider.Select();
    }

    private void CloseSettings()
    {
        AudioListener.volume = GameSettings.MasterVolume;
        menuMusic.volume = SavedMusicVolume;
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
        settingsButton.Select();
    }

    private void ChangeVolume(float volume)
    {
        AudioListener.volume = volume;
        ShowVolume(volumeValue, volume);
        UpdateApplyButton();
    }

    private void ChangeMusicVolume(float volume)
    {
        menuMusic.volume = volume;
        ShowVolume(musicVolumeValue, volume);
        UpdateApplyButton();
    }

    private static void ShowVolume(Text label, float volume) =>
        label.text = $"{Mathf.RoundToInt(volume * 100)}%";

    private void RefreshResolutions()
    {
        currentResolution = ScreenSize;
        resolutions = Screen.resolutions
            .Select(mode => new Vector2Int(mode.width, mode.height))
            .Append(currentResolution)
            .Distinct()
            .OrderByDescending(size => size.x)
            .ThenByDescending(size => size.y)
            .ToList();

        resolutionDropdown.Hide();
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(resolutions.Select(size => $"{size.x} × {size.y}").ToList());
        int currentIndex = resolutions.IndexOf(currentResolution);
        resolutionDropdown.SetValueWithoutNotify(currentIndex);
        UpdateApplyButton();
    }

    private bool HasVolumeChange => !Mathf.Approximately(volumeSlider.value, GameSettings.MasterVolume);
    private bool HasMusicVolumeChange => !Mathf.Approximately(musicVolumeSlider.value, SavedMusicVolume);
    private Vector2Int SelectedResolution => resolutions[resolutionDropdown.value];

    private void UpdateApplyButton() =>
        applyButton.interactable = HasVolumeChange || HasMusicVolumeChange || SelectedResolution != currentResolution;

    private void ApplySettings()
    {
        if (HasVolumeChange)
            GameSettings.SetMasterVolume(volumeSlider.value);

        if (HasMusicVolumeChange)
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolumeSlider.value);

        if (SelectedResolution != currentResolution)
            GameSettings.SetResolution(SelectedResolution);
        else
            PlayerPrefs.Save();

        applyButton.interactable = false;
    }

    private void Play()
    {
        if (!SceneEntryCheckpoint.TryGetStartIndex(out int index))
        {
            Debug.LogError("Main menu cannot load the starting scene. Enable Home in the build scene list.");
            return;
        }

        PlayerPrefs.Save();
        playButton.interactable = false;
        SceneManager.LoadSceneAsync(index);
    }

    private static void Quit()
    {
        PlayerPrefs.Save();
        Application.Quit();
    }
}
