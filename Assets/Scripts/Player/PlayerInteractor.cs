using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
public sealed class PlayerInteractor : MonoBehaviour
{
    private readonly Dictionary<Collider2D, IInteractable> candidates =
        new Dictionary<Collider2D, IInteractable>();

    private Player player;
    private IInteractable focusedInteractable;
    private bool isSubscribed;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void OnEnable()
    {
        GameSettings.Changed += RefreshGuideVisibility;
        SubscribeToInput();
    }

    private void Start()
    {
        SubscribeToInput();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= RefreshGuideVisibility;
        UnsubscribeFromInput();

        SetFocusedInteractable(null);
        candidates.Clear();
    }

    private void Update()
    {
        RefreshFocusedInteractable();
    }

    private void RefreshGuideVisibility() => focusedInteractable?.SetInteractionFocus(true);

    private void OnTriggerEnter2D(Collider2D other)
    {
        IInteractable interactable = FindInteractable(other);
        if (interactable == null)
        {
            return;
        }

        candidates[other] = interactable;
        RefreshFocusedInteractable();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!candidates.Remove(other))
        {
            return;
        }

        RefreshFocusedInteractable();
    }

    private void OnInteractStarted(InputAction.CallbackContext context)
    {
        TryInteract(InteractionInputType.Interact);
    }

    private void OnPortalUseStarted(InputAction.CallbackContext context)
    {
        TryInteract(InteractionInputType.PortalUse);
    }

    private void TryInteract(InteractionInputType inputType)
    {
        RefreshFocusedInteractable();

        if (IsAvailable(focusedInteractable) && focusedInteractable.InputType == inputType)
        {
            focusedInteractable.Interact(player);
        }
    }

    private void SubscribeToInput()
    {
        if (isSubscribed || player == null || player.input == null)
        {
            return;
        }

        player.input.Player.Interact.started += OnInteractStarted;
        player.input.Player.PortalUse.started += OnPortalUseStarted;
        isSubscribed = true;
    }

    private void UnsubscribeFromInput()
    {
        if (!isSubscribed || player == null || player.input == null)
        {
            isSubscribed = false;
            return;
        }

        player.input.Player.Interact.started -= OnInteractStarted;
        player.input.Player.PortalUse.started -= OnPortalUseStarted;
        isSubscribed = false;
    }

    private void RefreshFocusedInteractable()
    {
        IInteractable nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        foreach (KeyValuePair<Collider2D, IInteractable> candidate in candidates)
        {
            if (candidate.Key == null || !candidate.Key.enabled || !IsAvailable(candidate.Value))
            {
                continue;
            }

            float sqrDistance =
                (candidate.Value.InteractionTransform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearest = candidate.Value;
            nearestSqrDistance = sqrDistance;
        }

        SetFocusedInteractable(nearest);
    }

    private void SetFocusedInteractable(IInteractable interactable)
    {
        if (ReferenceEquals(focusedInteractable, interactable))
        {
            return;
        }

        if (IsAlive(focusedInteractable))
        {
            focusedInteractable.SetInteractionFocus(false);
        }

        focusedInteractable = interactable;

        if (IsAvailable(focusedInteractable))
        {
            focusedInteractable.SetInteractionFocus(true);
        }
    }

    private static IInteractable FindInteractable(Collider2D target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }

    private static bool IsAvailable(IInteractable interactable)
    {
        return IsAlive(interactable) && interactable.CanInteract;
    }

    private static bool IsAlive(IInteractable interactable)
    {
        return interactable is Object unityObject && unityObject != null;
    }
}
