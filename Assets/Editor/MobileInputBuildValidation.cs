using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>Validate the actual scenes processed into the player, including inactive UI.</summary>
public sealed class MobileInputBuildValidation : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var module in root.GetComponentsInChildren<BaseInputModule>(true))
                if (module is StandaloneInputModule || module.GetType().Name == "TouchInputModule")
                    throw new BuildFailedException(scene.path + ": legacy UI input module on " + module.name + ". Use InputSystemUIInputModule.");
            foreach (var system in root.GetComponentsInChildren<EventSystem>(true))
            {
                var input = system.GetComponent<InputSystemUIInputModule>();
                if (input == null || !input.enabled || input.actionsAsset == null)
                    throw new BuildFailedException(scene.path + ": EventSystem requires an enabled InputSystemUIInputModule with input actions.");
            }
        }
    }
}
