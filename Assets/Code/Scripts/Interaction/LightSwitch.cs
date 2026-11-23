using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class LightSwitch : MonoBehaviour
{
    [Serializable]
    private struct Fixture
    {
        public Light light;
        public Renderer bulb;
    }

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Transform pivot;
    [SerializeField] private bool startsOn;
    [SerializeField] private Fixture[] fixtures = Array.Empty<Fixture>();

    private MaterialPropertyBlock bulbProperties;
    private bool isOn;

    private void Awake() => SetState(startsOn);

    internal void Toggle() => SetState(!isOn);

    private void SetState(bool on)
    {
        isOn = on;
        pivot.localRotation = Quaternion.AngleAxis(on ? -90f : 90f, Vector3.right);

        foreach (var fixture in fixtures)
        {
            if (fixture.light != null) fixture.light.enabled = on;
            if (fixture.bulb == null) continue;

            var material = fixture.bulb.sharedMaterial;
            if (material == null || !material.HasProperty(EmissionColor)) continue;

            bulbProperties ??= new MaterialPropertyBlock();
            fixture.bulb.GetPropertyBlock(bulbProperties);
            bulbProperties.SetColor(EmissionColor, on ? material.GetColor(EmissionColor) : Color.black);
            fixture.bulb.SetPropertyBlock(bulbProperties);
        }
    }
}
