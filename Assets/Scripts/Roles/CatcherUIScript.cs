using System.Collections;

using TMPro;

using Unity.Netcode;

using UnityEngine;


public enum PowerState

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

    // ── Pending-state cache for when ShowPowerDrainTimer is called while inactive ──
    bool hasPendingState = false;
    PowerState pendingState;
    int pendingRemainingTime;

    private void OnEnable()

    {

        GameStartManager.OnRoundEndedClientSignal += OnRoundEnded;

        GameStartManager.OnNewRoundStartedClientSignal += OnRoundStarted;

        // Replay whatever call came in while this object was inactive.
        if (hasPendingState)
        {
            hasPendingState = false;
            ShowPowerDrainTimer(pendingState, pendingRemainingTime);
        }

    }

    private void OnDisable()

    {

        GameStartManager.OnRoundEndedClientSignal -= OnRoundEnded;

        GameStartManager.OnNewRoundStartedClientSignal -= OnRoundStarted;

        // Any in-flight coroutine is dead the moment this object is disabled.
        if (powerDrainUICoroutine != null)
        {
            StopCoroutine(powerDrainUICoroutine);
            powerDrainUICoroutine = null;
        }

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

    public void ShowPowerDrainTimer(PowerState state, int remainingTime = 0)

    {
        // If this object (or a parent) is currently inactive, StartCoroutine will
        // throw. Cache the call and replay it from OnEnable once it's safe.
        if (!gameObject.activeInHierarchy)
        {
            hasPendingState = true;
            pendingState = state;
            pendingRemainingTime = remainingTime;
            Debug.LogWarning($"[CatcherUIScript] ShowPowerDrainTimer called while inactive; caching state {state} to replay on enable.");
            return;
        }

        Debug.LogWarning($"ShowPowerDrainTimer called with state: {state} and remainingTime: {remainingTime}");
        switch (state)

        {

            case PowerState.Recharged:

                if (powerDrainUICoroutine != null)

                {

                    StopCoroutine(powerDrainUICoroutine);

                    powerDrainUICoroutine = null;

                }

                timerText.text = "Power Recharged";

                break;

            case PowerState.Drained:

                if (powerDrainUICoroutine != null)

                {

                    StopCoroutine(powerDrainUICoroutine);

                    powerDrainUICoroutine = null;

                }

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

                if (powerDrainUICoroutine != null)

                {

                    StopCoroutine(powerDrainUICoroutine);

                    powerDrainUICoroutine = null;

                }

                timerText.text = "Recharging....";

                break;

        }

    }

    IEnumerator PowerDrainTimerCoroutine(int remainingTime)

    {
        Debug.LogWarning($"PowerDrainTimerCoroutine started with remainingTime: {remainingTime}");
        while (remainingTime > 0)

        {

            timerText.text = $"Power Drain in : {remainingTime.ToString()} S";

            yield return new WaitForSeconds(1f);

            remainingTime--;

        }

        timerText.text = "Power Drained";

    }


}