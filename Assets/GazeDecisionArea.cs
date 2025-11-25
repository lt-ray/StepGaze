using UnityEngine;
using UnityEngine.UI;

public class GazeDecisionArea : MonoBehaviour
{
    public int index;          // 1, 2, 3...
    public GazeButton owner;   // 親ボタン

    private bool wasGazedLastFrame = false;

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();
        bool nowGazed = (gazed == gameObject);

        // 視線がこのエリアに「入った瞬間」だけ通知
        if (nowGazed && !wasGazedLastFrame)
        {
            owner.OnAreaPassed(index, this);
        }

        wasGazedLastFrame = nowGazed;
    }

    // 見た目だけ消したい（Update は動かしたままにしたい）ので、
    // GameObject.SetActive(false) ではなく Graphic.enabled を切る
    public void SetVisible(bool visible)
    {
        var graphics = GetComponentsInChildren<Graphic>();
        foreach (var g in graphics)
        {
            g.enabled = visible;
        }
    }
}
