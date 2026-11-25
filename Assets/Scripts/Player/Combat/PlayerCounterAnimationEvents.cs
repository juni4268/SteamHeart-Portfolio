using UnityEngine;

// Attach to the Animator GameObject when it is a child of the Player.
[RequireComponent(typeof(Animator))]
public class PlayerCounterAnimationEvents : MonoBehaviour
{
    private PlayerCounter skill;

    private void Awake() => skill = GetComponentInParent<PlayerCounter>();

    public void BeginCounterWindow() => skill?.BeginCounterWindow();
    public void EndCounterWindow() => skill?.EndCounterWindow();
    public void ApplyCounterDamage() => skill?.ApplyCounterDamage();
    public void FinishCounter() => skill?.FinishCounter();

    private void OnDisable()
    {
        if (skill != null) skill.Cancel();
    }
}
