using UnityEngine;

public sealed class FocusPoint : MonoBehaviour
{
    [Min(0)] public int sceneIndex = 3;
    [Tooltip("Allow U to return to Train when the mind scene has no scripted exit.")]
    public bool returnOnU;
}
