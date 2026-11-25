using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneLoadingIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform gear;
    [SerializeField] private float rotationSpeed = 90f;

    public bool IsConfigured => gear != null;

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    private void OnEnable() => gear.localRotation = Quaternion.identity;

    private void Update() => gear.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);
}
