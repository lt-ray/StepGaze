using UnityEngine;
using UnityEngine.UI;

public class GazeDecisionArea : MonoBehaviour
{
    public int index;               
    public GazeButton owner;        
    public float dwellDuration = 0.2f;

    private bool wasGazedLastFrame = false;
    private bool passedInThisStay = false;
    private float gazeTimer = 0f;

    private Image img;

    private void Awake()
    {
        img = GetComponent<Image>();
    }

    private void Update()
    {
        if (GazeManager.Instance == null) return;

        GameObject gazed = GazeManager.Instance.GetGazedUI();
        bool nowGazed = (gazed == gameObject);

        if (nowGazed)
        {
            if (!wasGazedLastFrame)
            {
                gazeTimer = 0f;
                passedInThisStay = false;
            }

            gazeTimer += Time.deltaTime;

            if (!passedInThisStay && gazeTimer >= dwellDuration)
            {
                passedInThisStay = true;
                owner.OnAreaPassed(index, this);
            }
        }
        else
        {
            gazeTimer = 0f;
            passedInThisStay = false;
        }

        wasGazedLastFrame = nowGazed;
    }

    public void SetVisible(bool visible)
    {
        var graphics = GetComponentsInChildren<Graphic>();
        foreach (var g in graphics) g.enabled = visible;
    }

    public void SetColor(Color c)
    {
        if (img == null) img = GetComponent<Image>();
        if (img != null) img.color = c;
    }
}