using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class GazeManager : MonoBehaviour
{
    public static GazeManager Instance;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private EventSystem eventSystem;
    public Vector2 GazeScreenPosition { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (graphicRaycaster == null) graphicRaycaster = FindObjectOfType<GraphicRaycaster>();
        if (eventSystem == null) eventSystem = FindObjectOfType<EventSystem>();
    }

    private void Update()
    {
        GazeScreenPosition = Input.mousePosition; // ★アイトラッカー使用時はここを変更
    }

    public GameObject GetGazedUI()
    {
        if (graphicRaycaster == null || eventSystem == null) return null;
        var pointerData = new PointerEventData(eventSystem) { position = GazeScreenPosition };
        var results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerData, results);
        return results.Count > 0 ? results[0].gameObject : null;
    }
}