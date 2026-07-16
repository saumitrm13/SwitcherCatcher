using System;
using UnityEngine;
using Unity.Netcode;
using NUnit.Framework.Constraints;
using Unity.Mathematics;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using System.Linq;
using Unity.Networking.Transport.Error;
public class SwitcherScript : NetworkBehaviour
{
    List<PoleScript> Poles;
    TextMeshProUGUI debugText;
    TextMeshProUGUI ownedPoleText;
    TextMeshProUGUI triggerCaseText;
    public static SwitcherScript localOwnerInstance;
    public NetworkVariable<bool> isInSafeZone = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public Switcher thisSwitcher {  get; set; }
    internal bool hasNecessaryResource = false;
    public static event Action OnSwitcherPoleAssigned;
    public static event Action OnSwitcherPoleAssignedClientSignal;
    public static event Action OnPoleOwnershipChanged;
    public NetworkVariable<bool> isEligibleToStealNet =
    new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); 
    bool serverSignalToSteal = true;
    bool isStealingNow = false;

    Pole currentPole;
    Transform currentColliderTransform;
    DangerVisuals dangerVisuals;
    ClientRpcParams clientRpcParams = new ClientRpcParams();
    public GameObject thisSwitcherPointer;
    [Header("Particle Systems")]
    [SerializeField] ParticleSystem successVFX;
    [SerializeField] ParticleSystem wrongPoleEntryAttackVFX;
    [SerializeField] ParticleSystem resourceGainVFX;
    [SerializeField] GameObject throwableMagic;

    
    [Header("Task Timer")]
    [SerializeField] float taskTimeLimit = 30f;   // seconds � tweak in Inspector
    [Header("Resources Visuals")]
    [SerializeField] GameObject resourceVisualsParent; // parent object that holds the resource visuals
    
    Coroutine taskTimerCoroutine;
    public NetworkVariable<PoleType> ownedPoleType = new NetworkVariable<PoleType>(
        PoleType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<PoleType> targetPoleType = new NetworkVariable<PoleType>(
        PoleType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isCompletingATask = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public override void OnNetworkSpawn()
    {   
        Debug.Log("Switcher assigned");
        thisSwitcher = new Switcher(OwnerClientId,this);
        
        GetAllPoles();
        debugText = GameObject.Find("DebugText").GetComponent<TextMeshProUGUI>();
        ownedPoleText = GameObject.Find("DebugText3").GetComponent<TextMeshProUGUI>();
        triggerCaseText = GameObject.Find("TriggerCaseText").GetComponent<TextMeshProUGUI>();
        GameStartManager.OnRoundEnded += SwitcherScriptRoutineAfterRoundEnd;
        clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };

        if (IsServer)
        {
            CatcherScript.cursedPoleType.OnValueChanged += OnCursedPoleChanged;
        }
        if (!IsOwner)
        {
            thisSwitcherPointer.SetActive(false);
        }
        else
        {
            localOwnerInstance = this;
        }
    }

    private void SwitcherScriptRoutineAfterRoundEnd()
    {
        if(!IsServer) return;
        StopTaskTimer();
        SetResourceVisualsClientRpc(false, clientRpcParams);

    }

    private void OnDisable()
    {
        if (IsServer)
            CatcherScript.cursedPoleType.OnValueChanged -= OnCursedPoleChanged;
    }
    private void OnCursedPoleChanged(PoleType oldValue, PoleType newValue)
    {
        UpdateStealEligibility();
    }
    void Start()
    {
        Debug.Log($"You are in safe zone : {isInSafeZone}");

    }

    
    void Update()
    {
        if (IsOwner)
        {
            if (isEligibleToStealNet.Value && Input.GetKeyDown(KeyCode.V) && !isStealingNow)
            {   

                isStealingNow = true;
                StartCoroutine(StartStealing());
            }
        }
    }

    public void GetAllPoles()
    {   

        Poles = new List<PoleScript>(FindObjectsByType<PoleScript>(FindObjectsSortMode.None));
        Debug.Log($"[SwitcherScript] Found {Poles.Count} poles in the scene.");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled)
            return;
        
        if (!other.gameObject.CompareTag("Pole"))
        {
            return;
        }
        currentColliderTransform = other.gameObject.transform;
        if (!IsServer) { return; }
        if (other.gameObject.CompareTag("Pole")) {
            isInSafeZone.Value = true;
            Debug.Log("You are in safe zone");
            currentPole = other.gameObject.GetComponent<PoleScript>().thisPole;
            currentColliderTransform = other.gameObject.transform;
            
            if (!thisSwitcher.OwnsAPole())
            {
                triggerCaseText.text = $"Pole Entry Case : Doesn't own a pole";
                Debug.Log("[Switcher Script : OnTriggerEnter] Handling ownership");
                HandlePoleOwnerShip(currentPole);
                return;
            }
            else if (thisSwitcher.getOwnedPoleType() == currentPole.Type)
            {
                Debug.Log("[Switcher Script : OnTriggerEnter] This is the owned pole...so checking resources");
                if (hasNecessaryResource)
                {
                    triggerCaseText.text = $"Pole Entry Case : Owned pole with resources";
                    hasNecessaryResource = false;
                    SetResourceVisualsClientRpc(false, clientRpcParams);    
                    thisSwitcher.AssignTargetPole(null);
                    StopTaskTimer();
                    ScoreManager.Instance?.AddSwitcherSaveScore(OwnerClientId);
                   
                    //NotifyClientAboutThePoleClientRpc($"Well done! You handled the situation", false, clientRpcParams);
                    RoutineAfterGettingResourcesToOwnedPoleClientRpc(clientRpcParams);
                    StartCoroutine(WaitAndAssignNextPole());

                }
                else
                {
                    triggerCaseText.text = $"Pole Entry Case : Owned pole without resources";
                    NotifyClientAboutThePoleClientRpc($"You came back...but without resources!", false, clientRpcParams);
                }
                thisSwitcher.SetCurrentOccupiedPole(currentPole);
                currentPole.Occupy();
                PlaySuccessVFXClientRpc(clientRpcParams);
                return;
            }else if(thisSwitcher.getTargetPoleType() != currentPole.Type)
            {
                InvalidPoleEntryRoutineClientRpc(clientRpcParams);
            }
            else
            {
                HandleStrangerEntryInThePole();
                return;
            }
           
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) { return; }
        if (other.gameObject.CompareTag("Pole"))
        {
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            };
           
           
            Pole exitedPole = other.gameObject.GetComponent<PoleScript>().thisPole;
            if (thisSwitcher.hasOccupiedAPole())
            {
                isInSafeZone.Value = false;
                Debug.Log("[Switcher Script : OnTriggerExit] You are out of the safe zone");
                if (thisSwitcher.getOwnedPoleType().Equals(exitedPole.Type))
                {
                    Debug.Log("[Switcher Script : OnTriggerExit] You are leaving home...come back soon!");
                    exitedPole.Vacate();
                }
                else
                {
                    Debug.Log("[Switcher Script : OnTriggerExit] Bye bye guest");
                    exitedPole.SendOffTheGuest();
                }
                thisSwitcher.FreeCurrentOccupiedPole();
                Debug.Log("Vacated current pole");
            }
        }
    }


    [ClientRpc]
    void NotifyClientAboutThePoleClientRpc(String message, bool assignPole = false, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log(message);
        debugText.text = message;
        if (assignPole) {
           
            
            OnSwitcherPoleAssignedClientSignal?.Invoke();
            StartCoroutine(waitAndAssignTargetPole());
        }
    }

    void HandlePoleOwnerShip(Pole currentPole)
    {
        Debug.Log("Attempting to assign the pole");
        

        if (currentPole.HasOwner())
        {
            NotifyClientAboutThePoleClientRpc("This pole is already owned", false, clientRpcParams);
        }
        else
        {
            ProcessAfterOwningAPoleClientRpc(clientRpcParams);
            thisSwitcher.AssignPole(currentPole);
            ownedPoleType.Value = currentPole.Type;
            currentPole.AssignOwner(thisSwitcher);
            OnSwitcherPoleAssigned?.Invoke();
            ChangeClientUIClientRpc(currentPole.Type, clientRpcParams);
            UpdateStealEligibility();
            NotifyClientAboutThePoleClientRpc("Great!... You own a Pole", true, clientRpcParams);
            
            PlaySuccessVFXClientRpc(clientRpcParams);
            
        }
    }

    IEnumerator waitAndAssignTargetPole()
    {
        yield return new WaitForSeconds(15);
        AssignTargetPoleToSwitcherServerRpc();
    }
    [ServerRpc]
    void AssignTargetPoleToSwitcherServerRpc()
    {   
        Debug.Log("Total available poles count : " + Poles.Count);  
        if(Poles.Count <= 1)
        {   
            string notification = "No poles available to assign as target pole. Please wait for the next round.";
            NotifyClientAboutThePoleClientRpc(notification, false, clientRpcParams);
            return;
        }
        Pole targetPole = Poles[UnityEngine.Random.Range(0, Poles.Count)].thisPole;

        if(Poles.Count == 1 && targetPole.Type == thisSwitcher.getOwnedPoleType())
        {
            string notification = "No poles available to assign as target pole. Please wait for the next round.";
            NotifyClientAboutThePoleClientRpc(notification, false, clientRpcParams);
            return;
        }
        while (targetPole.Type == thisSwitcher.getOwnedPoleType())
        {
            targetPole = Poles[UnityEngine.Random.Range(0, Poles.Count)].thisPole;

        }
        
        if (targetPole == null)
        { 
            return;
        }
        else
        { 
            if(thisSwitcher == null)
            { 
                return;
            }
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            };
            thisSwitcher.AssignTargetPole(targetPole);
            string notification = $"Your pole has a problem. Get to {targetPole.Type} pole quickly to gather resources and come back";
            targetPoleType.Value = targetPole.Type;
            
            NotifyClientAboutThePoleClientRpc(notification, false, clientRpcParams);
            isCompletingATask.Value = true; 
            ShowProblemToPlayerClientRpc();
            StartTaskTimer();  
           
            return;
        }
    }

    void ValidateCurrentPoleAndNotifySwitcher(Pole currentPole, bool isSnatched = false)
    {
        string message = "";
        if (currentPole.Type == thisSwitcher.getTargetPoleType())
        {
            if (isSnatched)
            {
                message = "You snatched the right pole";
            }
            else
            {
                message = $"Perfect ! you found the right pole";
            }
            PlayResourceGainVFXClientRpc(clientRpcParams);
            hasNecessaryResource = true;
            SetResourceVisualsClientRpc(true, clientRpcParams);
            targetPoleType.Value = PoleType.None;
        }
        else
        {
            if (!isSnatched)
            {
                message = $"You are at {currentPole.Type} pole and you need to be at {thisSwitcher.getTargetPoleType()} pole";
            }
            else
            {
                message = $"Good snatch...but this won't solve your problem!!! but getting to {thisSwitcher.getTargetPoleType()} pole might !";
            }
            PlaySuccessVFXClientRpc(clientRpcParams);
        }
        thisSwitcher.SetCurrentOccupiedPole(currentPole);
        
        NotifyClientAboutThePoleClientRpc(message, false, clientRpcParams);
    }

    void NonServerRpcAssignTargetPoleToSwitcher()
    {
        Pole targetPole = Poles[UnityEngine.Random.Range(0, Poles.Count)].thisPole;
        Debug.Log("Total available poles count : " + Poles.Count);
        while (targetPole.Type == thisSwitcher.getOwnedPoleType())
        {
            targetPole = Poles[UnityEngine.Random.Range(0, Poles.Count)].thisPole;
        }

        if (targetPole == null)
        {
            return;
        }
        else
        {
            if (thisSwitcher == null)
            {
                return;
            }
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            };
            thisSwitcher.AssignTargetPole(targetPole);
            targetPoleType.Value = targetPole.Type;
            string notification = $"Your pole has a problem. Get to {targetPole.Type} pole quickly to gather resources and come back";
            NotifyClientAboutThePoleClientRpc(notification, false, clientRpcParams);
            ShowProblemToPlayerClientRpc(clientRpcParams);
            StartTaskTimer();
            return;
        }
    }
    IEnumerator WaitAndAssignNextPole()
    {   

        yield return new WaitForSeconds(15);
        NonServerRpcAssignTargetPoleToSwitcher();
        
    }

   

    void HandleStrangerEntryInThePole()
    {

        if (currentPole.isThisGuestAllowed(thisSwitcher))
        {
            Debug.Log($"[SwitcherScript] : The switcher that owns {thisSwitcher.getOwnedPoleType()} pole is allowed at the {currentPole.Type} pole");
            ValidateCurrentPoleAndNotifySwitcher(currentPole, false);
            // currentPole.Occupy();
            currentPole.AllowGuestToComeIn();
            thisSwitcher.SetCurrentOccupiedPole(currentPole);
        }
        else if (currentPole.IsCurrentlyOccupied() && !currentPole.IsDestroyed())
        {
            NotifyClientAboutThePoleClientRpc($"You can't just barge in!. Find a free pole", false, clientRpcParams);
            Debug.Log($"[SwitcherScript] : The switcher that owns {thisSwitcher.getOwnedPoleType()} pole is not allowed at the {currentPole.Type} pole");
            isInSafeZone.Value = false;
            InvalidPoleEntryRoutineClientRpc(clientRpcParams);
        }
        else
        {
            if (currentPole.IsPoleReadyToBeSnatched())
            {
                Debug.Log($"[Switcher Script : OnTriggerEnter] Pole is now going to be snatched");
                triggerCaseText.text = $"Pole Entry Case : Snatch logic";
                
                // Get the pole owner before snatch to break their partnership
                Switcher poleOwner = currentPole.GetOwner();
                
                currentPole.SnatchPole(thisSwitcher);

                // Break the previous owner's partnership after snatch
                if (poleOwner != null)
                {
                    var allHandlers = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None);
                    SwitcherRquestHandler poleOwnerHandler = allHandlers
                        .FirstOrDefault(h => h.OwnerClientId == poleOwner.getClientID());
                    poleOwnerHandler?.BreakPartnershipIfAllied();
                }

                ValidateCurrentPoleAndNotifySwitcher(currentPole, true);
                thisSwitcher.SetCurrentOccupiedPole(currentPole);
            }
            else
            {
                NotifyClientAboutThePoleClientRpc($"This pole is already occupied by a guest or a snatcher", false, clientRpcParams);
                triggerCaseText.text = $"Pole Entry Case : Not snathable";
                InvalidPoleEntryRoutineClientRpc(clientRpcParams);
            }
        }
       
    }

    [ClientRpc]
    void ChangeClientUIClientRpc(PoleType poleType ,ClientRpcParams clientRpcParams = default)
    {
        ownedPoleText.text = $"Your pole : {poleType.ToString()}";
    }

   

    public string ResolvePoleEntry(Pole currentPole)
    {
        if (!thisSwitcher.OwnsAPole())
            return "ownership";

        if (thisSwitcher.getOwnedPoleType() == currentPole.Type)
            return hasNecessaryResource
                ? "owned_with_resources"
                : "owned_without_resources";

        // Mirror HandleStrangerEntryInThePole ordering:
        // guest check FIRST, before checking occupation
        if (currentPole.isThisGuestAllowed(thisSwitcher))
            return "allowed_guest";

        if (currentPole.IsCurrentlyOccupied())
            return "denied_guest";

        return currentPole.IsPoleReadyToBeSnatched()
            ? "snatch"
            : "blocked_snatch";
    }



    void UpdateStealEligibility()
    {
        if (!IsServer) return;
        if (thisSwitcher == null || !thisSwitcher.OwnsAPole()) return;
        isEligibleToStealNet.Value =
            (ownedPoleType.Value == CatcherScript.cursedPoleType.Value);
        Debug.Log($"{thisSwitcher.getOwnedPoleType().ToString()} pole owner's eligibility to steal : {isEligibleToStealNet.Value}");
    }

    void StartTaskTimer()
    {
        if (!IsServer) return;
        if (taskTimerCoroutine != null) StopCoroutine(taskTimerCoroutine);
        taskTimerCoroutine = StartCoroutine(TaskTimerCoroutine());
    }

    public void StopTaskTimer()
    {
        if (!IsServer) return;
        isCompletingATask.Value = false;    
        if (taskTimerCoroutine != null)
        {
            StopCoroutine(taskTimerCoroutine);
            taskTimerCoroutine = null;
        }
        HideTimerClientRpc(clientRpcParams);

    }

    IEnumerator TaskTimerCoroutine()
    {   
        
        int remaining = Mathf.CeilToInt(taskTimeLimit);
        while (remaining > 0)
        {
            UpdateTimerClientRpc(remaining, clientRpcParams);
            yield return new WaitForSeconds(1f);
            remaining--;
        }

        UpdateTimerClientRpc(0, clientRpcParams);
        taskTimerCoroutine = null;

        DestroyPoleRoutine();
    }

    void DestroyPoleRoutine()
    {
        // TODO: fill in destruction / penalty logic
        Debug.Log($"[SwitcherScript] Timer expired for {thisSwitcher.getOwnedPoleType()} pole owner!");
        Pole destroyedPole = thisSwitcher.getOwnedPole();
        PoleType destroyedPoleType = destroyedPole.Type;
        ExplodePoleClientRpc(destroyedPoleType);
        RoutineAfterOwnedPoleDestroyClientRpc(clientRpcParams);
        destroyedPole.DestroyPole();
        RemoveDestroyedPoleFromListClientRpc(destroyedPoleType);
        GetComponent<SwitcherRquestHandler>().RequestHandlerRoutineAfterPoleDestroy();
    }

    void RemoveDestroyedPoleFromList(Pole destroyedPole)
    {
        PoleScript poleScriptToRemove = Poles.FirstOrDefault(p => p.thisPole == destroyedPole);
        if (poleScriptToRemove != null)
        {
            Poles.Remove(poleScriptToRemove);
            Debug.Log($"[SwitcherScript] Removed destroyed {destroyedPole.Type} pole from active poles list. Remaining poles: {Poles.Count}");
        }
    }

    [ClientRpc]
    void RemoveDestroyedPoleFromListClientRpc(PoleType destroyedPoleType)
    {
        PoleScript poleScriptToRemove = Poles.FirstOrDefault(p => p.thisPole.Type == destroyedPoleType);
        if (poleScriptToRemove != null)
        {
            Poles.Remove(poleScriptToRemove);
            Debug.Log($"[SwitcherScript] Removed destroyed {destroyedPoleType} pole from active poles list on all clients. Remaining poles: {Poles.Count}");
        }
    }

 

    [ClientRpc]    void ExplodePoleClientRpc(PoleType poleType)
    {
        string poleName = poleType.ToString() + "Pole";
        GameObject poleObj = GameObject.Find(poleName);
        if (poleObj == null) return;

        poleObj.GetComponent<Animator>().enabled = true;
    }

    [ClientRpc]
    void UpdateTimerClientRpc(int secondsRemaining, ClientRpcParams clientRpcParams = default)
    {   
        
        var handler = SwitcherRquestHandler.LocalOwnerInstance;
        if (handler?.switcherUIFunctions != null)
            handler.switcherUIFunctions.ShowTimeRemaining(secondsRemaining);
    }

    [ClientRpc]
    void HideTimerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        var handler = SwitcherRquestHandler.LocalOwnerInstance;
        if (handler?.switcherUIFunctions != null)
            handler.switcherUIFunctions.HideTimer();
    }
    IEnumerator StartStealing()
    {
        float time = 0;
        while(serverSignalToSteal && time < 5f)
        {
            ManageServerSignalToStealServerRpc();
            yield return new WaitForSeconds(1f);
            time += 1;
        }
        if (!serverSignalToSteal)
        {
            debugText.text = "Sorry you couldn't steal this pole";
            serverSignalToSteal = true;
        }
        else
        {
            StealPoleServerRpc();
        }
        isStealingNow = false;
    }

    [ServerRpc]
    void ManageServerSignalToStealServerRpc()
    {
        if (currentPole != null)
        {
            if (currentPole.IsCurrentlyOccupied())
            {
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { OwnerClientId }
                    }
                };
                ManageClientSignalToStealClientRpc(clientRpcParams);
            }
        }
    }

    [ServerRpc]
    void StealPoleServerRpc()
    {
        if (currentPole != null)
        {   
            PoleType thisSwitcherPoleType = thisSwitcher.getOwnedPoleType();
            
            Pole thisSwitcherPole = GameObject.Find(thisSwitcherPoleType.ToString() + "Pole") ?.GetComponent<PoleScript>().thisPole;
            

            Switcher victimSwitcher = currentPole.GetOwner();
            PoleType thisSwitcherTargetPoleType = thisSwitcher.getTargetPoleType();
            thisSwitcher.AssignTargetPole(GameObject.Find(victimSwitcher.getTargetPoleType().ToString() + "Pole")?.GetComponent<PoleScript>().thisPole);
            victimSwitcher.AssignTargetPole(GameObject.Find(thisSwitcherTargetPoleType.ToString() + "Pole")?.GetComponent<PoleScript>().thisPole);
            thisSwitcherPole.ChangeOwner(currentPole.GetOwner());
            victimSwitcher.ChangeOwnedPole(thisSwitcherPole);
            currentPole.ChangeOwner(thisSwitcher);
            thisSwitcher.ChangeOwnedPole(currentPole);

            // Break partnerships for both switchers if they have any
            var allHandlers = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None);

            SwitcherRquestHandler thisSwitcherHandler = allHandlers
                .FirstOrDefault(h => h.OwnerClientId == thisSwitcher.getClientID());
            SwitcherRquestHandler victimHandler = allHandlers
                .FirstOrDefault(h => h.OwnerClientId == victimSwitcher.getClientID());
            // SwitcherScript.cs � inside StealPoleServerRpc, after BreakPartnership calls

            thisSwitcherHandler?.UpdatePoleType(thisSwitcher.getOwnedPoleType());   // thief's new pole
            victimHandler?.UpdatePoleType(victimSwitcher.getOwnedPoleType());        // victim's new pole
            thisSwitcherHandler?.BreakPartnershipIfAllied();
            // victimHandler call is safe � if thief had no alliance, it's a no-op;
            // if victim was allied WITH the thief, alliedWithPoleType is already None after the first call
            victimHandler?.BreakPartnershipIfAllied();

            ulong victimSwitcherId = victimSwitcher.getClientID();

            ClientRpcParams victimClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { victimSwitcherId }
                }
            };

            NotifyClientAboutThePoleClientRpc($"Your pole was stolen and your new target pole is {victimSwitcher.getTargetPoleType().ToString()}", false, victimClientRpcParams);
            NotifyClientAboutThePoleClientRpc($"You stole this pole and your new target pole is {thisSwitcher.getTargetPoleType().ToString()}", false,clientRpcParams);
           
            ChangeClientUIClientRpc(thisSwitcher.getOwnedPoleType(), clientRpcParams);
            ChangeClientUIClientRpc(victimSwitcher.getOwnedPoleType(),victimClientRpcParams);
           
            
            thisSwitcherHandler?.ClearSentRequestsAfterStealClientRpc(clientRpcParams);
            victimHandler?.ClearSentRequestsAfterStealClientRpc(victimClientRpcParams);
            // Thief
            ownedPoleType.Value = thisSwitcher.getOwnedPoleType();
            targetPoleType.Value = thisSwitcher.getTargetPoleType();
            StopTaskTimer();
            // Victim
            var victimScript = victimSwitcher.getSwitcherScriptRef();
            victimScript.StopTaskTimer();
            victimScript.ownedPoleType.Value = victimSwitcher.getOwnedPoleType();
            victimScript.targetPoleType.Value = victimSwitcher.getTargetPoleType();
            UpdateStealEligibility();              // thief
            victimScript.UpdateStealEligibility(); // victim
            victimScript.StopTaskTimer();
            //victimSwitcher.getSwitcherScriptRef().RoutineAfterBeingStealVictiomClientRpc(victimClientRpcParams);
        }
    }

    [ClientRpc]
    void ManageClientSignalToStealClientRpc(ClientRpcParams clientRpcParams = default)
    {
        serverSignalToSteal = false;
    }

    [ClientRpc]
    void PlaySuccessVFXClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (successVFX != null)
            successVFX.Play();  
    }

    [ClientRpc]
    void PlayResourceGainVFXClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if(resourceGainVFX != null) resourceGainVFX.Play();
        dangerVisuals.ShakeCam();
    }

    [ClientRpc]
    void InvalidPoleEntryRoutineClientRpc(ClientRpcParams clientRpcParams = default) {
        GetComponent<AnimationAndMovementControllerNetwork>().TakeAFall();
        if (wrongPoleEntryAttackVFX != null)
        {   
            
            wrongPoleEntryAttackVFX.Play();
        }
        
    }

   
    [ClientRpc]
    void RoutineAfterOwnedPoleDestroyClientRpc(ClientRpcParams clientRpcParams = default) {
        GetComponent<AnimationAndMovementControllerNetwork>().enabled = false;
        GetComponent<Animator>().SetTrigger("Die");
    }

    [ClientRpc]
    void SetResourceVisualsClientRpc(bool active, ClientRpcParams clientRpcParams = default)
    {
        if (resourceVisualsParent != null)
            resourceVisualsParent.SetActive(active);
    }

    [ClientRpc]
    void RoutineAfterGettingResourcesToOwnedPoleClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // NotifyClientAboutThePoleClientRpc($"You got the resources! Now get back to your pole : {thisSwitcher.getOwnedPoleType().ToString()}", false, clientRpcParams);
        debugText.text = $"Well done! You handled the situation";
        currentColliderTransform.Find("ResourceParent").gameObject.SetActive(true);
        //GetComponent<SwitcherRquestHandler>().PlayCatcherDeathSequence();
        dangerVisuals.StopShowingTheProblem();

    }

    [ClientRpc]
    void ProcessAfterOwningAPoleClientRpc(ClientRpcParams clientRpcParams = default) {
        GameObject problemVisuals = currentColliderTransform.Find("ProblemVisual").gameObject;
        dangerVisuals = problemVisuals.GetComponent<DangerVisuals>();

        GetComponent<AnimationAndMovementControllerNetwork>().PassFLCamDataToVisuals(dangerVisuals,throwableMagic);

    }

    [ClientRpc]
    void ShowProblemToPlayerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        dangerVisuals.ShowProblem();
    }
    //[ServerRpc]
    //void RoutineAfterBeingStealVictimServerRpc()
    //{
    //    hasNecessaryResource = false;
    //    NotifyClientAboutThePoleClientRpc($"Your pole was stolen. And your new target pole is : {thisSwitcher.getTargetPoleType().ToString()}",false,clientRpcParams);
    //    ownedPoleType.Value = thisSwitcher.getOwnedPoleType();
    //    targetPoleType.Value = thisSwitcher.getTargetPoleType();

    //}

    //[ClientRpc]
    //public void RoutineAfterBeingStealVictiomClientRpc(ClientRpcParams clientRpcParams = default)
    //{

    //    RoutineAfterBeingStealVictimServerRpc();
    //}


    public float GetTaskTimeLimit()
    {
        return taskTimeLimit;
    }   

}
