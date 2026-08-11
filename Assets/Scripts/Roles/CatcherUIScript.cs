using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;


public enum  PowerState
{
    Recharged,
    Drained,
    Draining,
    Recharging
}
public class CatcherUIScript : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] RectTransform timerPanel;
    Coroutine powerDrainUICoroutine;


    private void OnEnable()
    {
        GameStartManager.OnRoundEndedClientSignal += OnRoundEnded;
        GameStartManager.OnNewRoundStartedClientSignal += OnRoundStarted;
    }

    private void OnDisable()
    {
        GameStartManager.OnRoundEndedClientSignal -= OnRoundEnded;
        GameStartManager.OnNewRoundStartedClientSignal -= OnRoundStarted;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    //For now we know that Catcher is always on server...but that won't be the case 
    //after lobby and relay implementation
    //might need to make on pole cursed using serverrpc later

    void OnRoundEnded()
    {
        timerPanel.localScale = Vector3.zero;
    }

    void OnRoundStarted()
    {
        timerPanel.localScale = Vector3.one;
    }
    public void ShowPowerDrainTimer(PowerState state,int remainingTime = 0)
    {
        switch (state)
        {
            case PowerState.Recharged:
                timerText.text = "Power Recharged";
                break;
            case PowerState.Drained:
                timerText.text = "Power Drained";
                break;
            case PowerState.Draining:
                {
                    if (powerDrainUICoroutine != null)
                    {
                        StopCoroutine(powerDrainUICoroutine);
                    }
                    powerDrainUICoroutine = StartCoroutine(PowerDrainTimerCoroutine(remainingTime));
                }
                break;
            case PowerState.Recharging:
                timerText.text = "Recharging....";
                break;

        }
       
    }

    IEnumerator PowerDrainTimerCoroutine(int remainingTime)
    {
        while (remainingTime > 0)
        {
            timerText.text = $"Power Drain in : {remainingTime.ToString()} S";
            yield return new WaitForSeconds(1f);
            remainingTime--;
        }
       
        timerText.text = "Power Drained";

    }



}
