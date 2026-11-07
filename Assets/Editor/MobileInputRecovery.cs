using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Repairs an invalid editor-only InputSettings instance without restarting or saving scenes.</summary>
public static class MobileInputRecovery
{
    [MenuItem("Tools/Chess/Mobile/Repair Editor Input Settings")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Stop Play Mode before repairing editor input.");

        // Prefer the project's configured asset. Projects without one use Input System defaults.
        if (!EditorBuildSettings.TryGetConfigObject<InputSettings>("com.unity.input.settings", out var settings) || settings == null)
        {
            settings = ScriptableObject.CreateInstance<InputSettings>();
            settings.name = "Editor Input Settings (recovered defaults)";
            settings.hideFlags = HideFlags.HideAndDontSave;
        }
        InputSystem.settings = settings;
        Directory.CreateDirectory("output");
        File.WriteAllText("output/input-recovery.txt", "InputSettings restored: " + (InputSystem.settings != null) + "\n" +
            "Update mode: " + InputSystem.settings.updateMode + "\nNo scenes saved or reloaded.\n");
        Debug.Log("Editor InputSettings restored. No scenes saved or reloaded.");
    }
}
