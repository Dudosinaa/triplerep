using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Temporary visual replacement approved for the missing LFS weapon assets.
// Ammunition, raycast hits, aim and reload are still controlled by PistolController.
public sealed class ProceduralPistol : MonoBehaviour
{
    private Transform assembly;
    private Transform slide;
    private GameObject flash;
    private AudioSource source;
    private AudioClip shotSound;
    private readonly List<Material> materials = new List<Material>();
    private float recoil;
    private float reloadRemaining;
    private void Awake()
    {
        assembly = new GameObject("Procedural Pistol Assembly").transform;
        assembly.SetParent(transform, false);
        var metal = MakeMaterial(new Color(0.12f, 0.14f, 0.16f), 0.75f);
        var grip = MakeMaterial(new Color(0.22f, 0.10f, 0.06f), 0.05f);
        var skin = MakeMaterial(new Color(0.60f, 0.39f, 0.27f), 0f);
        Part("Frame", new Vector3(0f, 0.025f, 0.035f), new Vector3(0.055f, 0.07f, 0.22f), metal);
        slide = Part("Slide", new Vector3(0f, 0.085f, 0.045f), new Vector3(0.06f, 0.045f, 0.28f), metal);
        Part("Barrel", new Vector3(0f, 0.073f, 0.20f), new Vector3(0.024f, 0.024f, 0.06f), metal);
        var handle = Part("Grip", new Vector3(0f, -0.07f, -0.035f), new Vector3(0.055f, 0.14f, 0.07f), grip);
        handle.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Part("Front Sight", new Vector3(0f, 0.115f, 0.16f), new Vector3(0.008f, 0.018f, 0.014f), metal);
        Part("Rear Sight Left", new Vector3(-0.018f, 0.115f, -0.075f), new Vector3(0.012f, 0.018f, 0.012f), metal);
        Part("Rear Sight Right", new Vector3(0.018f, 0.115f, -0.075f), new Vector3(0.012f, 0.018f, 0.012f), metal);
        Part("Trigger Guard", new Vector3(0f, -0.035f, 0.065f), new Vector3(0.018f, 0.014f, 0.095f), metal);
        Part("Right Hand", new Vector3(0.025f, -0.085f, -0.045f), new Vector3(0.075f, 0.075f, 0.095f), skin);
        Part("Left Hand", new Vector3(-0.035f, -0.09f, 0.015f), new Vector3(0.065f, 0.065f, 0.12f), skin);
        var bright = MakeMaterial(new Color(1f, 0.65f, 0.08f), 0f);
        flash = Part("Muzzle Flash", new Vector3(0f, 0.073f, 0.25f), new Vector3(0.07f, 0.07f, 0.09f), bright).gameObject;
        flash.SetActive(false);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var samples = new float[4410];
        var noise = new System.Random(410);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / 44100f;
            samples[i] = ((float)noise.NextDouble() * 2f - 1f + Mathf.Sin(t * 2f * Mathf.PI * 90f)) *
                Mathf.Exp(-t * 70f) * 0.3f;
        }
        shotSound = AudioClip.Create("Procedural pistol shot", samples.Length, 1, 44100, false);
        shotSound.SetData(samples, 0);
    }
    private Material MakeMaterial(Color color, float metallic)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.35f);
        materials.Add(material);
        return material;
    }
    private Transform Part(string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(assembly, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }
    public void Shot() { recoil = 1f; source.PlayOneShot(shotSound); }
    public void Reload() { reloadRemaining = 2.2f; }
    public void CancelReload() { reloadRemaining = 0f; }
    private void LateUpdate()
    {
        recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 8f);
        reloadRemaining = Mathf.Max(0f, reloadRemaining - Time.deltaTime);
        float reload = reloadRemaining > 0f ? Mathf.Sin((1f - reloadRemaining / 2.2f) * Mathf.PI) : 0f;
        assembly.localRotation = Quaternion.Euler(-recoil * 12f + reload * 35f, 0f, reload * -25f);
        assembly.localPosition = new Vector3(0f, -reload * 0.12f, -recoil * 0.035f);
        slide.localPosition = new Vector3(0f, 0.085f, 0.045f - recoil * 0.045f);
        flash.SetActive(recoil > 0.75f);
    }
    private void OnDisable()
    {
        recoil = 0f;
        if (flash != null) flash.SetActive(false);
    }
    private void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
        if (shotSound != null) Destroy(shotSound);
    }
}
