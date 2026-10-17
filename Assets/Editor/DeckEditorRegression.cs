using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>驗證分類與初始卡獨立選取，測試後還原玩家牌組。</summary>
[InitializeOnLoad]
public static class DeckEditorRegression
{
    private const string Key = "Chess.DeckEditorRegression";
    private static int stage;
    private static double finish;
    static DeckEditorRegression()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key,false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"Scene", ""));
            SessionState.SetBool(Key,false);
        };
    }
    /// <summary>從正式主選單開啟牌組 UI 測試。</summary>
    [MenuItem("Tools/Chess/Run Deck Editor Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(Key+"Scene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key,true); stage=0;
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/StartScene.unity");
        EditorApplication.isPlaying=true;
    }
    /// <summary>在初始化完成後驗證操作，保護玩家原本的存檔。</summary>
    private static void Tick()
    {
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying) return;
        if (stage==1)
        {
            if(EditorApplication.timeSinceStartup>finish)
            {
                Object.FindFirstObjectByType<StartMenuController>().ShowMainMenu();
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot("output/main-menu.png");
                stage=2; finish=EditorApplication.timeSinceStartup+1.5;
            }
            return;
        }
        if (stage==2) { if(EditorApplication.timeSinceStartup>finish) EditorApplication.isPlaying=false; return; }
        if (Time.timeSinceLevelLoad<.8f) return;
        stage=1;
        bool existed=PlayerPrefs.HasKey("SelectedDeckV2"); string saved=PlayerPrefs.GetString("SelectedDeckV2");
        var lines=new System.Collections.Generic.List<string>();
        try
        {
            var menu=Object.FindFirstObjectByType<StartMenuController>(); menu.OpenDeckEditor();
            var filters=menu.deckFilters;
            Action<bool,string> check=(ok,name)=>{ if(!ok)throw new Exception(name); lines.Add("PASS "+name); };
            menu.ClearCards(); check(!menu.saveDeckButton.interactable,"Empty deck cannot be saved");
            var toggle=typeof(StartMenuController).GetMethod("ToggleCard",BindingFlags.NonPublic|BindingFlags.Instance);
            toggle.Invoke(menu,new object[]{"E04"}); menu.SelectOpeningCard("E04");
            check(filters.opening.value==1,"Opening card selected independently from quantity");
            toggle.Invoke(menu,new object[]{"E04"}); check(filters.opening.value==1,"Adding second copy preserves opening choice");
            filters.categories[1].onClick.Invoke();
            check(!filters.Matches(menu.cardLibrary.GetCard("E04"),2),"Job category hides event without changing deck");
            filters.ResetFilters(); filters.search.text="E04";
            check(filters.Matches(menu.cardLibrary.GetCard("E04"),2) && !filters.Matches(menu.cardLibrary.GetCard("J01"),0),"Keyword filters card ID");
            filters.selectedOnlyButton.onClick.Invoke();
            check(!filters.Matches(menu.cardLibrary.GetCard("E04"),0),"Selected-only filter excludes unselected cards");
            menu.SaveDeck(); var config=DeckStorage.LoadConfigOrDefault(menu.cardLibrary.Cards);
            check(config.counts["E04"]==2 && config.openingCardId=="E04","Save roundtrip preserves quantity and opening card");
            menu.OpenDeckEditor(); toggle.Invoke(menu,new object[]{"E04"});
            check(filters.opening.value==0,"Removing last copy clears opening choice");
            menu.CloseDeckEditor(); menu.OpenDeckEditor();
            check(filters.opening.value==1,"Leaving without save discards draft");
            menu.SelectAllCards(); filters.ResetFilters(); menu.SelectOpeningCard("E04");
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot("output/deck-editor.png");
        }
        catch(Exception e) { lines.Add("FAIL "+e); }
        finally
        {
            if(existed)PlayerPrefs.SetString("SelectedDeckV2",saved);else PlayerPrefs.DeleteKey("SelectedDeckV2");
            PlayerPrefs.Save(); Directory.CreateDirectory("output"); File.WriteAllLines("output/deck-editor-regression.txt",lines);
            finish=EditorApplication.timeSinceStartup+1.5;
        }
    }
}
