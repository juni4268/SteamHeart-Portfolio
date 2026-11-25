using UnityEngine;

[RequireComponent(typeof(Player))]
public class CameraTargetDirectionSmoother : MonoBehaviour
{
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float turnSmoothTime = 0.25f;
    [SerializeField, Min(0f)] private float turnDelay = 0.2f;

    private Player player;
    private Vector3 baseLocalPosition;
    private float targetOffsetX;
    private float currentOffsetX;
    private float offsetVelocityX;
    private float lookDirection;
    private float turnTimer;

    private void Awake()
    {
        player = GetComponent<Player>();

        if (cameraTarget == null)
        {
            Transform foundTarget = transform.Find("CameraTarget");
            cameraTarget = foundTarget;
        }

        if (cameraTarget == null) return;

        baseLocalPosition = cameraTarget.localPosition;
        targetOffsetX = Mathf.Abs(baseLocalPosition.x);
        lookDirection = GetFacingSign();
        currentOffsetX = lookDirection * targetOffsetX;
        ApplyCameraTargetPosition();
    }

    private void LateUpdate()
    {
        if (cameraTarget == null) return;

        UpdateCameraTarget(player.input.Player.Movement.ReadValue<Vector2>().x, Time.deltaTime);
    }

    private void UpdateCameraTarget(float horizontalInput, float deltaTime)
    {
        float facingSign = GetFacingSign();
        if (facingSign != lookDirection && horizontalInput * facingSign > 0f)
        {
            turnTimer += deltaTime;
            if (turnTimer >= turnDelay)
            {
                lookDirection = facingSign;
                turnTimer = 0f;
            }
        }
        else turnTimer = 0f;

        float desiredOffsetX = lookDirection * targetOffsetX;
        currentOffsetX = Mathf.SmoothDamp(
            currentOffsetX,
            desiredOffsetX,
            ref offsetVelocityX,
            turnSmoothTime,
            Mathf.Infinity,
            deltaTime);

        ApplyCameraTargetPosition();
    }

    private float GetFacingSign()
    {
        return transform.localScale.x >= 0f ? 1f : -1f;
    }

    private void ApplyCameraTargetPosition()
    {
        Vector3 baseWorldPosition = transform.TransformPoint(new Vector3(
            0f,
            baseLocalPosition.y,
            baseLocalPosition.z));
        cameraTarget.position = new Vector3(
            transform.position.x + currentOffsetX,
            baseWorldPosition.y,
            baseWorldPosition.z);
    }
}
