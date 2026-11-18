using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class FieldRoomAppearanceSetup
{
    [MenuItem("Tools/Chess/Presentation/Register Field Room Appearances")]
    public static void Register()
    {
        const string path = "Assets/Resources/FieldRoomCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<FieldRoomCatalog>(path);
        bool create = catalog == null;
        if (create) catalog = ScriptableObject.CreateInstance<FieldRoomCatalog>();
        var ids = new[] { "F02", "F04", "F06", "F08", "F10", "F11", "F12" };
        var entries = ids.Select(id =>
        {
            string prefabPath = AssetDatabase.FindAssets("SceneRoot_" + id + " t:Prefab", new[] { "Assets/Room/AdvancedFieldVariants" })
                .Select(AssetDatabase.GUIDToAssetPath).Single();
            return new FieldRoomCatalog.Entry { fieldId = id, roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) };
        }).ToArray();
        catalog.rooms = entries;
        if (create) AssetDatabase.CreateAsset(catalog, path);
        else { EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog); }
        Debug.Log("Registered all seven field room appearances.");
    }
}

/// <summary>在當前棋局的 Play Mode 副本驗證真實合成；不儲存編輯中的場景。</summary>
[InitializeOnLoad]
public static class FieldRoomAppearanceRegression
{
    const string Key = "ChessRPG.FieldRoomRegression";
    const string Report = "output/advanced-field-rooms/runtime-verification.txt";
    static IEnumerator routine;
    static double deadline;

    static FieldRoomAppearanceRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 100;
                routine = Checks();
                new GameObject("Field room regression").AddComponent<FieldRoomRegressionRunner>();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "Start", ""));
                SessionState.SetBool(Key, false);
                routine = null;
            }
        };
    }

    [MenuItem("Tools/Chess/Presentation/Test Field Room Fusion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Photon.Pun.PhotonNetwork.IsConnected)
            throw new InvalidOperationException("Requires disconnected Edit Mode.");
        if (SceneManager.GetActiveScene().name != "ChessScene") throw new InvalidOperationException("Open ChessScene first.");
        FieldRoomAppearanceSetup.Register();
        SessionState.SetString(Key + "Start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = null;
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory("output/advanced-field-rooms");
        File.WriteAllText(Report, "Runtime fusion / room lifecycle checks\n");
        LogicManager.SetNextGameMode(false);
        EditorApplication.isPlaying = true;
    }

    public static void Tick()
    {
        if (routine == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
            if (routine.MoveNext()) return;
            File.AppendAllText(Report, "PASS: all runtime room checks\n");
        }
        catch (Exception error) { File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); }
        routine = null;
        EditorApplication.isPlaying = false;
    }

    static IEnumerator Checks()
    {
        yield return null;
        var logic = Object.FindFirstObjectByType<LogicManager>();
        var original = logic.gameObject.scene.GetRootGameObjects().Single(g => g.name == "SceneRoot");
        var pairs = new[] { "F01,F01,F02", "F03,F03,F04", "F05,F05,F06", "F07,F07,F08", "F09,F09,F10", "F01,F03,F11", "F05,F07,F12" };
        foreach (var pair in pairs)
        {
            logic.Initialize();
            yield return null;
            Check(original.activeSelf && !Rooms(logic).Any(), "Reset restores original room");
            var ids = pair.Split(',');
            var slots = logic.GetFieldPlaces();
            Check(logic.TryPlayFieldCard(Card(ids[0]), slots[0]), "First card accepted " + ids[0]);
            Check(original.activeSelf, "Base field keeps original room");
            if (ids[2] == "F12") logic.DeferFullGameRestartUntilUsingCard();
            Check(logic.TryPlayFieldCard(Card(ids[1]), slots[1]), "Second card accepted " + ids[1]);
            while (logic.GetComponent<FieldRoomAppearance>() == null || logic.GetComponent<FieldRoomAppearance>().CurrentFieldId != ids[2])
                yield return null;
            var appearance = logic.GetComponent<FieldRoomAppearance>();
            Check(!original.activeSelf && Rooms(logic).Count() == 1, "Exactly one themed room " + ids[2]);
            var room = Rooms(logic).Single();
            Check(room.transform.position == original.transform.position && room.transform.localScale == original.transform.localScale,
                "Room preserves authored transform " + ids[2]);
            appearance.Show(ids[2]);
            Check(Rooms(logic).Single() == room, "Repeated update reuses room " + ids[2]);
            if (ids[2] == "F12")
            {
                Check(logic.IsFieldFusionPlaying, "F12 locks input while presenting room");
                float until = Time.realtimeSinceStartup + .5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Check(appearance.CurrentFieldId == "F12" && logic.IsFieldFusionPlaying, "F12 remains visible before restart");
            }
            while (logic.IsFieldFusionPlaying) yield return null;
            Check(logic.GetActiveFieldCount(ids[2]) == 1, "Fusion gameplay result " + ids[2]);
        }
        logic.Initialize();
        yield return null;
        var places = logic.GetFieldPlaces();
        logic.TryPlayFieldCard(Card("F02"), places[0]);
        Check(!original.activeSelf, "Direct advanced card shows room");
        logic.ApplyClassicChessMode();
        yield return null;
        Check(original.activeSelf && !Rooms(logic).Any(), "Classic mode restores original, removes themed room");
    }

    static System.Collections.Generic.IEnumerable<GameObject> Rooms(LogicManager logic) =>
        logic.gameObject.scene.GetRootGameObjects().Where(g => g.name.StartsWith("FieldRoom_") && g.activeSelf);
    static CardDefinition Card(string id) => Resources.Load<CardAsset>("Cards/" + id).CreateDefinition();
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        File.AppendAllText(Report, "PASS: " + label + "\n");
    }
}

public sealed class FieldRoomRegressionRunner : MonoBehaviour
{
    void Update() => FieldRoomAppearanceRegression.Tick();
}
