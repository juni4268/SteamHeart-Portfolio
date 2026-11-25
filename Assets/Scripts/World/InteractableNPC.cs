using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class InteractableNPC : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("체크하면 이 NPC는 한 번만 상호작용할 수 있습니다. Scene을 다시 불러오면 초기화됩니다.")]
    [SerializeField] private bool interactOnce;
    [SerializeField] private InteractionKeyGuide interactionKeyGuide;
    [SerializeField] private SpriteInteractionOutline interactionOutline;
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    [Header("Healing")]
    [SerializeField] private bool healOnInteract;
    [SerializeField, Min(0)] private int healAmount = 4;

    [Header("Interaction Prefab")]
    [SerializeField] private GameObject interactionPrefab;
    [Tooltip("생성되는 프리팹의 색상 회전입니다. 0이면 원본 색상을 유지하며 아웃라인 색상에는 영향을 주지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float prefabHueShift;
    [SerializeField] private Vector3 prefabSpawnOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField, Min(0f)] private float prefabRiseHeight = 0.5f;
    [SerializeField, Min(0f)] private float prefabRiseDuration = 0.25f;
    [SerializeField, Min(0f)] private float prefabPauseDuration = 0.08f;
    [SerializeField, Min(0f)] private float prefabFlightDuration = 0.35f;
    [SerializeField, Min(0f)] private float prefabArcHeight = 0.3f;
    [SerializeField, Min(0f)] private float prefabShrinkDuration = 0.08f;
    [SerializeField] private ParticleSystem acquisitionEffectPrefab;

    [Header("Core Handover")]
    [SerializeField] private GameObject handoverPrefab;
    [SerializeField] private Vector3 handoverStartOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 handoverEndOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Core가 초당 이동하는 거리입니다.")]
    [SerializeField, Min(0.1f)] private float handoverSpeed = 4f;
    [SerializeField] private AudioSource handoverAudio;

    private bool hasInteracted;
    private bool handingOver;
    private Coroutine handoverRoutine;
    private GameObject handoverInstance;

    public UnityEvent OnInteract => onInteract;
    public Transform InteractionTransform => transform;
    public bool CanInteract => isActiveAndEnabled && !handingOver && (!interactOnce || !hasInteracted);
    public InteractionInputType InputType => InteractionInputType.Interact;

    private void Reset()
    {
        BoxCollider2D interactionArea = GetComponent<BoxCollider2D>();
        interactionArea.isTrigger = true;
        interactionArea.size = new Vector2(2f, 2f);
    }

    private void OnValidate()
    {
        BoxCollider2D interactionArea = GetComponent<BoxCollider2D>();
        if (interactionArea != null)
        {
            interactionArea.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        CancelHandover();
        interactionOutline?.SetFocused(false);
        interactionKeyGuide?.Hide();
    }

    public void SetInteractionFocus(bool focused)
    {
        focused = focused && CanInteract;
        interactionOutline?.SetFocused(focused);
        if (focused)
        {
            interactionKeyGuide?.Show();
            return;
        }

        interactionKeyGuide?.Hide();
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        hasInteracted = true;
        if (handoverPrefab != null && player != null)
        {
            handingOver = true;
            SetInteractionFocus(false);
            handoverRoutine = StartCoroutine(HandoverThenInteract(player));
            return;
        }

        if (interactOnce) SetInteractionFocus(false);
        CompleteInteraction(player, spawnPickup: true);
    }

    private IEnumerator HandoverThenInteract(Player player)
    {
        Vector3 start = player.transform.position + handoverStartOffset;
        handoverInstance = Instantiate(handoverPrefab, start, handoverPrefab.transform.rotation);
        while (true)
        {
            if (player == null || !player.isActiveAndEnabled || player.health.IsDead)
            {
                CancelHandover();
                yield break;
            }

            Vector3 destination = transform.position + handoverEndOffset;
            handoverInstance.transform.position = Vector3.MoveTowards(
                handoverInstance.transform.position, destination, handoverSpeed * Time.deltaTime);
            if (handoverInstance.transform.position == destination) break;
            yield return null;
        }

        Destroy(handoverInstance);
        handoverInstance = null;
        if (handoverAudio != null && handoverAudio.clip != null)
        {
            for (int repeat = 0; repeat < 2; repeat++)
            {
                handoverAudio.Play();
                do
                {
                    yield return null;
                    if (player == null || !player.isActiveAndEnabled || player.health.IsDead)
                    {
                        CancelHandover();
                        yield break;
                    }
                } while (handoverAudio.isPlaying || AudioListener.pause);
            }
        }

        NPCInteractionPickup pickup = SpawnInteractionPickup(player);
        if (pickup != null)
        {
            handoverInstance = pickup.gameObject;
            bool collected = false;
            pickup.Collected += () => collected = true;
            while (!collected)
            {
                if (pickup == null || player == null || !player.isActiveAndEnabled || player.health.IsDead)
                {
                    CancelHandover();
                    yield break;
                }
                yield return null;
            }
            handoverInstance = null;
        }

        if (player == null || !player.isActiveAndEnabled || player.health.IsDead)
        {
            CancelHandover();
            yield break;
        }
        handoverRoutine = null;
        handingOver = false;
        CompleteInteraction(player, spawnPickup: false);
    }

    private void CancelHandover()
    {
        if (!handingOver) return;
        if (handoverRoutine != null) StopCoroutine(handoverRoutine);
        handoverRoutine = null;
        if (handoverInstance != null) Destroy(handoverInstance);
        handoverInstance = null;
        if (handoverAudio != null) handoverAudio.Stop();
        handingOver = false;
        hasInteracted = false;
    }

    private void CompleteInteraction(Player player, bool spawnPickup)
    {
        if (healOnInteract && player != null)
        {
            player.health?.Heal(healAmount);
        }

        if (spawnPickup) SpawnInteractionPickup(player);
        onInteract.Invoke();
    }

    private NPCInteractionPickup SpawnInteractionPickup(Player player)
    {
        if (interactionPrefab == null || player == null) return null;
        GameObject instance = Instantiate(interactionPrefab,
            transform.position + prefabSpawnOffset, interactionPrefab.transform.rotation);
        NPCInteractionPickup pickup = instance.AddComponent<NPCInteractionPickup>();
        pickup.Initialize(player.transform, prefabRiseHeight, prefabRiseDuration,
            prefabPauseDuration, prefabFlightDuration, prefabArcHeight, prefabShrinkDuration,
            acquisitionEffectPrefab, prefabHueShift);
        return pickup;
    }
}
