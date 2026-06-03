using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class SwitcherRquestHandler : NetworkBehaviour
{

    private TextMeshProUGUI debugText;
    ClientRpcParams thisClientRpcParams;
    bool hasPendingRequests = false;
    List<Request> pendingRequests = new List<Request>();
    List<Request> pendingResponses = new List<Request>();
    public static SwitcherRquestHandler LocalOwnerInstance;
    // FIX 1: Removed `static`. A static list is shared across ALL handler instances on
    // the server, meaning requests from any player contaminate every other player's queue.
    // This must be a per-instance field so each handler only tracks its own requests.
    List<Request> allRequestDataForServer = new List<Request>();
    private static List<PoleType> DestroyedPoles = new List<PoleType>();
    PoleType thisPoleType = PoleType.None;
    GameObject switcherCanvas;

    [HideInInspector]
    public SwitcherUIScript switcherUIFunctions;
    [HideInInspector]
    public UIEffects switcherCanvasUIEffects;
    PoleType alliedWithPoleType = PoleType.None;


    void Start()
    {

    }

    public override void OnNetworkSpawn()
    {
        SwitcherScript.OnSwitcherPoleAssigned += AssignThisPole;

        debugText = GameObject.Find("DebugText2").GetComponent<TextMeshProUGUI>();
        thisClientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
        if (IsOwner)
        {
            LocalOwnerInstance = this;
        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalOwnerInstance == this)
        {
            LocalOwnerInstance = null;
        }
    }
    private void AssignThisPole()
    {
        if (IsServer)
        {
            Debug.Log($"[SwitcherRquestHandler , AssignThisPole] Attempting to assign pole");
            if (GetComponent<SwitcherScript>() != null)
            {

                if (GetComponent<SwitcherScript>().thisSwitcher != null)
                {
                    if (GetComponent<SwitcherScript>().thisSwitcher.OwnsAPole())
                    {
                        thisPoleType = GetComponent<SwitcherScript>().thisSwitcher.getOwnedPoleType();
                        Debug.Log($"Debug Point : SwitcherRequestHandler : AssignThisPole IsServer");
                    }
                    else
                    {
                        Debug.Log($"[SwitcherRquestHandler , AssignThisPole] this switcher does not own a pole");
                    }
                }
                else
                {
                    Debug.Log($"[SwitcherRquestHandler , AssignThisPole] this switcher is null");
                }
            }
            else
            {
                Debug.Log($"Switcher script not found");
            }
        }

        switcherCanvas = GameObject.Find("SwitcherCanvasPanel");
        if (IsOwner)
        {
            switcherCanvas.SetActive(true);
            switcherUIFunctions = switcherCanvas.GetComponent<SwitcherUIScript>();
            switcherUIFunctions.requestHandler = this;
            switcherCanvasUIEffects = switcherCanvas.GetComponent<UIEffects>();
            switcherCanvas.GetComponent<RectTransform>().localScale = Vector3.one;
            if(DestroyedPoles.Count == 0)
            {
                  debugText.text = $"No destroyed poles at the moment";
                Debug.Log($"No destroyed poles at the moment");
            }
            Debug.Log(Equals(switcherUIFunctions, null) ? "switcherUIFunctions is null" : "switcherUIFunctions is not null");
            Debug.Log("Disabling send request buttons for destroyed pole types: " + string.Join(", ", DestroyedPoles));
            switcherUIFunctions.DisableSendRequestBtnsForDestroyedPoleTypes(DestroyedPoles);
            Debug.Log(GetInstanceID());
            if (switcherUIFunctions != null)
            {
                Debug.Log($"Debug Point : SwitcherRequestHandler : AssignThisPole IsOwner");
            }
            else
            {
                Debug.Log($"Debug Point : SwitcherRequestHandler : AssignThisPole IsOwner But switcherUIFunctons null");
            }
        }
    }


    public void UpdatePoleType(PoleType newPoleType)
    {
        thisPoleType = newPoleType;
    }

    void Update()
    {
        if (IsOwner)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                AcceptExhangeRequestServerRpc();
            }
        }
    }

    [ClientRpc]
    void SendRequestToSwitcherClientRpc(PoleType fromPole, PoleType toPole, ClientRpcParams clientRpcParams)
    {
        Debug.Log("Request received");
        if (debugText != null)
        {
            debugText.text = $"Exchange request received from {fromPole} pole owner";
        }

        Request currentRequest = new Request(fromPole, toPole);
        pendingRequests.Add(currentRequest);

        // "this" here is the SENDER's component on this machine — not the local owner's.
        // So we must find the local owner's handler to get the correct switcherUIFunctions.
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);

        if (localOwnerHandler == null)
        {
            Debug.LogError("Could not find local owner SwitcherRequestHandler");
            return;
        }

        if (localOwnerHandler.switcherUIFunctions != null)
        {
            localOwnerHandler.switcherUIFunctions.AddRequestInRequestPanel(currentRequest);
        }
        else
        {
            Debug.LogError("Local owner's switcherUIFunctions is null");
        }
    }

    [ServerRpc]
    void UpdatePendingRequestsServerRpc(PoleType fromPole, PoleType toPole)
    {
        Request serverRequest = new Request(fromPole, toPole);
        // FIX 2a: Was `allRequestDataForServer.Append(serverRequest)`.
        // List.Append() is a LINQ extension that returns a NEW IEnumerable without
        // modifying the original list. The returned value was discarded, so nothing
        // was ever stored. List.Add() mutates the list in place, as intended.
        allRequestDataForServer.Add(serverRequest);
        hasPendingRequests = true;
    }

    public void OnPoleBtnClick(PoleType poleType)
    {
        if (!IsOwner)
        {
            debugText.text = $"Debug point : OnPoleBtnClick : Not owner";
            Debug.Log($"Debug point : OnPoleBtnClick : Not owner");
            return;
        }
        Debug.Log($"Debug point : OnPoleBtnClick : Calling HandleSwitchRequestServerRpc");
        debugText.text = $"Debug point : OnPoleBtnClick : Calling HandleSwitchRequestServerRpc";
        HandleSwitchRequestServerRpc(poleType);
    }

    [ServerRpc]
    void HandleSwitchRequestServerRpc(PoleType poleType)
    {
        if (poleType == thisPoleType) return;
        if (alliedWithPoleType != PoleType.None)
        {
            Debug.Log("[HandleSwitchRequest] Already allied — cannot send request");
            NotifyClientAboutThePoleClientRpc("You are already allied with someone", thisClientRpcParams);
            return;
        }
        string poleName = poleType.ToString() + "Pole";
        Pole targetPole = GameObject.Find(poleName)?.GetComponent<PoleScript>().thisPole;
        SwitcherRquestHandler targetHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
    .FirstOrDefault(h => h.thisPoleType == poleType);

        if (targetHandler != null && targetHandler.alliedWithPoleType != PoleType.None)
        {
            Debug.Log("[HandleSwitchRequest] Target already allied — cannot send request");
            NotifyClientAboutThePoleClientRpc($"{poleType} pole owner is already allied with someone", thisClientRpcParams);
            return;
        }
        Debug.Log($"Checking if the {poleType} has an owner");
        if (targetPole.HasOwner())
        {
            ulong poleOwnerID = targetPole.GetOwner().getClientID();
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { poleOwnerID }
                }
            };
            Debug.Log($"Sending request to client with pole owner ID as {poleOwnerID}");
            debugText.text = $"Sending request to client with pole owner ID as {poleOwnerID}";

            Request currentRequest = new Request(thisPoleType, poleType);
            // FIX 2b: Was `pendingRequests.Append(currentRequest)` — same Append/Add
            // issue as above. Nothing was being stored in pendingRequests.
            pendingRequests.Add(currentRequest);

            Debug.Log("Updating pending requests");
            Request serverRequest = new Request(thisPoleType, poleType);
            allRequestDataForServer.Add(serverRequest);
            hasPendingRequests = true;
            NotifyRequestSentClientRpc(poleType, thisClientRpcParams);
            SendRequestToSwitcherClientRpc(thisPoleType, poleType, clientRpcParams);
        }
        else
        {
            NotifyClientAboutThePoleClientRpc($"{poleType.ToString()} Pole doesnt have an owner", thisClientRpcParams);
        }
    }

    [ClientRpc]
    void NotifyClientAboutThePoleClientRpc(String message, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log(message);
        debugText.text = message;
    }

    [ServerRpc]
    void AcceptExhangeRequestServerRpc()
    {
        // FIX 3: The original code read allRequestDataForServer[0] on the debugText line
        // BEFORE the Count > 0 guard below, causing an IndexOutOfRangeException on an
        // empty list. The guard must come first.
        if (allRequestDataForServer.Count == 0)
        {
            Debug.Log("Rejected by server — no pending requests");
            return;
        }

        Request requestToProcess = allRequestDataForServer[0];
        debugText.text = $"Accepting request from {requestToProcess.SentByPoleType} to {requestToProcess.SentToPoleType}";
        Debug.Log($"Debug point : SwitcherRequestHandler : AcceptExhangeRequestServerRpc 1");

        string sentByPoleName = requestToProcess.SentByPoleType.ToString() + "Pole";
        string sentToPoleName = requestToProcess.SentToPoleType.ToString() + "Pole";

        Pole sentByPole = GameObject.Find(sentByPoleName)?.GetComponent<PoleScript>().thisPole;
        Pole sentToPole = GameObject.Find(sentToPoleName)?.GetComponent<PoleScript>().thisPole;
        if (sentByPole == null || sentToPole == null)
        {
            Debug.Log("One of the poles is missing or maybe both");
            return;
        }

        Switcher sentBySwitcher = sentByPole.GetOwner();
        Switcher sentToSwitcher = sentToPole.GetOwner();
        if (sentBySwitcher == null || sentToSwitcher == null)
        {
            Debug.Log("One of the switchers is missing or maybe both");
            return;
        }

        sentByPole.welcomeGuest(sentToSwitcher);
        sentToPole.welcomeGuest(sentBySwitcher);

        ulong sentBySwitcherID = sentBySwitcher.getClientID();
        ulong sentToSwitcherID = sentToSwitcher.getClientID();
        Debug.Log($"Debug point : SwitcherRequestHandler : AcceptExhangeRequestServerRpc 2");

        ClientRpcParams sentByRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sentBySwitcherID }
            }
        };
        ClientRpcParams sentToRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sentToSwitcherID }
            }
        };

        Debug.Log($"Debug point : SwitcherRequestHandler : AcceptExhangeRequestServerRpc 3");
        NotifyClientAboutThePoleClientRpc($"You are allied with {sentByPoleName} owner", sentToRpcParams);
        NotifyClientAboutThePoleClientRpc($"You are allied with {sentToPoleName} owner", sentByRpcParams);

        // FIX 3 (cont.): Remove the processed request so it isn't accepted again on the
        // next keypress. The original code left it at index 0 indefinitely.
        allRequestDataForServer.RemoveAt(0);
        if (allRequestDataForServer.Count == 0)
        {
            hasPendingRequests = false;
        }

        Debug.Log("Accepted by server");
        debugText.text = $"Accepted request from {sentByPoleName} to {sentToPoleName}";
    }


    public void AcceptRequest(PoleType poleType)
    {
        AcceptRequestServerRpc(poleType);
    }

    [ServerRpc(RequireOwnership = false)]
    void AcceptRequestServerRpc(PoleType poleType)
    {
        // GUARD 1: Self-check — if this acceptor is already allied, reject immediately
        if (alliedWithPoleType != PoleType.None)
        {
            Debug.Log("[AcceptRequestServerRpc] Acceptor is already in an alliance. Rejecting.");
            NotifyClientAboutThePoleClientRpc("You are already in an alliance.", thisClientRpcParams);
            pendingRequests.RemoveAll(r => r.SentByPoleType == poleType);
            return;
        }

        string sentByPoleName = poleType.ToString() + "Pole";
        string sentToPoleName = thisPoleType.ToString() + "Pole";

        Pole sentByPole = GameObject.Find(sentByPoleName)?.GetComponent<PoleScript>().thisPole;
        Pole sentToPole = GameObject.Find(sentToPoleName)?.GetComponent<PoleScript>().thisPole;

        if (sentByPole == null || sentToPole == null)
        {
            Debug.Log("[AcceptRequestServerRpc] One or both poles not found");
            return;
        }

        Switcher sentBySwitcher = sentByPole.GetOwner();
        Switcher sentToSwitcher = sentToPole.GetOwner();

        if (sentBySwitcher == null || sentToSwitcher == null)
        {
            Debug.Log("[AcceptRequestServerRpc] One or both switchers not found");
            return;
        }

        // GUARD 2: Sender-check — if the sender (A) is already allied with someone else, reject
        SwitcherRquestHandler senderHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.thisPoleType == poleType);

        if (senderHandler != null && senderHandler.alliedWithPoleType != PoleType.None)
        {
            Debug.Log($"[AcceptRequestServerRpc] Sender ({poleType}) is already allied. Rejecting late accept.");
            NotifyClientAboutThePoleClientRpc(
                $"{poleType} pole owner is already in an alliance. Request cancelled.",
                thisClientRpcParams
            );
            pendingRequests.RemoveAll(r => r.SentByPoleType == poleType);
            return;
        }

        // LOCK: Set alliance on BOTH handlers immediately — before any pole mutations.
        // This ensures the next queued RPC hits GUARD 2 above and is rejected cleanly.
        alliedWithPoleType = poleType;
        if (senderHandler != null)
            senderHandler.alliedWithPoleType = thisPoleType;

        // Now safe to mutate pole state
        sentByPole.welcomeGuest(sentToSwitcher);
        sentToPole.welcomeGuest(sentBySwitcher);

        ulong sentBySwitcherID = sentBySwitcher.getClientID();
        ulong sentToSwitcherID = sentToSwitcher.getClientID();
        Debug.Log($"Debug point : SwitcherRequestHandler : AcceptRequestServerRpc 2");

        ClientRpcParams sentByRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sentBySwitcherID }
            }
        };
        ClientRpcParams sentToRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { sentToSwitcherID }
            }
        };

        Debug.Log($"Debug point : SwitcherRequestHandler : AcceptRequestServerRpc 3");
        NotifyClientAboutThePoleClientRpc($"You are allied with {sentByPoleName} owner", sentToRpcParams);
        NotifyClientAboutThePoleClientRpc($"You are allied with {sentToPoleName} owner", sentByRpcParams);
        PerformRoutineAfterAllianceClientRpc(sentToRpcParams);
        PerformRoutineAfterAllianceClientRpc(sentByRpcParams);
        ClearSentRequestsForAllianceClientRpc(thisPoleType, poleType);
        // Remove the matching request from allRequestDataForServer
        Request matchingRequest = allRequestDataForServer
            .FirstOrDefault(r => r.SentByPoleType == poleType && r.SentToPoleType == thisPoleType);
        if (matchingRequest != null)
        {
            allRequestDataForServer.Remove(matchingRequest);
        }
        if (allRequestDataForServer.Count == 0)
        {
            hasPendingRequests = false;
        }

        // Cancel all outgoing requests from both sides and notify their targets
        CancelPendingRequestsOf(this);
        if (senderHandler != null)
            CancelPendingRequestsOf(senderHandler);

        // Clear both request panels and show break button
        ClearRequestPanelClientRpc(sentToRpcParams);
        ClearRequestPanelClientRpc(sentByRpcParams);
        ShowBreakPartnershipClientRpc(sentToRpcParams);
        ShowBreakPartnershipClientRpc(sentByRpcParams);

        Debug.Log($"[AcceptRequestServerRpc] Accepted request from {sentByPoleName} to {sentToPoleName}");
    }

    void CancelPendingRequestsOf(SwitcherRquestHandler handler)
    {
        foreach (Request req in handler.allRequestDataForServer)
        {
            string targetPoleName = req.SentToPoleType.ToString() + "Pole";
            Pole targetPole = GameObject.Find(targetPoleName)?.GetComponent<PoleScript>().thisPole;
            if (targetPole != null && targetPole.HasOwner())
            {
                ulong targetOwnerID = targetPole.GetOwner().getClientID();
                ClientRpcParams targetRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { targetOwnerID }
                    }
                };
                RemoveRequestFromPanelClientRpc(req.SentByPoleType, targetRpcParams);
            }
        }
        handler.allRequestDataForServer.Clear();
        handler.pendingRequests.Clear();
    }

    [ClientRpc]
    void RemoveRequestFromPanelClientRpc(PoleType fromPoleType, ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);
        if (localOwnerHandler?.switcherUIFunctions != null)
            localOwnerHandler.switcherUIFunctions.RemoveRequestFromPanel(fromPoleType);

    }

    [ClientRpc]
    void ClearRequestPanelClientRpc(ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);
        if (localOwnerHandler?.switcherUIFunctions != null)
            localOwnerHandler.switcherUIFunctions.ClearRequestPanel();
    }

    [ClientRpc]
    void ShowBreakPartnershipClientRpc(ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);
        if (localOwnerHandler?.switcherUIFunctions != null)
            localOwnerHandler.switcherUIFunctions.ShowBreakPartnershipButton();
    }

    [ClientRpc]
    void HideBreakPartnershipClientRpc(ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);
        if (localOwnerHandler?.switcherUIFunctions != null)
            localOwnerHandler.switcherUIFunctions.HideBreakPartnershipButton();
    }

    public void BreakPartnership()
    {
        BreakPartnershipServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void BreakPartnershipServerRpc()
    {
        if (alliedWithPoleType == PoleType.None)
        {
            Debug.Log("[BreakPartnershipServerRpc] No active alliance to break");
            return;
        }

        string ownPoleName = thisPoleType.ToString() + "Pole";
        string alliedPoleName = alliedWithPoleType.ToString() + "Pole";

        Pole ownPole = GameObject.Find(ownPoleName)?.GetComponent<PoleScript>().thisPole;
        Pole alliedPole = GameObject.Find(alliedPoleName)?.GetComponent<PoleScript>().thisPole;

        Switcher ownSwitcher = ownPole?.GetOwner();
        Switcher alliedSwitcher = alliedPole?.GetOwner();

        // Only evict the guest if they are still the alliance partner
        if (ownPole != null && alliedSwitcher != null)
            ownPole.SendOffGuestIfTheyAre(alliedSwitcher);

        if (alliedPole != null && ownSwitcher != null)
            alliedPole.SendOffGuestIfTheyAre(ownSwitcher);


        SwitcherRquestHandler partnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.thisPoleType == alliedWithPoleType);

        ulong partnerClientId = alliedPole?.GetOwner()?.getClientID() ?? 0;
        ClientRpcParams partnerRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { partnerClientId } }
        };

        Debug.Log($"[BreakPartnershipServerRpc] Breaking alliance between {thisPoleType} and {alliedWithPoleType}");

        HideBreakPartnershipClientRpc(thisClientRpcParams);
        HideBreakPartnershipClientRpc(partnerRpcParams);

        alliedWithPoleType = PoleType.None;
        if (partnerHandler != null)
            partnerHandler.alliedWithPoleType = PoleType.None;
    }

    public void BreakPartnershipIfAllied()
    {
        if (alliedWithPoleType == PoleType.None) return;

        string ownPoleName = thisPoleType.ToString() + "Pole";
        string alliedPoleName = alliedWithPoleType.ToString() + "Pole";

        Pole ownPole = GameObject.Find(ownPoleName)?.GetComponent<PoleScript>().thisPole;
        Pole alliedPole = GameObject.Find(alliedPoleName)?.GetComponent<PoleScript>().thisPole;

        if (ownPole != null) ownPole.SendOffTheGuest();
        if (alliedPole != null) alliedPole.SendOffTheGuest();

        SwitcherRquestHandler partnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.thisPoleType == alliedWithPoleType);

        ulong partnerClientId = alliedPole?.GetOwner()?.getClientID() ?? 0;
        ClientRpcParams partnerRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { partnerClientId } }
        };

        HideBreakPartnershipClientRpc(thisClientRpcParams);
        HideBreakPartnershipClientRpc(partnerRpcParams);

        // Clear both sides so the second call (from victim's handler) is a no-op
        alliedWithPoleType = PoleType.None;
        if (partnerHandler != null)
            partnerHandler.alliedWithPoleType = PoleType.None;
    }

    public bool IsAllied()
    {
        return alliedWithPoleType != PoleType.None;
    }

    [ClientRpc]
    void NotifyRequestSentClientRpc(PoleType poleType, ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None)
            .FirstOrDefault(h => h.IsOwner);

        if (localOwnerHandler?.switcherUIFunctions != null)
        {
            localOwnerHandler.switcherUIFunctions.OnRequestSentSuccessfully(poleType);
        }
    }

    [ClientRpc]
    void PerformRoutineAfterAllianceClientRpc(ClientRpcParams clientRpcParams = default)
    {
        SwitcherRquestHandler localOwnerHandler = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None).FirstOrDefault(h => h.IsOwner);
        if (localOwnerHandler?.switcherUIFunctions != null)
        {
            localOwnerHandler.switcherUIFunctions.ClearSentRequestPoleTypesList();
        }
    }

    [ClientRpc]
    void ClearSentRequestsForAllianceClientRpc(PoleType poleA, PoleType poleB)
    {
        var localHandler = SwitcherRquestHandler.LocalOwnerInstance;

        if (localHandler?.switcherUIFunctions != null)
        {
            localHandler.switcherUIFunctions.RemovePoleFromSentList(poleA);
            localHandler.switcherUIFunctions.RemovePoleFromSentList(poleB);
        }
    }

    [ClientRpc]
    public void ClearSentRequestsAfterStealClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (SwitcherRquestHandler.LocalOwnerInstance?.switcherUIFunctions != null)
        {
            SwitcherRquestHandler.LocalOwnerInstance
                .switcherUIFunctions
                .ClearSentRequestPoleTypesList();
        }
    }
    [ClientRpc]
    void DestroySendRequestBtnForAPoleTypeClientRpc(PoleType poletype,ClientRpcParams clientRpcParams = default)
    {
        if (SwitcherRquestHandler.LocalOwnerInstance?.switcherUIFunctions != null)
        {
            SwitcherRquestHandler.LocalOwnerInstance.switcherUIFunctions.DestroySendRequestBtnForPoleType(poletype);
        }
        debugText.text = $"The {poletype.ToString()} pole has been destroyed. You can no longer send requests to its owner.";
        DestroyedPoles.Add(poletype);
        return;
    }
    public void RequestHandlerRoutineAfterPoleDestroy()
    {
        if (alliedWithPoleType != PoleType.None)
        {
            BreakPartnershipIfAllied();
        }
        else
        {
            CancelPendingRequestsOf(this);
            
        }
        
        DestroySendRequestBtnForAPoleTypeClientRpc(thisPoleType);
    }
    public void PlayCatcherDeathSequence()
    {
        switcherCanvasUIEffects.PlaySequence();
    }
}

