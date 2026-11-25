using UnityEngine;

public enum InteractionInputType
{
    Interact,
    PortalUse
}

public interface IInteractable
{
    Transform InteractionTransform { get; }
    bool CanInteract { get; }
    InteractionInputType InputType { get; }

    void SetInteractionFocus(bool focused);
    void Interact(Player player);
}
