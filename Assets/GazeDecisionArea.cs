using UnityEngine;

public class GazeDecisionArea : MonoBehaviour
{
    public int index;          // 1 か 2
    public GazeButton owner;   // 親ボタン

    private bool wasGazedLastFrame = false;

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();
        bool nowGazed = (gazed == gameObject);

        // 「今回見ている && 前のフレームでは見ていなかった」
        // → 視線がエリアに「入った瞬間」
        if (nowGazed && !wasGazedLastFrame)
        {
            owner.OnAreaPassed(index);
        }

        wasGazedLastFrame = nowGazed;
    }
}
