using UnityEngine;
using UnityEngine.Events;

/// <summary>Requests head animation through semantic parameters and applies body death motion.</summary>
[DisallowMultipleComponent]
public sealed class Boss1BodyAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("그로기 상태를 제어하는 Animator Bool Parameter입니다.")]
    [SerializeField] private string groggyParameter = "IsGroggy";
    [Tooltip("그로기 재생 중 숨길 기본 그림입니다. 머리와 별도로 표시되는 눈 등을 연결합니다.")]
    [SerializeField] private SpriteRenderer[] groggyHiddenRenderers;
    [SerializeField] private Boss1GroggyEffect groggyEffect;

    [Header("Death")]
    [Tooltip("사망 모션에 진입하면 숨길 오브젝트입니다. Body, Back과 양손은 포함하지 않습니다.")]
    [SerializeField] private GameObject[] deathHiddenObjects;
    [Tooltip("사망 시 손 전체가 이 높이 아래로 떨어지면 숨깁니다. 손의 X 위치는 유지합니다.")]
    [SerializeField] private Transform deathHandDespawnAnchor;
    [Tooltip("사망 시 몸이 가라앉을 최종 높이입니다. 이미 이보다 낮으면 현재 높이를 유지합니다.")]
    [SerializeField] private Transform deathRestAnchor;
    [Tooltip("사망 Clip에서 0~1로 애니메이션하는 가라앉기 진행도 Float Parameter입니다.")]
    [SerializeField] private string deathSinkParameter = "DeathSinkProgress";
    [Tooltip("사망 Clip에서 애니메이션하는 좌우 떨림 Float Parameter입니다. 값은 월드 거리입니다.")]
    [SerializeField] private string deathShakeParameter = "DeathShakeX";
    [Tooltip("사망 모션의 실제 시작 시 각 지점에서 한 번씩 생성할 스파크입니다.")]
    [SerializeField] private Boss1DeathSparkEmitter deathSparks;
    [Tooltip("사망 모션이 정상적으로 끝난 후 실행합니다. 중단되면 실행하지 않습니다.")]
    [SerializeField] private UnityEvent onDeathAnimationCompleted = new UnityEvent();

    private bool[] previousRendererStates;
    private bool deathMotionActive;
    private Vector3 deathStartPosition;
    private Vector3 deathEndPosition;
    private Boss1Controller deathOwner;
    private bool groggyRecoveryRequested;

    public bool IsGroggy { get; private set; }

    private void LateUpdate()
    {
        if (!deathMotionActive || animator == null || !animator.isActiveAndEnabled) return;
        ApplyDeathPose(animator.GetFloat(deathSinkParameter), animator.GetFloat(deathShakeParameter));
    }

    public void SetGroggy(bool groggy)
    {
        if (!groggy) groggyEffect?.Stop();
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(groggyParameter, groggy);
    }

    public void NotifyGroggyEntered()
    {
        IsGroggy = true;
        groggyRecoveryRequested = false;
        GetComponentInParent<Boss1Audio>()?.PlayTurnOff();
        groggyEffect?.Play();
        if (previousRendererStates != null || groggyHiddenRenderers == null) return;

        previousRendererStates = new bool[groggyHiddenRenderers.Length];
        for (int i = 0; i < groggyHiddenRenderers.Length; i++)
        {
            SpriteRenderer renderer = groggyHiddenRenderers[i];
            if (renderer == null) continue;
            previousRendererStates[i] = renderer.enabled;
            renderer.enabled = false;
        }
    }

    public void NotifyGroggyExited()
    {
        Boss1Controller owner = GetComponentInParent<Boss1Controller>();
        if (IsGroggy)
        {
            Boss1Audio audio = GetComponentInParent<Boss1Audio>();
            if (groggyRecoveryRequested && isActiveAndEnabled && owner != null && !owner.IsDead
                && owner.IsExecutingPattern)
                audio?.PlayTurnOn();
            else
                audio?.StopMachinery();
        }
        groggyRecoveryRequested = false;
        IsGroggy = false;
        groggyEffect?.Stop();
        if (previousRendererStates == null) return;

        for (int i = 0; i < previousRendererStates.Length; i++)
        {
            if (groggyHiddenRenderers[i] != null)
                groggyHiddenRenderers[i].enabled = previousRendererStates[i];
        }
        previousRendererStates = null;
    }

    public void RequestGroggyRecovery() => groggyRecoveryRequested = true;

    public void NotifyDeathEntered()
    {
        NotifyGroggyExited();
        deathStartPosition = transform.position;
        deathEndPosition = deathStartPosition;
        if (deathRestAnchor != null)
            deathEndPosition.y = Mathf.Min(deathStartPosition.y, deathRestAnchor.position.y);
        deathMotionActive = true;
        if (deathHiddenObjects != null)
            foreach (GameObject hiddenObject in deathHiddenObjects)
                if (hiddenObject != null) hiddenObject.SetActive(false);

        deathOwner = GetComponentInParent<Boss1Controller>();
        if (deathOwner != null)
        {
            deathOwner.LeftHand?.BeginDeath();
            deathOwner.RightHand?.BeginDeath();
        }
        deathSparks?.Play();
    }

    public void NotifyDeathCompleted()
    {
        if (deathMotionActive)
        {
            ApplyDeathPose(1f, 0f);
            deathMotionActive = false;
        }
        onDeathAnimationCompleted.Invoke();
    }

    public void NotifyDeathExited()
    {
        deathMotionActive = false;
    }

    private void ApplyDeathPose(float progress, float shake)
    {
        Vector3 position = Vector3.Lerp(deathStartPosition, deathEndPosition, Mathf.Clamp01(progress));
        position.x += shake;
        transform.position = position;
        if (deathOwner != null && deathHandDespawnAnchor != null)
        {
            deathOwner.LeftHand?.ApplyDeathPose(progress, deathHandDespawnAnchor.position.y);
            deathOwner.RightHand?.ApplyDeathPose(progress, deathHandDespawnAnchor.position.y);
        }
    }

    private void OnDisable()
    {
        NotifyDeathExited();
        SetGroggy(false);
        NotifyGroggyExited();
    }
}
