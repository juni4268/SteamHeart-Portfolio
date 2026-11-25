using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MapPortal : MonoBehaviour, IInteractable
{
    [Header("Destination")]
    [SerializeField] private SceneReference destinationScene = new SceneReference();

    [Header("Interaction Guide")]
    [SerializeField] private InteractionKeyGuide interactionKeyGuide;
    [SerializeField] private SpriteInteractionOutline interactionOutline;

    [Header("Unlock Conditions")]
    [Tooltip("Automatically find all enemies placed in this portal's Scene, including inactive enemies, when it awakens. They must all be defeated before use.")]
    [SerializeField] private bool requiresAllEnemiesDefeated;
    [Tooltip("Call Unlock from the designated NPC interaction to enable this portal.")]
    [SerializeField] private bool requiresUnlock;
    private bool unlocked;

    [Header("Scene Transition")]
    [SerializeField] private VideoClip transitionVideo;
    [SerializeField] private CutsceneId transitionCutscene;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;
    [SerializeField] private Color fadeColor = Color.black;

    private bool isLoading;
    private EnemyHealth[] sceneEnemies = System.Array.Empty<EnemyHealth>();

    public Transform InteractionTransform => transform;
    public bool CanInteract =>
        isActiveAndEnabled &&
        (!requiresUnlock || unlocked) &&
        AreSceneEnemiesDefeated() &&
        !isLoading &&
        !SceneTransitionOverlay.IsTransitioning &&
        !VideoCutsceneOverlay.IsPlaying;
    public InteractionInputType InputType => InteractionInputType.PortalUse;

    private void Awake()
    {
        interactionKeyGuide?.SetInputType(InteractionInputType.PortalUse);
        if (requiresAllEnemiesDefeated) CollectSceneEnemies();
    }

    private void CollectSceneEnemies()
    {
        var enemies = new List<EnemyHealth>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            enemies.AddRange(root.GetComponentsInChildren<EnemyHealth>(true));
        sceneEnemies = enemies.ToArray();
    }

    public void Unlock() => unlocked = true;

    private bool AreSceneEnemiesDefeated()
    {
        if (!requiresAllEnemiesDefeated) return true;
        foreach (EnemyHealth enemy in sceneEnemies)
            if (enemy != null && !enemy.IsDead) return false;
        return true;
    }

    private void Reset()
    {
        BoxCollider2D portalArea = GetComponent<BoxCollider2D>();
        portalArea.isTrigger = true;
        portalArea.size = new Vector2(1.5f, 2f);
    }

    private void OnValidate()
    {
        BoxCollider2D portalArea = GetComponent<BoxCollider2D>();
        if (portalArea != null)
        {
            portalArea.isTrigger = true;
        }

#if UNITY_EDITOR
        destinationScene?.Validate();
#endif
    }

    private void OnDisable()
    {
        interactionOutline?.SetFocused(false);
        interactionKeyGuide?.Hide();
        isLoading = false;
    }

    public void SetInteractionFocus(bool focused)
    {
        interactionOutline?.SetFocused(focused && CanInteract);
        if (focused)
        {
            interactionKeyGuide?.Show();
            return;
        }

        interactionKeyGuide?.Hide();
    }

    public void Interact(Player player)
    {
        if (CanInteract)
        {
            LoadDestination();
        }
    }

    private void LoadDestination()
    {
        if (destinationScene == null || !destinationScene.IsAssigned)
        {
            Debug.LogError($"{name} has no destination scene assigned.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destinationScene.Path))
        {
            Debug.LogError(
                $"Cannot load scene '{destinationScene.Path}'. Add it to Build Settings first.",
                this);
            return;
        }

        if (transitionVideo != null)
        {
            isLoading = SceneTransitionOverlay.TryLoadSceneAfterVideo(destinationScene.Path, transitionVideo,
                fadeInDuration, fadeOutDuration, fadeInDuration, fadeColor, cutsceneId: transitionCutscene);
            return;
        }

        BeginSceneTransition();
    }

    private void BeginSceneTransition()
    {
        isLoading = SceneTransitionOverlay.TryLoadScene(
            destinationScene.Path,
            fadeOutDuration,
            fadeInDuration,
            fadeColor);
    }
}
