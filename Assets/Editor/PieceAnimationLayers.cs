using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Keep movement progress and visual action curves on independent layers.</summary>
public static class PieceAnimationLayers
{
    [MenuItem("Tools/Chess/Presentation/Separate Piece Animation Layers")]
    public static void Apply()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Prefabs/Animations/PieceShared.controller");
        Configure(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }

    public static void Configure(AnimatorController controller)
    {
        var movement = controller.layers[0].stateMachine;
        if (!controller.layers.Any(l => l.name == "Actions")) controller.AddLayer("Actions");
        var layers = controller.layers;
        var actionLayer = layers.First(l => l.name == "Actions");
        actionLayer.defaultWeight = 1f;
        controller.layers = layers;
        var actions = actionLayer.stateMachine;

        // Reparent existing states without replacing their clips or authored speeds.
        foreach (string name in new[] { "Capture", "Captured" })
        {
            var source = movement.states.FirstOrDefault(s => s.state.name == name);
            if (source.state == null) continue;
            var old = actions.states.FirstOrDefault(s => s.state.name == name);
            if (old.state != null) actions.RemoveState(old.state);
            actions.states = actions.states.Concat(new[] { source }).ToArray();
            movement.states = movement.states.Where(s => s.state != source.state).ToArray();
        }
        foreach (var t in movement.anyStateTransitions.ToArray())
        {
            if (t.destinationState != null && (t.destinationState.name == "Capture" || t.destinationState.name == "Captured"))
                movement.RemoveAnyStateTransition(t);
            else t.duration = 0f;
        }

        const string idlePath = "Assets/Prefabs/Animations/PieceActionIdle.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(idlePath);
        if (clip == null)
        {
            clip = new AnimationClip { name = "PieceActionIdle", frameRate = 30f };
            foreach (string axis in new[] { "x", "y", "z" })
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("VisualRoot", typeof(Transform), "m_LocalScale." + axis), AnimationCurve.Constant(0, .1f, 1f));
            AssetDatabase.CreateAsset(clip, idlePath);
        }
        var idle = actions.states.FirstOrDefault(s => s.state.name == "Idle").state ?? actions.AddState("Idle");
        idle.motion = clip;
        actions.defaultState = idle;
        foreach (var child in actions.states.Concat(movement.states)) child.state.writeDefaultValues = false;
        foreach (string name in new[] { "Capture", "Captured" })
        {
            var state = actions.states.First(s => s.state.name == name).state;
            foreach (var t in state.transitions) t.destinationState = idle;
            if (!actions.anyStateTransitions.Any(t => t.destinationState == state))
            {
                var t = actions.AddAnyStateTransition(state);
                t.hasExitTime = false;
                t.duration = .02f;
                t.AddCondition(AnimatorConditionMode.If, 0, name);
            }
        }
        EditorUtility.SetDirty(controller);
    }
}
