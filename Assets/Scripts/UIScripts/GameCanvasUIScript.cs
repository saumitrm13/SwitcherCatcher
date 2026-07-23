using TMPro;
using UnityEngine;

public class GameCanvasUIScript : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI debugText;
    [SerializeField] TextMeshProUGUI debugText2;
    [SerializeField] TextMeshProUGUI debugText3;
    [SerializeField] TextMeshProUGUI targetPoleText;
    [SerializeField] TextMeshProUGUI triggerCaseText;

    [SerializeField] RectTransform switcherUIPanelRect;

    [SerializeField] TextMeshProUGUI roundTimerText;

    float timeRemaining;
    bool timerRunning = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameStartManager.OnRoundEndedClientSignal += GameUIRoutineAfterRoundEnd;
        GameStartManager.OnRoundTimerStarted += StartLocalTimer;
        GameStartManager.OnRoundTimerCorrection += CorrectLocalTimer;
    }

    // Update is called once per frame
    void Update()
    {
        if (!timerRunning) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining < 0f) timeRemaining = 0f;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        roundTimerText.text = $"{minutes}:{seconds:00}";
    }

    void GameUIRoutineAfterRoundEnd()
    {
        debugText.text = "";
        debugText2.text = "";
        debugText3.text = "No Pole!";
        targetPoleText.text = "No Pole!";
        triggerCaseText.text = "";
        switcherUIPanelRect.localScale = Vector3.zero;
        timerRunning = false;

    }
    void OnDestroy()
    {
        GameStartManager.OnRoundEndedClientSignal -= GameUIRoutineAfterRoundEnd;
        GameStartManager.OnRoundTimerStarted -= StartLocalTimer;
        GameStartManager.OnRoundTimerCorrection -= CorrectLocalTimer;
    }

    void StartLocalTimer(float duration)
    {
        timeRemaining = duration;
        timerRunning = true;
    }

    void CorrectLocalTimer(float serverRemaining)
    {
        timeRemaining = serverRemaining; // snap to server truth, fixes any drift
    }


}
