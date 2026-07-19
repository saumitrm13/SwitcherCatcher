using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


[System.Serializable]
public class RequestObject
{
    public string Name;
    public GameObject Object;
}
public class SwitcherUIScript : MonoBehaviour
{
    private bool arePoleBtnsActivated = false;
    private bool isRequstScrollViewActivated = false;
    private HashSet<PoleType> sentRequestPoleTypes = new HashSet<PoleType>();



    [Header("Timer")]
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] GameObject timerPanel;
    [SerializeField] GameObject poleBtnsPanel;
    public SwitcherRquestHandler requestHandler;
    [SerializeField] GameObject requestsView;
    [SerializeField] GameObject scrollView;
    [SerializeField] GameObject requestPrefab;
    [SerializeField] TextMeshProUGUI debugText;
    [Header("Request Objects")]
    [SerializeField] GameObject RedRequest;
    [SerializeField] GameObject GreenRequest;
    [SerializeField] GameObject BlueRequest;
    [SerializeField] GameObject WhiteRequest;
    [SerializeField] GameObject BlackRequest;
    [SerializeField] GameObject PurpleRequest;
    [SerializeField] GameObject breakPartnershipButton;

    [Header("Send Request Buttons")]
    [SerializeField] GameObject SendRequestButtonForBlack;
    [SerializeField] GameObject SendRequestButtonForBlue;
    [SerializeField] GameObject SendRequestButtonForGreen;
    [SerializeField] GameObject SendRequestButtonForPurple;
    [SerializeField] GameObject SendRequestButtonForRed;
    [SerializeField] GameObject SendRequestButtonForWhite;
    private void Awake()
    {


    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.R)) {
            ToggleRequestsScrollView();
        }
    }

    public void TogglePoleBtns()
    {
        poleBtnsPanel.SetActive(!arePoleBtnsActivated);
        arePoleBtnsActivated = !arePoleBtnsActivated;
    }

    public void ToggleRequestsScrollView()
    {
        if (!isRequstScrollViewActivated)
        {
            requestsView.transform.localScale = Vector3.one;
        }
        else
        {
            requestsView.transform.localScale = Vector3.zero;
        }
        isRequstScrollViewActivated = !isRequstScrollViewActivated;
    }
    public void OnPoleBtnClicked(string pole)
    {
        if (requestHandler == null)
        {
            debugText.text = "Not ready yet — please wait.";
            return;
        }
        if (requestHandler.IsAllied())
        {
            debugText.text = "You are already allied!";
            return;
        }
        PoleType poleType = (PoleType)Enum.Parse(typeof(PoleType), pole);


        if (sentRequestPoleTypes.Contains(poleType))
        {
            debugText.text = $"Request already sent to {pole}";
            return;
        }
        switch (pole) {
            case "White":
                {


                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.White);
                    break;
                }
            case "Green":
                {

                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.Green);
                    break;
                }
            case "Red":
                {

                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.Red);
                    break;
                }
            case "Blue":
                {

                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.Blue);
                    break;
                }
            case "Purple":
                {

                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.Purple);
                    break;
                }
            case "Black":
                {

                    debugText.text = $"Sending request to {pole}";
                    requestHandler.OnPoleBtnClick(PoleType.Black);
                    break;
                }
            default:
                {
                    debugText.text = $"Pole text does not match {pole}";
                    break;
                }
        }

    }

    public void OnRequestSentSuccessfully(PoleType poleType)
    {
        sentRequestPoleTypes.Add(poleType);
        debugText.text = $"Request sent to {poleType}";
    }

    public void AddRequestInRequestPanel(Request request)
    {
        Debug.Log($"Debug point SwitcherUIScript AddRequestInRequestView Target Name : {request.SentByPoleType.ToString()}Request");
        Transform target = requestsView
    .GetComponentsInChildren<Transform>(true) // true = include inactive
    .FirstOrDefault(t => t.name == $"{request.SentByPoleType.ToString()}Request");
        target.localScale = Vector3.one;

    }

    public void AcceptRequestFromUI(int poleTypeInt)
    {
        PoleType poleType = (PoleType)poleTypeInt;
        if (requestHandler != null)
        {
            requestHandler.AcceptRequest(poleType);
        }
        else
        {
            Debug.LogError("[SwitcherUIScript] requestHandler is null");
        }
    }

    public void RemoveRequestFromPanel(PoleType poleType)
    {
        Transform target = requestsView
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == $"{poleType.ToString()}Request");
        if (target != null)
            target.localScale = Vector3.zero;
    }

    public void ClearRequestPanel()
    {
        foreach (Transform child in requestsView.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.EndsWith("Request"))
                child.localScale = Vector3.zero;
        }
    }

    public void ShowBreakPartnershipButton()
    {
        if (breakPartnershipButton != null)
            breakPartnershipButton.SetActive(true);
    }

    public void HideBreakPartnershipButton()
    {
        if (breakPartnershipButton != null)
            breakPartnershipButton.SetActive(false);
    }

    public void OnBreakPartnershipClicked()
    {
        if (requestHandler != null)
            requestHandler.BreakPartnership();
        else
            Debug.LogError("[SwitcherUIScript] requestHandler is null");
    }

    public void ClearSentRequestPoleTypesList()
    {
        sentRequestPoleTypes.Clear();
    }
    public void RemovePoleFromSentList(PoleType poleType)
    {
        sentRequestPoleTypes.Remove(poleType);
    }
    public void ShowTimeRemaining(int seconds)
    {
        if (timerPanel != null) timerPanel.SetActive(true);

        if (timerText != null)
        {
            timerText.text = seconds > 0
                ? $"Time left: {seconds}s"
                : "Time's up!";

            // Optional: turn red in the last 10 seconds
            timerText.color = seconds <= 10 ? Color.red : Color.white;
        }
    }

    public void HideTimer()
    {
        if (timerPanel != null) timerPanel.SetActive(false);
        if (timerText != null) timerText.text = "";
    }

    public void DestroySendRequestBtnForPoleType(PoleType poleType)
    {
        GameObject btnToDestroy = null;
        switch (poleType)
        {
            case PoleType.White:
                btnToDestroy = SendRequestButtonForWhite;
                break;
            case PoleType.Green:
                btnToDestroy = SendRequestButtonForGreen;
                break;
            case PoleType.Red:
                btnToDestroy = SendRequestButtonForRed;
                break;
            case PoleType.Blue:
                btnToDestroy = SendRequestButtonForBlue;
                break;
            case PoleType.Purple:
                btnToDestroy = SendRequestButtonForPurple;
                break;
            case PoleType.Black:
                btnToDestroy = SendRequestButtonForBlack;
                break;
        }
        if (btnToDestroy != null)
        {
            Destroy(btnToDestroy);
        }
    }

    public void DisableSendRequestBtnsForDestroyedPoleTypes(List<PoleType> destroyedPoleTypes)
    {
            Debug.Log("Disabling send request buttons for destroyed pole types: " + string.Join(", ", destroyedPoleTypes));
        foreach (PoleType poleType in destroyedPoleTypes)
            {   
                Debug.Log($"Attempting to disable send request button for pole type: {poleType}");
            DestroySendRequestBtnForPoleType(poleType);
            }
        
    } 
  }
