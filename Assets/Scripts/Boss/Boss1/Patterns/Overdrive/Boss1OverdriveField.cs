using UnityEngine;

/// <summary>Scene bindings and start selection for overdrive.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1DebrisField))]
public sealed class Boss1OverdriveField : MonoBehaviour
{
    [Header("Start Points")]
    [UnityEngine.Serialization.FormerlySerializedAs("leftSweepStartPoint")]
    [SerializeField] private Transform leftOverdriveStartPoint;
    [UnityEngine.Serialization.FormerlySerializedAs("rightSweepStartPoint")]
    [SerializeField] private Transform rightOverdriveStartPoint;

    [Header("Punch Pullback Points")]
    [Tooltip("충전 후 왼손을 뒤로 당길 위치입니다. 왼손 시작점보다 왼쪽에 배치합니다.")]
    [SerializeField] private Transform leftPunchPullbackPoint;
    [Tooltip("충전 후 오른손을 뒤로 당길 위치입니다. 오른손 시작점보다 오른쪽에 배치합니다.")]
    [SerializeField] private Transform rightPunchPullbackPoint;

    [Header("Attack Exit Points")]
    [Tooltip("오른손 Punch의 종료 X입니다. 왼쪽 벽 바깥에 배치하며 Y는 사용하지 않습니다.")]
    [SerializeField] private Transform leftPunchExitPoint;
    [Tooltip("왼손 Punch의 종료 X입니다. 오른쪽 벽 바깥에 배치하며 Y는 사용하지 않습니다.")]
    [SerializeField] private Transform rightPunchExitPoint;
    [Tooltip("후속 내려찍기의 종료 Y입니다. 바닥 아래에 배치하며 조준한 X는 유지합니다. 고철 대응 내려찍기에는 사용하지 않습니다.")]
    [SerializeField] private Transform slamExitPoint;

    [Header("벽")]
    [Tooltip("왼쪽으로 발사된 잔해가 도착할 맵 왼쪽 벽입니다.")]
    [SerializeField] private Collider2D leftWall;
    [Tooltip("오른쪽으로 발사된 잔해가 도착할 맵 오른쪽 벽입니다.")]
    [SerializeField] private Collider2D rightWall;

    [Header("본체")]
    [Tooltip("그로기 때 함께 내려올 본체와 얼굴, 눈의 이동 기준입니다.")]
    [SerializeField] private Transform bodyRoot;
    [Tooltip("본체에 피해를 전달하는 얼굴 Collider입니다. 그로기 하강 높이도 이 Collider로 계산합니다.")]
    [SerializeField] private Collider2D bodyCollider;

    [Header("References")]
    [SerializeField] private Boss1DebrisField debrisField;

    public Collider2D LeftWall => leftWall;
    public Collider2D RightWall => rightWall;
    public Collider2D BodyCollider => bodyCollider;
    public Transform BodyRoot => bodyRoot;
    public Transform SlamExitPoint => slamExitPoint;

    public bool HasValidBindings => isActiveAndEnabled
        && leftOverdriveStartPoint != null && rightOverdriveStartPoint != null
        && leftOverdriveStartPoint.gameObject.activeInHierarchy
        && rightOverdriveStartPoint.gameObject.activeInHierarchy
        && leftOverdriveStartPoint.position.x < rightOverdriveStartPoint.position.x
        && leftPunchPullbackPoint != null && rightPunchPullbackPoint != null
        && leftPunchPullbackPoint.gameObject.activeInHierarchy
        && rightPunchPullbackPoint.gameObject.activeInHierarchy
        && leftWall != null && leftWall.enabled && leftWall.gameObject.activeInHierarchy
        && rightWall != null && rightWall.enabled && rightWall.gameObject.activeInHierarchy
        && leftWall.bounds.max.x < rightWall.bounds.min.x
        && leftPunchExitPoint != null && leftPunchExitPoint.gameObject.activeInHierarchy
        && rightPunchExitPoint != null && rightPunchExitPoint.gameObject.activeInHierarchy
        && slamExitPoint != null && slamExitPoint.gameObject.activeInHierarchy
        && leftPunchExitPoint.position.x < leftWall.bounds.min.x
        && rightPunchExitPoint.position.x > rightWall.bounds.max.x
        && bodyCollider != null && bodyCollider.enabled && bodyCollider.gameObject.activeInHierarchy
        && bodyRoot != null && bodyRoot.gameObject.activeInHierarchy
        && bodyCollider.transform.IsChildOf(bodyRoot)
        && debrisField != null && debrisField.isActiveAndEnabled
        && debrisField.FloorCollider != null && debrisField.FloorCollider.enabled
        && debrisField.FloorCollider.gameObject.activeInHierarchy
        && slamExitPoint.position.y < debrisField.FloorCollider.bounds.min.y;

    public bool CanStart => HasValidBindings;

    private void Reset() => ResolveReferences();
    private void Awake() => ResolveReferences();

    private void OnDisable()
    {
        Boss1Controller controller = GetComponent<Boss1Controller>();
        if (controller != null && controller.CurrentPattern is Boss1OverdrivePattern)
            controller.CancelCurrentPattern();
    }

    public bool TrySelectStart(out Boss1HandSide side, out Transform startPoint,
        out Transform pullbackPoint, out Transform punchExitPoint, out Collider2D targetWall)
    {
        side = default;
        startPoint = null;
        pullbackPoint = null;
        punchExitPoint = null;
        targetWall = null;
        if (!CanStart) return false;

        side = Random.Range(0, 2) == 0 ? Boss1HandSide.Left : Boss1HandSide.Right;
        startPoint = side == Boss1HandSide.Left ? leftOverdriveStartPoint : rightOverdriveStartPoint;
        pullbackPoint = side == Boss1HandSide.Left ? leftPunchPullbackPoint : rightPunchPullbackPoint;
        punchExitPoint = side == Boss1HandSide.Left ? rightPunchExitPoint : leftPunchExitPoint;
        targetWall = side == Boss1HandSide.Left ? rightWall : leftWall;
        return true;
    }

    private void ResolveReferences()
    {
        if (debrisField == null) debrisField = GetComponent<Boss1DebrisField>();
    }
}
