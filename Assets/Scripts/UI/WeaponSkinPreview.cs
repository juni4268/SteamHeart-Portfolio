using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

// A visual-only actor: no gameplay components, Animation Events, or Animator state paths.
public sealed class WeaponSkinPreview : MonoBehaviour
{
    [SerializeField] private Image image;
    private GameObject actor;
    private SpriteRenderer spriteRenderer;
    private PlayableGraph graph;

    public void Show(AnimationClip idle)
    {
        Release();
        image.sprite = null;
        image.enabled = idle != null;
        if (idle == null) return;
        actor = new GameObject("Skin Idle Preview", typeof(SpriteRenderer), typeof(Animator));
        actor.hideFlags = HideFlags.HideAndDontSave;
        spriteRenderer = actor.GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
        Animator animator = actor.GetComponent<Animator>();
        animator.fireEvents = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("Weapon Skin Idle");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        AnimationClipPlayable clip = AnimationClipPlayable.Create(graph, idle);
        clip.SetApplyFootIK(false);
        AnimationPlayableOutput.Create(graph, "Sprite", animator).SetSourcePlayable(clip);
        graph.Play();
        graph.Evaluate(0f);
        image.sprite = spriteRenderer.sprite;
    }

    private void Update()
    {
        if (!graph.IsValid()) return;
        graph.Evaluate(Time.unscaledDeltaTime);
        image.sprite = spriteRenderer.sprite;
    }

    private void OnDisable() => Release();

    private void Release()
    {
        if (graph.IsValid()) graph.Destroy();
        if (actor != null)
        {
            if (Application.isPlaying) Destroy(actor);
            else DestroyImmediate(actor);
        }
        actor = null;
        spriteRenderer = null;
    }
}
