using UnityEngine;

internal static class GameSettings
{
    private const string VolumeKey = "GameSettings.MasterVolume";
    private const string WidthKey = "GameSettings.ScreenWidth";
    private const string HeightKey = "GameSettings.ScreenHeight";

    public static float MasterVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Restore()
    {
        AudioListener.volume = MasterVolume;

        int width = PlayerPrefs.GetInt(WidthKey);
        int height = PlayerPrefs.GetInt(HeightKey);
        if (width <= 0 || height <= 0 || (width == Screen.width && height == Screen.height))
            return;

        foreach (var mode in Screen.resolutions)
        {
            if (mode.width != width || mode.height != height) continue;
            Screen.SetResolution(width, height, Screen.fullScreenMode);
            break;
        }
    }

    public static void SetMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
    }

    public static void SetResolution(Vector2Int size)
    {
        Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
        PlayerPrefs.SetInt(WidthKey, size.x);
        PlayerPrefs.SetInt(HeightKey, size.y);
        PlayerPrefs.Save();
    }
}
