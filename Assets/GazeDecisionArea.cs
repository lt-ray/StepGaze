using UnityEngine;
using UnityEngine.UI;

public class GazeDecisionArea : MonoBehaviour
{
    public int index;               // 1, 2, 3...
    public GazeButton owner;        // 親ボタン
    public float dwellDuration = 0.2f;  // このエリアに必要な dwell 秒数（GazeButton から設定される）

    private bool wasGazedLastFrame = false;
    private bool passedInThisStay = false;  // 今の「滞在」の間に一度通過判定したか
    private float gazeTimer = 0f;

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();
        bool nowGazed = (gazed == gameObject);

        if (nowGazed)
        {
            // 新しく乗った瞬間にタイマーリセット
            if (!wasGazedLastFrame)
            {
                gazeTimer = 0f;
                passedInThisStay = false;
            }

            gazeTimer += Time.deltaTime;

            // まだこの滞在中に通過報告していなくて、dwell時間を超えたら通過
            if (!passedInThisStay && gazeTimer >= dwellDuration)
            {
                passedInThisStay = true;
                owner.OnAreaPassed(index, this);
            }
        }
        else
        {
            // 視線が外れたらタイマーもフラグもリセット
            gazeTimer = 0f;
            passedInThisStay = false;
        }

        wasGazedLastFrame = nowGazed;
    }

    // 見た目だけ消したい（Update は動かしたまま）とき用
    public void SetVisible(bool visible)
    {
        var graphics = GetComponentsInChildren<Graphic>();
        foreach (var g in graphics)
        {
            g.enabled = visible;
        }
    }
}
