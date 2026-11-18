using UnityEngine;
using UnityEngine.UI;

public sealed class ConsciousnessPlaceholder : MonoBehaviour
{
    private void Start()
    {
        var camera = new GameObject("Consciousness Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        var view = camera.GetComponent<Camera>();
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;
        var go = new GameObject("Consciousness Placeholder", typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var textObject = new GameObject("Return Hint", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(go.transform, false);
        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "Сознание\nU — вернуться";
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        if (!TrainMindSession.OwnsMindScene(gameObject.scene))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
