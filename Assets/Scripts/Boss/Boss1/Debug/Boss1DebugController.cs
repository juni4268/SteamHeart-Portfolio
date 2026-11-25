using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1Controller))]
public sealed class Boss1DebugController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Boss1Controller controller;
    [SerializeField] private Boss1PatternScheduler scheduler;
    [SerializeField] private Boss1PatternBase selectedPattern;

    [Header("Hand Test")]
    [SerializeField] private Vector2 leftHandOffset = Vector2.left;
    [SerializeField] private Vector2 rightHandOffset = Vector2.right;
    [SerializeField, Min(0f)] private float handMoveDuration = 0.5f;

    public Boss1Controller Controller => controller;
    public Boss1PatternScheduler Scheduler => scheduler;
    public Boss1PatternBase SelectedPattern => selectedPattern;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public bool ExecuteSelectedPattern()
    {
        if (!CanUseDebugControls() || selectedPattern == null)
        {
            return false;
        }

        scheduler?.StopScheduling();
        return controller.TryStartPattern(selectedPattern);
    }

    public bool StartScheduler()
    {
        return CanUseDebugControls() && scheduler != null && scheduler.StartScheduling();
    }

    public void StopScheduler()
    {
        scheduler?.StopScheduling();
    }

    public bool CancelCurrentPattern()
    {
        return CanUseDebugControls() && controller.CancelCurrentPattern();
    }

    public void MoveLeftHand()
    {
        MoveHand(controller?.LeftHand, leftHandOffset);
    }

    public void MoveRightHand()
    {
        MoveHand(controller?.RightHand, rightHandOffset);
    }

    public void MoveBothHands()
    {
        MoveLeftHand();
        MoveRightHand();
    }

    public void ReturnLeftHand()
    {
        ReturnHand(controller?.LeftHand);
    }

    public void ReturnRightHand()
    {
        ReturnHand(controller?.RightHand);
    }

    public void ReturnBothHands()
    {
        ReturnLeftHand();
        ReturnRightHand();
    }

    private bool CanUseDebugControls()
    {
        return Application.isPlaying && controller != null && !controller.IsDead;
    }

    private void MoveHand(Boss1HandController hand, Vector2 offset)
    {
        if (!CanUseDebugControls() || hand == null)
        {
            return;
        }

        hand.MoveTo((Vector2)hand.transform.position + offset, handMoveDuration);
    }

    private void ReturnHand(Boss1HandController hand)
    {
        if (!CanUseDebugControls() || hand == null)
        {
            return;
        }

        hand.ReturnToIdle(handMoveDuration);
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<Boss1Controller>();
        }

        if (scheduler == null)
        {
            scheduler = GetComponent<Boss1PatternScheduler>();
        }

        if (selectedPattern == null)
        {
            selectedPattern = GetComponent<Boss1PatternBase>();
        }
    }
}
