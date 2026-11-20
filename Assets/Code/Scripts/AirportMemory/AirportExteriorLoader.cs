using System;
using System.IO;
using GLTFast;
using UnityEngine;

// The source GLB is kept in StreamingAssets so it is also available in builds.
public sealed class AirportExteriorLoader : MonoBehaviour
{
    [SerializeField] private string relativePath = "AirportMemory/Airport.glb";
    private GltfImport gltf;
    private bool destroyed;

    private async void Start()
    {
        gltf = new GltfImport();
        try
        {
            if (!await gltf.Load(Path.Combine(Application.streamingAssetsPath, relativePath)))
                throw new Exception("Airport.glb could not be loaded");
            if (destroyed) return;
            if (!await gltf.InstantiateMainSceneAsync(new GameObjectInstantiator(gltf, transform)))
                throw new Exception("Airport.glb could not be instantiated");
        }
        catch (Exception exception)
        {
            if (!destroyed) Debug.LogError("Airport exterior: " + exception.Message, this);
        }
    }

    private void OnDestroy()
    {
        destroyed = true;
        gltf?.Dispose();
    }
}
