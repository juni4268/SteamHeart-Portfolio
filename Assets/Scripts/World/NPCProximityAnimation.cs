using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class NPCProximityAnimation : MonoBehaviour
{
    [SerializeField] private Animator npcAnimator;
    [Tooltip("플레이어 진입 시 설정할 Animator Trigger Parameter입니다.")]
    [SerializeField] private string reactionParameter = "PlayerEntered";

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();

    private void Reset()
    {
        BoxCollider2D area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(3f, 2f);
    }

    private void OnValidate()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (npcAnimator == null || string.IsNullOrWhiteSpace(reactionParameter))
        {
            Debug.LogError("NPCProximityAnimation needs an Animator and a reaction Trigger Parameter.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (npcAnimator != null && npcAnimator.runtimeAnimatorController != null
            && !string.IsNullOrWhiteSpace(reactionParameter))
        {
            npcAnimator.ResetTrigger(reactionParameter);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled || other.GetComponentInParent<Player>() == null) return;

        bool wasOutside = playerColliders.Count == 0;
        if (playerColliders.Add(other) && wasOutside)
        {
            npcAnimator.SetTrigger(reactionParameter);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (playerColliders.Remove(other) && playerColliders.Count == 0)
        {
            npcAnimator.ResetTrigger(reactionParameter);
        }
    }
}
