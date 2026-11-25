using System.Globalization;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyAnimationEventReceiver))]
public sealed class EnemyAnimationEventReceiverEditor : Editor
{
    private EnemyMovementCurvePreview preview;

    private void OnEnable()
    {
        EditorApplication.update += UpdatePreview;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        Undo.undoRedoPerformed += UpdatePreview;
    }

    private void OnDisable()
    {
        StopPreview();
        EditorApplication.update -= UpdatePreview;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        Undo.undoRedoPerformed -= UpdatePreview;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Movement Curve Editing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Choose a frame in the Animation window, then drag the yellow Scene arrow to edit movement while preserving curve tangents. " +
            "The enemy returns to its original position when preview ends.", MessageType.Info);

        if (preview != null)
        {
            EditorGUILayout.LabelField("Frame", preview.Frame.ToString());
            EditorGUILayout.LabelField("Distance", preview.Distance.ToString("0.###"));
            if (preview.Frame == 0)
                EditorGUILayout.HelpBox("Frame 0 is the starting position. Choose a later frame to move the enemy.", MessageType.Info);
            if (GUILayout.Button("Stop Movement Preview")) StopPreview();
            return;
        }

        var receiver = (EnemyAnimationEventReceiver)target;
        AnimationWindow window = FindAnimationWindow(receiver);
        using (new EditorGUI.DisabledScope(window == null))
        {
            if (GUILayout.Button("Edit Movement in Scene"))
            {
                preview = EnemyMovementCurvePreview.Start(receiver, window);
                if (preview == null)
                    Debug.LogWarning("Select this enemy's Animator in the Animation window and enable Preview.", receiver);
            }
        }

        if (window == null)
            EditorGUILayout.HelpBox("Open an Animation Clip with a Movement Distance curve for this scene object or Prefab Mode object.", MessageType.Info);
    }

    private static AnimationWindow FindAnimationWindow(EnemyAnimationEventReceiver receiver)
    {
        if (EditorUtility.IsPersistent(receiver) || receiver.GetComponentInParent<EnemyFrameMovement>() == null)
            return null;

        foreach (AnimationWindow window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
        {
            if (window.canPreview && window.animationClip != null &&
                EnemyMovementCurvePreview.HasMovementCurve(receiver, window.animationClip))
                return window;
        }

        return null;
    }

    private void UpdatePreview()
    {
        if (preview == null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || !preview.Update())
        {
            StopPreview();
            return;
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void OnSceneGUI()
    {
        if (preview == null || !preview.Update()) return;

        Vector3 position = preview.Position;
        using (new Handles.DrawingScope(Color.yellow))
        {
            Handles.DrawDottedLine(preview.Origin, position, 4f);
            Handles.Label(position + Vector3.up * HandleUtility.GetHandleSize(position) * 0.15f,
                $"Frame {preview.Frame} | Distance {preview.Distance:0.###}");

            if (preview.Frame == 0 || preview.Playing) return;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(position, Vector3.right,
                HandleUtility.GetHandleSize(position) * 0.8f, Handles.ArrowHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                preview.RecordPosition(moved);
                Repaint();
            }
        }
    }

    private void StopPreview()
    {
        if (preview == null) return;
        preview.Dispose();
        preview = null;
        SceneView.RepaintAll();
        Repaint();
    }
}

internal sealed class EnemyMovementCurvePreview : System.IDisposable
{
    private const string DistanceProperty = "movementDistance";
    private const string PositionProperty = "m_LocalPosition.x";

    private readonly EnemyAnimationEventReceiver receiver;
    private readonly AnimationWindow window;
    private readonly AnimationClip clip;
    private readonly Transform root;
    private readonly EditorCurveBinding binding;
    private readonly float originalLocalX;
    private readonly float direction;
    private readonly bool toolsWereHidden;
    private bool disposed;

    public Vector3 Origin { get; }
    public Vector3 Position => root.position;
    public int Frame => window.frame;
    public bool Playing => window.playing;
    public float Distance => AnimationUtility.GetEditorCurve(clip, binding).Evaluate(window.time);

    private EnemyMovementCurvePreview(EnemyAnimationEventReceiver receiver, AnimationWindow window)
    {
        this.receiver = receiver;
        this.window = window;
        clip = window.animationClip;
        root = receiver.GetComponentInParent<EnemyFrameMovement>().transform;
        binding = GetBinding(receiver);
        Origin = root.position;
        originalLocalX = root.localPosition.x;
        direction = root.localScale.x < 0f ? -1f : 1f;
        toolsWereHidden = Tools.hidden;

        // Register the temporary offset with animation mode. Dispose also restores the root,
        // which is outside the Animator's own hierarchy.
        AnimationMode.AddPropertyModification(
            EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), PositionProperty),
            new PropertyModification
            {
                target = root,
                propertyPath = PositionProperty,
                value = originalLocalX.ToString("R", CultureInfo.InvariantCulture)
            }, true);
        Tools.hidden = true;
        Update();
    }

    public static EnemyMovementCurvePreview Start(EnemyAnimationEventReceiver receiver, AnimationWindow window)
    {
        if (Application.isPlaying || receiver == null || EditorUtility.IsPersistent(receiver) ||
            receiver.GetComponentInParent<EnemyFrameMovement>() == null ||
            window == null || !window.canPreview || window.animationClip == null ||
            !HasMovementCurve(receiver, window.animationClip)) return null;

        window.previewing = true;
        if (!AnimationMode.IsPropertyAnimated(receiver, DistanceProperty)) return null;

        return new EnemyMovementCurvePreview(receiver, window);
    }

    public static bool HasMovementCurve(EnemyAnimationEventReceiver receiver, AnimationClip clip)
    {
        return receiver.GetComponent<Animator>() != null &&
            AnimationUtility.GetEditorCurve(clip, GetBinding(receiver)) != null;
    }

    private static EditorCurveBinding GetBinding(EnemyAnimationEventReceiver receiver)
    {
        Animator animator = receiver.GetComponent<Animator>();
        string path = AnimationUtility.CalculateTransformPath(receiver.transform, animator.transform);
        return EditorCurveBinding.FloatCurve(path, typeof(EnemyAnimationEventReceiver), DistanceProperty);
    }

    public bool Update()
    {
        if (disposed || root == null || receiver == null || window == null ||
            !window.previewing || window.animationClip != clip || !AnimationMode.InAnimationMode() ||
            !AnimationMode.IsPropertyAnimated(receiver, DistanceProperty) ||
            !HasMovementCurve(receiver, clip)) return false;

        Vector3 position = root.position;
        position.x = Origin.x + Distance * direction;
        root.position = position;
        return true;
    }

    public void RecordPosition(Vector3 position)
    {
        if (!Update() || Frame == 0 || Playing) return;

        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        float time = Frame / clip.frameRate;
        float distance = (position.x - Origin.x) * direction;
        Undo.RecordObject(clip, "Edit Enemy Movement Curve");
        SetDistanceKey(curve, time, distance, 0.001f / clip.frameRate);

        AnimationUtility.SetEditorCurve(clip, binding, curve);
        EditorUtility.SetDirty(clip);
        window.Repaint();
        Update();
    }

    internal static void SetDistanceKey(AnimationCurve curve, float time, float distance, float tolerance)
    {
        int index = -1;
        int insertionIndex = 0;
        var leftModes = new AnimationUtility.TangentMode[curve.length];
        var rightModes = new AnimationUtility.TangentMode[curve.length];
        var broken = new bool[curve.length];
        for (int i = 0; i < curve.length; i++)
        {
            leftModes[i] = AnimationUtility.GetKeyLeftTangentMode(curve, i);
            rightModes[i] = AnimationUtility.GetKeyRightTangentMode(curve, i);
            broken[i] = AnimationUtility.GetKeyBroken(curve, i);
            if (curve[i].time < time) insertionIndex = i + 1;
            if (Mathf.Abs(curve[i].time - time) < tolerance) index = i;
        }

        bool added = index < 0;
        if (added)
        {
            index = curve.AddKey(new Keyframe(time, distance));
        }
        else
        {
            Keyframe key = curve[index];
            key.value = distance;
            curve.MoveKey(index, key);
        }

        for (int i = 0; i < leftModes.Length; i++)
        {
            int targetIndex = added && i >= insertionIndex ? i + 1 : i;
            AnimationUtility.SetKeyBroken(curve, targetIndex, broken[i]);
            AnimationUtility.SetKeyLeftTangentMode(curve, targetIndex, leftModes[i]);
            AnimationUtility.SetKeyRightTangentMode(curve, targetIndex, rightModes[i]);
        }

        if (added && leftModes.Length > 0)
        {
            var mode = insertionIndex > 0 ? rightModes[insertionIndex - 1] : leftModes[0];
            AnimationUtility.SetKeyBroken(curve, index, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, index, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, index, mode);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (window != null && window.animationClip == clip && window.previewing)
            window.previewing = false;
        if (root != null)
        {
            Vector3 position = root.localPosition;
            position.x = originalLocalX;
            root.localPosition = position;
        }

        Tools.hidden = toolsWereHidden;
    }
}
