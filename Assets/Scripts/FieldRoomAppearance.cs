using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只替換房間，不影響房間外的棋盤、攝影機或遊戲狀態。</summary>
public sealed class FieldRoomAppearance : MonoBehaviour
{
    private FieldRoomCatalog catalog;
    private GameObject originalRoom;
    private GameObject themedRoom;
    private bool originalWasActive;
    public string CurrentFieldId { get; private set; }

    public void Show(string fieldId)
    {
        if (!FieldCardRules.IsAdvancedField(fieldId)) { Restore(); return; }
        if (CurrentFieldId == fieldId && themedRoom != null) return;
        if (catalog == null) catalog = Resources.Load<FieldRoomCatalog>("FieldRoomCatalog");
        var prefab = catalog != null ? catalog.Find(fieldId) : null;
        if (prefab == null)
        {
            Restore();
            Debug.LogWarning("[FieldRoom] Missing room prefab for " + fieldId, this);
            return;
        }
        if (originalRoom == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root.name == "SceneRoot") { originalRoom = root; break; }
            if (originalRoom == null) return; // Pure board/test scenes need no environment.
            originalWasActive = originalRoom.activeSelf;
        }

        RemoveTheme();
        // Instantiate under an inactive parent so prefab lights/colliders never overlap the original.
        var staging = new GameObject("FieldRoom staging");
        SceneManager.MoveGameObjectToScene(staging, gameObject.scene);
        staging.SetActive(false);
        themedRoom = Instantiate(prefab, staging.transform, false);
        themedRoom.name = "FieldRoom_" + fieldId;
        themedRoom.SetActive(false);
        themedRoom.transform.SetParent(null, false);
        themedRoom.transform.SetPositionAndRotation(originalRoom.transform.position, originalRoom.transform.rotation);
        themedRoom.transform.localScale = originalRoom.transform.localScale;
        Destroy(staging);
        originalRoom.SetActive(false);
        themedRoom.SetActive(originalWasActive);
        CurrentFieldId = fieldId;
    }

    public void Restore()
    {
        RemoveTheme();
        if (originalRoom != null) originalRoom.SetActive(originalWasActive);
        originalRoom = null;
    }

    private void RemoveTheme()
    {
        if (themedRoom != null)
        {
            themedRoom.SetActive(false);
            Destroy(themedRoom);
            themedRoom = null;
        }
        CurrentFieldId = null;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
