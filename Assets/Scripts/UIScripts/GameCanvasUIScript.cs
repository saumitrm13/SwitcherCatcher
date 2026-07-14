using TMPro;
using UnityEngine;

public class GameCanvasUIScript : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI debugText;
    [SerializeField] TextMeshProUGUI debugText2;
    [SerializeField] TextMeshProUGUI debugText3;

    [SerializeField] TextMeshProUGUI triggerCaseText;

    [SerializeField] RectTransform switcherUIPanelRect;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameStartManager.OnRoundEndedClientSignal += GameUIRoutineAfterRoundEnd;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GameUIRoutineAfterRoundEnd()
    {
        debugText.text = "";
        debugText2.text = "";
        debugText3.text = "";   
        triggerCaseText.text = "";
        switcherUIPanelRect.localScale = Vector3.zero;

    }
}
