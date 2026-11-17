using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MetroModelMaterials.Editor
{
    // Replaces material references on glass meshes in this one model only.
    // Never edits shared source materials, mesh data, scene objects or other models.
    internal sealed class MetroWindowGlassPostprocessor : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Art/Models/Environment/Metro/Models/Metro_Unity.fbx";
        private const string MaterialDirectory = "Assets/Art/Models/Environment/Metro/Materials/Glass/";
        private const string AuditDirectory = "Assets/Art/Models/Environment/Metro/Materials/.metro-setup/windows/";

        private static readonly Regex Suffix = new Regex(@"\.\d+$");
        private static readonly Dictionary<string, string> GlassFiles = new Dictionary<string, string>
        {
            { "LateralTren", "WindowGlass_LateralTren.mat" },
            { "PuertasLaterales", "WindowGlass_PuertasLaterales.mat" },
            { "PuertaDelantera", "WindowGlass_PuertaDelantera.mat" },
            { "Black", "WindowGlass_Cab.mat" }
        };

        public override uint GetVersion() => 1;
        public override int GetPostprocessOrder() => 1000;

        private void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            foreach (string file in GlassFiles.Values)
                context.DependsOnArtifact(MaterialDirectory + file);
        }

        private static bool IsGlass(string objectName, string materialFamily)
        {
            switch (Suffix.Replace(objectName, ""))
            {
                case "VentanaLateral1_LP":
                case "VentanaLateral2_LP":
                case "VentanaLateral3_LP":
                    return materialFamily == "LateralTren";
                case "CristalesPuertasLaterales_LP":
                case "CristalesPuertasLaterales2_LP":
                    return materialFamily == "PuertasLaterales";
                case "CristalPuertaDelanteraCabezaTren_LP":
                    return materialFamily == "PuertaDelantera";
                case "CristalesFrontalesCabezaTren_LP":
                    // This entire group must retain its original opaque materials.
                    return false;
                default:
                    return false;
            }
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (assetPath != ModelPath) return;
            var loaded = new Dictionary<string, Material>();
            foreach (var pair in GlassFiles)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialDirectory + pair.Value);
                if (material == null)
                {
                    context.LogImportError("Missing window glass material: " + pair.Value);
                    return;
                }
                loaded.Add(pair.Key, material);
            }

            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var importRecords = new List<ImportRecord>();
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var paths = new string[materials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                    paths[slot] = AssetDatabase.GetAssetPath(materials[slot]);
                importRecords.Add(new ImportRecord { name = renderer.name, materialPaths = paths });
            }

            int changed = 0;
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                bool replace = false;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] == null) continue;
                    string family = Suffix.Replace(slots[slot].name, "");
                    if (!IsGlass(renderer.name, family)) continue;
                    slots[slot] = loaded[family];
                    replace = true;
                }
                if (!replace) continue;
                renderer.sharedMaterials = slots;
                changed++;
            }
            // Record Unity's actual slots before this processor. FBX material connection order
            // can differ from Unity submesh order, especially for unused or degenerate meshes.
            File.WriteAllText(AuditDirectory + "import-baseline.json",
                JsonUtility.ToJson(new ImportBaseline { renderers = importRecords.ToArray() }, true));
            Debug.Log("[MetroWindowGlass] Assigned separate glass materials to " + changed + " window renderers.");
        }

        [Serializable] private sealed class Baseline { public RendererRecord[] renderers; }
        [Serializable] private sealed class RendererRecord
        {
            public string name;
            public bool glass;
            public string[] materials;
        }
        [Serializable] private sealed class ImportBaseline { public ImportRecord[] renderers; }
        [Serializable] private sealed class ImportRecord
        {
            public string name;
            public string[] materialPaths;
        }
        [Serializable] private sealed class Validation
        {
            public int renderers;
            public int glassRenderers;
            public int unchangedOtherRenderers;
            public string[] errors;
        }

        [MenuItem("Tools/Metro/Validate Window Glass")]
        private static void Validate()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Metro_Unity model is unavailable.");
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(AuditDirectory + "baseline-renderers.json"));
            var imported = JsonUtility.FromJson<ImportBaseline>(File.ReadAllText(AuditDirectory + "import-baseline.json"));
            var originalSlots = new Dictionary<string, string[]>();
            foreach (var record in imported.renderers) originalSlots.Add(record.name, record.materialPaths);
            var actual = new Dictionary<string, MeshRenderer>();
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                actual.Add(renderer.name, renderer);

            var errors = new List<string>();
            int glass = 0, unchanged = 0;
            foreach (var record in baseline.renderers)
            {
                if (!actual.TryGetValue(record.name, out var renderer))
                {
                    errors.Add("Missing renderer: " + record.name);
                    continue;
                }
                var slots = renderer.sharedMaterials;
                var expectedSlots = originalSlots[record.name];
                bool valid = slots.Length == expectedSlots.Length;
                for (int slot = 0; slot < Math.Min(slots.Length, expectedSlots.Length); slot++)
                {
                    string expected = expectedSlots[slot];
                    if (record.glass)
                    {
                        string family = Suffix.Replace(Path.GetFileNameWithoutExtension(expected), "");
                        expected = MaterialDirectory + GlassFiles[family];
                    }
                    if (AssetDatabase.GetAssetPath(slots[slot]) != expected) valid = false;
                }
                if (!valid) errors.Add("Unexpected material slots: " + record.name);
                else if (record.glass) glass++;
                else unchanged++;
            }
            if (actual.Count != baseline.renderers.Length) errors.Add("Renderer count changed.");
            var result = new Validation
            {
                renderers = actual.Count,
                glassRenderers = glass,
                unchangedOtherRenderers = unchanged,
                errors = errors.ToArray()
            };
            File.WriteAllText(AuditDirectory + "unity-validation.json", JsonUtility.ToJson(result, true));
            if (errors.Count > 0) Debug.LogError("[MetroWindowGlass] Validation failed: " + string.Join("; ", errors));
            else Debug.Log("[MetroWindowGlass] Validation passed: " + glass + " glass renderers; " + unchanged + " other renderers retain their original materials.");
        }
    }
}
