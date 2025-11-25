using UnityEngine;

public class GazeDecisionArea : MonoBehaviour
{
    public int index;          // 1, 2, 3 ...
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
            owner.OnAreaPassed(index);
        }

        wasGazedLastFrame = nowGazed;
    }
}
