using System.Collections;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MenuFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void TransitionBlocksInputUntilCompleteAndCancelsCallbacksOnDisable()
    {
        var root = new GameObject("Menu feedback test", typeof(RectTransform), typeof(CanvasGroup), typeof(MenuPanelTransition));
        try
        {
            var group = root.GetComponent<CanvasGroup>();
            var rect = (RectTransform)root.transform;
            rect.anchoredPosition = new Vector2(12, 24);
            var presentation = root.GetComponent<MenuPanelTransition>();
            var data = new SerializedObject(presentation);
            data.FindProperty("group").objectReferenceValue = group;
            data.FindProperty("content").objectReferenceValue = rect;
            data.ApplyModifiedPropertiesWithoutUndo();
            presentation.Show();
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Transition(presentation).Complete(true);
            Assert.That(group.interactable, Is.True);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(12, 24)));

            int completed = 0;
            presentation.Hide(() => completed++);
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.True);
            Transition(presentation).Complete(true);
            Assert.That(completed, Is.EqualTo(1));
            presentation.Show();
            presentation.Hide(() => completed++);
            typeof(MenuPanelTransition).GetMethod("OnDisable", PrivateInstance).Invoke(presentation, null);
            Assert.That(presentation.IsTransitioning, Is.False);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(12, 24)));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void UpgradeFeedbackCleansUpOnClose()
    {
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Canvas.prefab");
        try
        {
            var view = root.GetComponentInChildren<UpgradeShopView>(true);
            IndustrialUISetup.ApplyWorkshop(view.transform);
            view.PlayHealthPurchaseFeedback();
            Assert.That(typeof(UpgradeShopView).GetField("healthFeedback", PrivateInstance).GetValue(view), Is.Not.Null);
            typeof(UpgradeShopView).GetMethod("OnDisable", PrivateInstance).Invoke(view, null);
            Assert.That(typeof(UpgradeShopView).GetField("healthFeedback", PrivateInstance).GetValue(view), Is.Null);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Sequence Transition(MenuPanelTransition presentation) =>
        (Sequence)typeof(MenuPanelTransition).GetField("transition", PrivateInstance).GetValue(presentation);

    [UnityTest]
    public IEnumerator InteractionGuideReversesHideWithoutScalingOrRunningOldHideCallback()
    {
        yield return new EnterPlayMode();
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects()) sceneRoot.SetActive(false);
        PropertyInfo showInteractionKeys = typeof(GameSettings).GetProperty(nameof(GameSettings.ShowInteractionKeys));
        bool originalShowInteractionKeys = GameSettings.ShowInteractionKeys;
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/InteractionKeyGuide.prefab"));
        try
        {
            showInteractionKeys.SetValue(null, true);
            var guide = root.GetComponent<InteractionKeyGuide>();
            var data = new SerializedObject(guide);
            var content = (RectTransform)data.FindProperty("visualRoot").objectReferenceValue;
            var group = (CanvasGroup)data.FindProperty("presentation").objectReferenceValue;
            Vector2 rest = content.anchoredPosition;
            Vector3 scale = root.transform.localScale;
            guide.Show();
            Assert.That(group.alpha, Is.Zero);
            Assert.That(content.anchoredPosition.y, Is.LessThan(rest.y));
            GuideTransition(guide).Complete(true);
            Assert.That(content.anchoredPosition, Is.EqualTo(rest));
            guide.Hide();
            Sequence oldHide = GuideTransition(guide);
            guide.Show();
            Assert.That(oldHide.IsActive(), Is.False);
            GuideTransition(guide).Complete(true);
            oldHide.Complete(true);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(root.transform.localScale, Is.EqualTo(scale));
            Assert.That(content.anchoredPosition, Is.EqualTo(rest));
            guide.Hide();
            GuideTransition(guide).Complete(true);
            Assert.That(root.activeSelf, Is.False);
        }
        finally
        {
            showInteractionKeys.SetValue(null, originalShowInteractionKeys);
            Object.DestroyImmediate(root);
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void TitleMenuReturnsToMainAfterModalTransitions()
    {
        GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/TitleScreen.prefab"));
        var title = root.GetComponent<TitleMenu>();
        try
        {
            if (!Application.isPlaying)
                typeof(TitleMenu).GetMethod("Awake", PrivateInstance).Invoke(title, null);
            var main = root.transform.Find("MainMenu").GetComponent<MenuPanelTransition>();
            var modal = root.transform.Find("Modal").GetComponent<MenuPanelTransition>();
            title.ShowSettings();
            Transition(main).Complete(true);
            Transition(modal).Complete(true);
            title.ShowControls();
            Transition(modal).Complete(true);
            title.Back();
            Transition(modal).Complete(true);
            Transition(main).Complete(true);
            Assert.That(main.gameObject.activeSelf, Is.True);
            Assert.That(modal.gameObject.activeSelf, Is.False);
            Assert.That(main.GetComponent<CanvasGroup>().interactable, Is.True);
        }
        finally
        {
            if (!Application.isPlaying)
                typeof(TitleMenu).GetMethod("OnDestroy", PrivateInstance).Invoke(title, null);
            Object.DestroyImmediate(root);
        }
    }

    private static Sequence GuideTransition(InteractionKeyGuide guide) =>
        (Sequence)typeof(InteractionKeyGuide).GetField("transitionTween", PrivateInstance).GetValue(guide);

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
