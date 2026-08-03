using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ComponentUtility = UnityEditorInternal.ComponentUtility;

[InitializeOnLoad]
public static class ChessPieceAnimationSetup
{
    private const string SetupFlag =
        "ChessPieceAnimationSetup.Version.2";

    private const string AnimationFolder =
        "Assets/Prefabs/Animations";

    private const string ControllerPath =
        AnimationFolder + "/PieceShared.controller";

    private const string IdleClipPath =
        AnimationFolder + "/PieceIdle.anim";

    private const string MoveClipPath =
        AnimationFolder + "/PieceMove.anim";

    private const string CaptureClipPath =
        AnimationFolder + "/PieceCapture.anim";

    private const string CapturedClipPath =
        AnimationFolder + "/PieceCaptured.anim";

    static ChessPieceAnimationSetup()
    {
        EditorApplication.delayCall += RunOnce;
    }

    [MenuItem("Tools/Chess/Setup Piece Animations")]
    public static void RunManually()
    {
        Setup();
        SessionState.SetBool(SetupFlag, true);
    }

    private static void RunOnce()
    {
        if (SessionState.GetBool(SetupFlag, false))
        {
            return;
        }

        Setup();
        SessionState.SetBool(SetupFlag, true);
    }

    private static void Setup()
    {
        EnsureFolder(AnimationFolder);

        AnimationClip idleClip =
            EnsureClip(IdleClipPath, CreateIdleClip);
        AnimationClip moveClip =
            EnsureClip(MoveClipPath, CreateMoveClip);
        AnimationClip captureClip =
            EnsureClip(CaptureClipPath, CreateCaptureClip);
        AnimationClip capturedClip =
            EnsureClip(CapturedClipPath, CreateCapturedClip);

        AnimatorController controller =
            EnsureController(
                idleClip,
                moveClip,
                captureClip,
                capturedClip
            );

        ApplyToPiecePrefabs(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static AnimationClip EnsureClip(
        string path,
        System.Func<AnimationClip> createClip
    )
    {
        AnimationClip clip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

        if (clip != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        clip = createClip();
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimationClip CreateIdleClip()
    {
        AnimationClip clip = new AnimationClip
        {
            name = "PieceIdle",
            frameRate = 30f
        };

        SetLooping(clip, true);
        SetCurve(
            clip,
            "",
            typeof(PieceAnimationPlayer),
            "moveBlend",
            ConstantCurve(1f)
        );
        return clip;
    }

    private static AnimationClip CreateMoveClip()
    {
        AnimationClip clip = new AnimationClip
        {
            name = "PieceMove",
            frameRate = 30f
        };

        SetCurve(
            clip,
            "",
            typeof(PieceAnimationPlayer),
            "moveBlend",
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.24f, 1f)
            )
        );

        return clip;
    }

    private static AnimationClip CreateCaptureClip()
    {
        AnimationClip clip = new AnimationClip
        {
            name = "PieceCapture",
            frameRate = 30f
        };

        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.x",
            PulseScaleCurve()
        );
        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.y",
            PulseScaleCurve()
        );
        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.z",
            PulseScaleCurve()
        );

        return clip;
    }

    private static AnimationClip CreateCapturedClip()
    {
        AnimationClip clip = new AnimationClip
        {
            name = "PieceCaptured",
            frameRate = 30f
        };

        AnimationCurve shrinkCurve =
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.2f, 0.05f)
            );

        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.x",
            shrinkCurve
        );
        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.y",
            shrinkCurve
        );
        SetCurve(
            clip,
            "VisualRoot",
            typeof(Transform),
            "m_LocalScale.z",
            shrinkCurve
        );

        return clip;
    }

    private static AnimationCurve PulseScaleCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.08f, 1.18f),
            new Keyframe(0.18f, 1f)
        );
    }

    private static AnimationCurve ConstantCurve(float value)
    {
        return new AnimationCurve(
            new Keyframe(0f, value),
            new Keyframe(0.1f, value)
        );
    }

    private static AnimatorController EnsureController(
        AnimationClip idleClip,
        AnimationClip moveClip,
        AnimationClip captureClip,
        AnimationClip capturedClip
    )
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                ControllerPath
            );

        if (controller == null)
        {
            controller =
                AnimatorController.CreateAnimatorControllerAtPath(
                    ControllerPath
                );
        }

        EnsureTrigger(controller, "Move");
        EnsureTrigger(controller, "Capture");
        EnsureTrigger(controller, "Captured");

        AnimatorStateMachine stateMachine =
            controller.layers[0].stateMachine;

        AnimatorState idle =
            EnsureState(stateMachine, "Idle", idleClip);
        AnimatorState move =
            EnsureState(stateMachine, "Move", moveClip);
        AnimatorState capture =
            EnsureState(stateMachine, "Capture", captureClip);
        AnimatorState captured =
            EnsureState(stateMachine, "Captured", capturedClip);

        stateMachine.defaultState = idle;

        EnsureAnyStateTransition(stateMachine, move, "Move", 0.02f);
        EnsureAnyStateTransition(
            stateMachine,
            capture,
            "Capture",
            0.02f
        );
        EnsureAnyStateTransition(
            stateMachine,
            captured,
            "Captured",
            0.02f
        );

        EnsureReturnTransition(move, idle, 0.02f);
        EnsureReturnTransition(capture, idle, 0.02f);

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ApplyToPiecePrefabs(
        AnimatorController controller
    )
    {
        string[] prefabGuids =
            AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            if (!IsPiecePrefabName(name))
            {
                continue;
            }

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(path);

            bool changed = false;
            Transform visualRoot =
                prefabRoot.transform.Find("VisualRoot");

            if (visualRoot == null)
            {
                visualRoot =
                    new GameObject("VisualRoot").transform;
                visualRoot.SetParent(prefabRoot.transform, false);
                changed = true;
            }

            changed |= MoveVisualComponentsToChild(
                prefabRoot,
                visualRoot.gameObject
            );

            Animator animator =
                prefabRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabRoot.AddComponent<Animator>();
                changed = true;
            }

            if (animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                changed = true;
            }

            PieceAnimationPlayer animationPlayer =
                prefabRoot.GetComponent<PieceAnimationPlayer>();
            if (animationPlayer == null)
            {
                animationPlayer =
                    prefabRoot.AddComponent<PieceAnimationPlayer>();
                changed = true;
            }

            changed |= AssignSerializedObjectReference(
                animationPlayer,
                "animator",
                animator
            );
            changed |= AssignSerializedObjectReference(
                animationPlayer,
                "visualRoot",
                visualRoot
            );

            CardAnimationReceiver receiver =
                prefabRoot.GetComponent<CardAnimationReceiver>();
            if (receiver == null)
            {
                receiver =
                    prefabRoot.AddComponent<CardAnimationReceiver>();
                changed = true;
            }

            changed |= AssignSerializedObjectReference(
                receiver,
                "animator",
                animator
            );
            changed |= AssignSerializedObjectReference(
                receiver,
                "effectRoot",
                visualRoot
            );

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool MoveVisualComponentsToChild(
        GameObject root,
        GameObject visualRoot
    )
    {
        bool changed = false;

        changed |= MoveComponent<MeshFilter>(root, visualRoot);
        changed |= MoveComponent<MeshRenderer>(root, visualRoot);
        changed |= MoveComponent<MeshCollider>(root, visualRoot);

        return changed;
    }

    private static bool MoveComponent<T>(
        GameObject source,
        GameObject target
    ) where T : Component
    {
        T sourceComponent = source.GetComponent<T>();

        if (sourceComponent == null)
        {
            return false;
        }

        if (target.GetComponent<T>() == null)
        {
            ComponentUtility.CopyComponent(sourceComponent);
            ComponentUtility.PasteComponentAsNew(target);
        }

        Object.DestroyImmediate(sourceComponent, true);
        return true;
    }

    private static bool AssignSerializedObjectReference(
        Object target,
        string propertyName,
        Object value
    )
    {
        SerializedObject serializedObject =
            new SerializedObject(target);
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return false;
        }

        if (property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool IsPiecePrefabName(string name)
    {
        return
            name == "White Pawn" ||
            name == "White Rook" ||
            name == "White Knight" ||
            name == "White Bishop" ||
            name == "White Queen" ||
            name == "White King" ||
            name == "Black Pawn" ||
            name == "Black Rook" ||
            name == "Black Knight" ||
            name == "Black Bishop" ||
            name == "Black Queen" ||
            name == "Black King";
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine stateMachine,
        string stateName,
        Motion motion
    )
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                childState.state.motion = motion;
                return childState.state;
            }
        }

        AnimatorState state = stateMachine.AddState(stateName);
        state.motion = motion;
        return state;
    }

    private static void EnsureTrigger(
        AnimatorController controller,
        string triggerName
    )
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == triggerName)
            {
                return;
            }
        }

        controller.AddParameter(
            triggerName,
            AnimatorControllerParameterType.Trigger
        );
    }

    private static void EnsureAnyStateTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState targetState,
        string triggerName,
        float duration
    )
    {
        foreach (
            AnimatorStateTransition transition in
            stateMachine.anyStateTransitions
        )
        {
            if (transition.destinationState != targetState)
            {
                continue;
            }

            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == triggerName)
                {
                    return;
                }
            }
        }

        AnimatorStateTransition newTransition =
            stateMachine.AddAnyStateTransition(targetState);
        newTransition.hasExitTime = false;
        newTransition.duration = duration;
        newTransition.AddCondition(
            AnimatorConditionMode.If,
            0f,
            triggerName
        );
    }

    private static void EnsureReturnTransition(
        AnimatorState fromState,
        AnimatorState toState,
        float duration
    )
    {
        foreach (AnimatorStateTransition transition in fromState.transitions)
        {
            if (transition.destinationState == toState)
            {
                return;
            }
        }

        AnimatorStateTransition newTransition =
            fromState.AddTransition(toState);
        newTransition.hasExitTime = true;
        newTransition.exitTime = 1f;
        newTransition.duration = duration;
    }

    private static void SetCurve(
        AnimationClip clip,
        string path,
        System.Type type,
        string propertyName,
        AnimationCurve curve
    )
    {
        clip.SetCurve(path, type, propertyName, curve);
        EditorUtility.SetDirty(clip);
    }

    private static void SetLooping(AnimationClip clip, bool looping)
    {
        SerializedObject serializedClip = new SerializedObject(clip);
        SerializedProperty settings =
            serializedClip.FindProperty("m_AnimationClipSettings");

        if (settings != null)
        {
            SerializedProperty loopTime =
                settings.FindPropertyRelative("m_LoopTime");

            if (loopTime != null)
            {
                loopTime.boolValue = looping;
            }
        }

        serializedClip.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)
            ?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
