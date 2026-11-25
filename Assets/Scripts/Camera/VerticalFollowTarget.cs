using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class VerticalFollowTarget : MonoBehaviour
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float fixedWorldX;

    private void OnEnable() => UpdatePosition();

    private void LateUpdate() => UpdatePosition();

    private void UpdatePosition()
    {
        if (followTarget == null) return;

        transform.position = new Vector3(fixedWorldX, followTarget.position.y, followTarget.position.z);
    }
}
