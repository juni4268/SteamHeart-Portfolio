using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class LowerTownIntro : MonoBehaviour
{
    [Header("Actors")]
    [SerializeField] private Player player;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private RuntimeAnimatorController cutsceneController;
    [SerializeField] private string walkingParameter = "Walking";
    [SerializeField] private SpriteRenderer merchant;
    [SerializeField] private BoxCollider2D merchantArea;
    [SerializeField] private Transform playerHead;
    [SerializeField] private Transform merchantHead;
    [SerializeField] private Behaviour[] suspendedBehaviours;

    [Header("Staging")]
    [SerializeField] private Transform conversationPoint;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private Transform directionPoint;
    [SerializeField, Min(0.1f)] private float walkSpeed = 1.2f;
    [SerializeField] private CinemachineCamera followCamera;
    [SerializeField] private Transform cameraAnchor;
    [SerializeField] private Transform conversationShot;
    [SerializeField, Min(0.01f)] private float cameraSmoothTime = 0.5f;

    [Header("Presentation")]
    [SerializeField] private GameObject hud;
    [SerializeField] private Canvas cutsceneUI;
    [SerializeField] private RectTransform speechBubble;
    [SerializeField] private Text speech;
    [SerializeField] private RawImage coreIcon;
    [SerializeField] private GameObject brokenCoreIcon;
    [SerializeField] private CanvasGroup coreGlow;
    [SerializeField] private CanvasGroup directionHint;
    [SerializeField] private Image directionImage;
    [SerializeField] private Animator directionAnimator;
    [SerializeField] private Button skipButton;
    [SerializeField, Min(0.1f)] private float iconHold = 1.2f;
    [SerializeField, Min(0f)] private float responsePause = 0.25f;
    [SerializeField] private SceneReference destinationScene = new SceneReference();
    [SerializeField, Min(0f)] private float fadeDuration = 0.8f;

    private static bool playbackRequested;
    private bool[] previousBehaviourStates;
    private bool previousInputEnabled;
    private bool previousHudActive;
    private RuntimeAnimatorController previousController;
    private Transform previousFollow;
    private PlayerAudio playerAudio;
    private Transform speaker;
    private Vector3 cameraVelocity;
    private float cameraOffset;
    private bool conversationStarted;
    private bool finishing;

    public bool IsPlaying { get; private set; }
    public static bool ShouldAutoSkip => GameSettings.AutoSkipWatchedCutscenes
        && PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.LowerTownIntro);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => playbackRequested = false;

    public static void RequestPlayback() => playbackRequested = true;
    public static void CancelPlaybackRequest() => playbackRequested = false;

    private void Awake()
    {
        if (cutsceneUI != null) cutsceneUI.gameObject.SetActive(false);
        if (!playbackRequested) return;
        playbackRequested = false;
        if (player == null || playerAnimator == null || cutsceneController == null || merchant == null
            || merchantArea == null || playerHead == null || merchantHead == null || conversationPoint == null
            || exitPoint == null || directionPoint == null || followCamera == null || cameraAnchor == null || conversationShot == null
            || hud == null || cutsceneUI == null || speechBubble == null || speech == null
            || coreIcon == null || brokenCoreIcon == null || coreGlow == null || directionHint == null || directionImage == null
            || directionAnimator == null
            || skipButton == null || destinationScene == null
            || !destinationScene.IsAssigned || !Application.CanStreamedLevelBeLoaded(destinationScene.Path))
        {
            Debug.LogError("LowerTownIntro needs its actors, staging, UI, and playable destination scene.", this);
            return;
        }

        IsPlaying = true;
        player.CancelCombatActions();
        previousInputEnabled = player.input.Player.enabled;
        player.input.Player.Disable();
        previousBehaviourStates = new bool[suspendedBehaviours.Length];
        for (int i = 0; i < suspendedBehaviours.Length; i++)
        {
            previousBehaviourStates[i] = suspendedBehaviours[i].enabled;
            suspendedBehaviours[i].enabled = false;
        }
        player.motor.Stop();
        playerAudio = player.GetComponent<PlayerAudio>();
        previousController = playerAnimator.runtimeAnimatorController;
        playerAnimator.runtimeAnimatorController = cutsceneController;
        playerAnimator.SetBool(walkingParameter, false);
        previousFollow = followCamera.Follow;
        cameraAnchor.position = previousFollow.position;
        cameraOffset = previousFollow.position.x - player.transform.position.x;
        followCamera.Follow = cameraAnchor;
        merchant.flipX = false;
        previousHudActive = hud.activeSelf;
        hud.SetActive(false);
        HideSpeech();
        HideDirection();
        cutsceneUI.gameObject.SetActive(true);
        skipButton.onClick.AddListener(Skip);
        LastInputDevice.Changed += RefreshSkipLabel;
        RefreshSkipLabel();
    }

    private IEnumerator Start()
    {
        if (!IsPlaying) yield break;
        while (SceneTransitionOverlay.IsTransitioning) yield return null;
        if (ShouldAutoSkip)
        {
            Skip();
            yield break;
        }
        yield return new WaitForSeconds(0.4f);
        yield return WalkTo(conversationPoint.position.x);
        conversationStarted = true;
        yield return new WaitForSeconds(0.4f);
        yield return ShowFaultyCore();
        yield return ShowCoreQuestion();
        speaker = merchantHead;
        speech.rectTransform.anchoredPosition = new Vector2(0f, 21f);
        speech.text = "!";
        speechBubble.gameObject.SetActive(true);
        speechBubble.localScale = Vector3.one * 0.55f;
        speechBubble.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
        yield return new WaitForSeconds(0.8f);
        HideSpeech();
        yield return new WaitForSeconds(responsePause);
        merchant.flipX = true;
        yield return ShowDirection();
        yield return new WaitForSeconds(0.15f);
        yield return WalkTo(exitPoint.position.x);
        if (merchantArea.bounds.Intersects(player.GetComponent<Collider2D>().bounds))
        {
            Debug.LogError("Place the intro exit point completely outside the Merchant area.", this);
            Skip();
            yield break;
        }
        PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.LowerTownIntro);
        Finish(fadeDuration);
    }

    private IEnumerator WalkTo(float targetX)
    {
        Vector3 scale = player.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(targetX - player.transform.position.x);
        player.transform.localScale = scale;
        playerAnimator.SetBool(walkingParameter, true);
        float deadline = Time.time + Mathf.Abs(targetX - player.transform.position.x) / walkSpeed + 3f;
        while (Mathf.Abs(targetX - player.transform.position.x) > 0.025f)
        {
            if (Time.time >= deadline)
            {
                Debug.LogError("The intro player's walk is blocked. Check the staging points and ground.", this);
                Skip();
                yield break;
            }
            float distance = targetX - player.transform.position.x;
            player.motor.SetVelocityX(Mathf.Sign(distance) * Mathf.Min(walkSpeed, Mathf.Abs(distance) / Time.fixedDeltaTime));
            yield return new WaitForFixedUpdate();
        }
        player.motor.SetVelocityX(0f);
        playerAnimator.SetBool(walkingParameter, false);
        playerAudio.StopLocomotion();
    }

    private IEnumerator ShowFaultyCore()
    {
        speaker = playerHead;
        speechBubble.gameObject.SetActive(true);
        speechBubble.localScale = Vector3.one;
        speech.rectTransform.anchoredPosition = new Vector2(13f, 21f);
        speech.text = string.Empty;
        brokenCoreIcon.SetActive(false);
        coreIcon.enabled = true;
        coreIcon.color = Color.white;
        yield return new WaitForSeconds(0.35f);
        for (int flicker = 0; flicker < 2; flicker++)
        {
            coreIcon.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            yield return new WaitForSeconds(0.2f);
            coreIcon.color = Color.white;
            yield return new WaitForSeconds(0.2f);
        }
        yield return coreIcon.DOColor(new Color(0.18f, 0.18f, 0.18f, 1f), 0.4f)
            .SetLink(gameObject).WaitForCompletion();
        coreIcon.enabled = false;
        brokenCoreIcon.SetActive(true);
        yield return new WaitForSeconds(1f);
        HideSpeech();
        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator ShowCoreQuestion()
    {
        speaker = playerHead;
        speechBubble.gameObject.SetActive(true);
        speechBubble.localScale = Vector3.one * 0.85f;
        speechBubble.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(gameObject);
        speech.rectTransform.anchoredPosition = new Vector2(13f, 21f);
        speech.text = "?";
        coreIcon.enabled = true;
        coreIcon.color = Color.white;
        coreGlow.gameObject.SetActive(true);
        coreGlow.alpha = 0f;
        coreGlow.transform.localScale = Vector3.one * 0.85f;
        DOTween.Sequence()
            .Append(coreGlow.DOFade(1f, iconHold * 0.25f))
            .Append(coreGlow.DOFade(0.25f, iconHold * 0.75f))
            .Insert(0f, coreGlow.transform.DOScale(1.15f, iconHold).SetEase(Ease.OutSine))
            .SetTarget(coreGlow).SetLink(gameObject);
        yield return new WaitForSeconds(iconHold);
        HideSpeech();
        yield return new WaitForSeconds(responsePause);
    }

    private void HideSpeech()
    {
        speech.text = string.Empty;
        speechBubble.DOKill();
        coreIcon.DOKill();
        coreIcon.enabled = false;
        coreIcon.color = Color.white;
        brokenCoreIcon.SetActive(false);
        coreGlow.DOKill();
        coreGlow.alpha = 0f;
        coreGlow.transform.localScale = Vector3.one;
        coreGlow.gameObject.SetActive(false);
        speechBubble.gameObject.SetActive(false);
        speechBubble.localScale = Vector3.one;
    }

    private IEnumerator ShowDirection()
    {
        directionHint.alpha = 0f;
        directionImage.enabled = directionAnimator.runtimeAnimatorController != null;
        directionHint.gameObject.SetActive(true);
        yield return directionHint.DOFade(1f, 0.12f).SetLink(gameObject).WaitForCompletion();
        yield return new WaitForSeconds(0.65f);
        yield return directionHint.DOFade(0f, 0.18f).SetLink(gameObject).WaitForCompletion();
        HideDirection();
    }

    private void HideDirection()
    {
        directionHint.DOKill();
        directionHint.alpha = 0f;
        directionHint.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (IsPlaying && !finishing && ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            || PlayerKeyBindings.ControlForPath(PlayerKeyBindings.GamepadCancelPath)?.wasPressedThisFrame == true))
            Skip();
    }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        Vector3 target = conversationShot.position;
        if (!conversationStarted) target.x = Mathf.Min(player.transform.position.x + cameraOffset, target.x);
        cameraAnchor.position = Vector3.SmoothDamp(cameraAnchor.position, target, ref cameraVelocity, cameraSmoothTime);
        if (speaker != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)cutsceneUI.transform,
                Camera.main.WorldToScreenPoint(speaker.position), null, out Vector2 position);
            speechBubble.anchoredPosition = position;
        }
        if (directionHint.gameObject.activeSelf)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)cutsceneUI.transform,
                Camera.main.WorldToScreenPoint(directionPoint.position), null, out Vector2 position);
            ((RectTransform)directionHint.transform).anchoredPosition = position;
        }
    }

    public void Skip() => Finish(0.15f);

    private void RefreshSkipLabel()
    {
        Text label = skipButton.GetComponentInChildren<Text>(true);
        if (label == null) return;
        string key = LastInputDevice.IsGamepad
            ? PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadCancelPath) : "Esc";
        label.text = $"건너뛰기 [{key}]";
    }

    private void Finish(float fadeOut)
    {
        if (!IsPlaying || finishing) return;
        finishing = true;
        StopAllCoroutines();
        player.motor.SetVelocityX(0f);
        playerAnimator.SetBool(walkingParameter, false);
        playerAudio.StopLocomotion();
        HideSpeech();
        HideDirection();
        skipButton.interactable = false;
        StartCoroutine(LeaveScene(fadeOut));
    }

    private IEnumerator LeaveScene(float fadeOut)
    {
        while (SceneTransitionOverlay.IsTransitioning) yield return null;
        if (!SceneTransitionOverlay.TryLoadScene(destinationScene.Path, fadeOut, fadeDuration, Color.black, pauseAudio: false))
        {
            finishing = false;
            skipButton.interactable = true;
        }
    }

    private void OnDisable()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        LastInputDevice.Changed -= RefreshSkipLabel;
        skipButton.onClick.RemoveListener(Skip);
        HideSpeech();
        HideDirection();
        if (playerAnimator != null) playerAnimator.runtimeAnimatorController = previousController;
        if (followCamera != null) followCamera.Follow = previousFollow;
        if (hud != null) hud.SetActive(previousHudActive);
        if (player != null)
        {
            player.motor.SetVelocityX(0f);
            playerAudio.StopLocomotion();
            for (int i = 0; i < suspendedBehaviours.Length; i++)
                if (suspendedBehaviours[i] != null) suspendedBehaviours[i].enabled = previousBehaviourStates[i];
            if (previousInputEnabled && player.isActiveAndEnabled && !player.health.IsDead) player.input.Player.Enable();
        }
        if (cutsceneUI != null) cutsceneUI.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate() => destinationScene?.Validate();
#endif
}
