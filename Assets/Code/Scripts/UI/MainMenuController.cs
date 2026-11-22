using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string HomeScene = "Assets/Level/Scenes/Locations/Home.unity";

    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValue;
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Button applyButton;

    private List<Vector2Int> resolutions;
    private Vector2Int currentResolution;
    private static Vector2Int ScreenSize => new Vector2Int(Screen.width, Screen.height);

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float volume = GameSettings.MasterVolume;
        volumeSlider.SetValueWithoutNotify(volume);
        ShowVolume(volume);
        volumeSlider.onValueChanged.AddListener(ChangeVolume);
        resolutionDropdown.onValueChanged.AddListener(UpdateApplyButton);
        applyButton.onClick.AddListener(ApplyResolution);
        settingsButton.onClick.AddListener(OpenSettings);
        backButton.onClick.AddListener(CloseSettings);
        playButton.onClick.AddListener(Play);
        quitButton.onClick.AddListener(Quit);
    }

    private void Update()
    {
        if (settingsPanel.activeSelf && ScreenSize != currentResolution)
            RefreshResolutions();
    }

    private void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshResolutions();
        volumeSlider.Select();
    }

    private void CloseSettings()
    {
        PlayerPrefs.Save();
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
        settingsButton.Select();
    }

    private void ChangeVolume(float volume)
    {
        GameSettings.SetMasterVolume(volume);
        ShowVolume(volume);
    }

    private void ShowVolume(float volume) => volumeValue.text = $"{Mathf.RoundToInt(volume * 100)}%";

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
        UpdateApplyButton(currentIndex);
    }

    private void UpdateApplyButton(int index) =>
        applyButton.interactable = resolutions[index] != currentResolution;

    private void ApplyResolution() => GameSettings.SetResolution(resolutions[resolutionDropdown.value]);

    private void Play()
    {
        if (!LocationTransition.TryGetBuildIndex(HomeScene, out int index))
        {
            Debug.LogError($"Main menu cannot load {HomeScene}. Enable it in the build scene list.");
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
