using UnityEngine;
using UnityEngine.Rendering;

public class BackgroundParallax : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [Range(0f, 1f)]
    [SerializeField] private float horizontalFollow = 0.7f;

    private Vector3 initialPosition;
    private float initialCameraX;

    private void OnEnable()
    {
        if (targetCamera == null) return;

        initialPosition = transform.position;
        initialCameraX = targetCamera.transform.position.x;
        RenderPipelineManager.beginCameraRendering += UpdatePosition;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= UpdatePosition;
    }

    private void UpdatePosition(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != targetCamera) return;

        Vector3 position = initialPosition;
        position.x += (targetCamera.transform.position.x - initialCameraX) * horizontalFollow;
        transform.position = position;
    }
}
