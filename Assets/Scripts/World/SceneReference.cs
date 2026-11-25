using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public sealed class SceneReference
{
#if UNITY_EDITOR
    [SerializeField] private SceneAsset sceneAsset;
#endif
    [SerializeField, HideInInspector] private string scenePath;

    public string Path => scenePath;
    public bool IsAssigned => !string.IsNullOrWhiteSpace(scenePath);

#if UNITY_EDITOR
    public void Validate()
    {
        scenePath = sceneAsset == null ? string.Empty : AssetDatabase.GetAssetPath(sceneAsset);
    }
#endif
}
