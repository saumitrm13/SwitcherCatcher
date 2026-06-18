using UnityEngine;
using Unity.Netcode;
using System.Linq;
using System;
using DG.Tweening;
using System.Collections;

public class CatcherScript : NetworkBehaviour
{
    Catcher catcher;
   
    public static NetworkVariable<PoleType> cursedPoleType = new NetworkVariable<PoleType>(PoleType.None);
    [SerializeField] GameObject catcherCanvas;
    [SerializeField] GameObject magicInCatcherHand;
   
    [SerializeField] AnimationAndMovementControllerNetwork movementControllerNetwork;
    [SerializeField] float powerDuration = 10f;
    [SerializeField] float powerRechargeDuration = 5f;

    Vector3 magicLocalPosition = new Vector3();
    ClientRpcParams thisClientRpcParams;
    Animator animator;
    GameObject currentSwitcherInRange;
    NetworkVariable<bool> hasPowers = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
    Coroutine powerDrainCoroutine;
    Coroutine powerRechargeCoroutine;
    

    private void Awake()
    {
        catcher = new Catcher();
        magicLocalPosition = magicInCatcherHand.transform.localPosition;
        
       
    }

    private void Start()
    {
        if (IsServer)
        {
            hasPowers.Value = true;
            Debug.Log("Catcher has powers: " + hasPowers.Value);
            StartPowerDrainTimer();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            catcherCanvas.SetActive(false);
           
            
        }
        thisClientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
        if (IsOwner)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled)
            return;
        if(other.gameObject.CompareTag("PowerSource") && IsServer)
        {   
            if(powerRechargeCoroutine != null) { StopCoroutine(powerRechargeCoroutine); powerRechargeCoroutine = null; }
            HandlePowerSourceEnter();
            return;
        }

        if (!other.gameObject.CompareTag("Switcher")){return;}
        currentSwitcherInRange = other.gameObject;
        if (!hasPowers.Value) {return;}
        if (!IsServer) { return; }
        Debug.Log("Player caught");
        
        GameObject caughtPlayer = other.gameObject;
        bool isCaughtOutOfSafeZone = !caughtPlayer.GetComponent<SwitcherScript>().isInSafeZone.Value;
        if (isCaughtOutOfSafeZone)
           {
              Debug.Log("Caught out of safe zone");
              var deadPlayer = caughtPlayer.GetComponent<AnimationAndMovementControllerNetwork>();
              Debug.Log($"Dead player is : {deadPlayer.NetworkObjectId}");
              Debug.Log($"Catcher is : {NetworkManager.Singleton.LocalClientId}");
              HandlePlayerDeathServerRpc(deadPlayer.OwnerClientId);
           }
        
        //var deadPlayer = other.GetComponent<AnimationAndMovementControllerNetwork>();
        //Debug.Log($"Dead player is : {deadPlayer.NetworkObjectId}");
        //Debug.Log($"Catcher is : {NetworkManager.Singleton.LocalClientId}");
        //HandlePlayerDeathServerRpc(deadPlayer.OwnerClientId);
    }

    private void OnTriggerExit(Collider other)
    {
        if(!isActiveAndEnabled)
            return;
        if(other.gameObject.CompareTag("PowerSource") && IsServer)
        {   
           
            HandlePowerSourceExit();
            return;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void HandlePlayerDeathServerRpc(ulong caughtClientId)
    {
        // Fix: ConnectedClients is keyed by clientId, SpawnedObjects is keyed by NetworkObjectId
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(caughtClientId, out var client))
            return;

        var caughtNetObj = client.PlayerObject;
        if (caughtNetObj == null) return;

        var switcherScript = caughtNetObj.GetComponent<SwitcherScript>();
        if (switcherScript == null) return;
        
        Switcher caughtSwitcher = switcherScript.thisSwitcher;

        if (caughtSwitcher.IsDead())
        {
            return;
        }
        ScoreManager.Instance?.AddCatcherCatchScore(OwnerClientId);
        // 1. Break alliance first — while pole references are still valid
        var allHandlers = FindObjectsByType<SwitcherRquestHandler>(FindObjectsSortMode.None);
        var caughtHandler = allHandlers.FirstOrDefault(h => h.OwnerClientId == caughtClientId);
        caughtHandler?.BreakPartnershipIfAllied();
        switcherScript.ownedPoleType.Value = PoleType.None;
        switcherScript.targetPoleType.Value = PoleType.None;
        // 2. Abandon the pole — wipes owner, guest, occupation flags
        Pole ownedPole = caughtSwitcher.GetOwnedPole();
        ownedPole?.Abandon();

        // 3. Wipe the switcher's internal state + role
        caughtSwitcher.Eliminate();

        // 4. Disable SwitcherScript so triggers/updates no longer fire
        //switcherScript.enabled = false;

        // 5. Tell the caught client to play death anim
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { caughtClientId }
            }
        };
        PlayDeathAnimationClientRpc(clientRpcParams);
        CatcherRoutineAfterCatchingSwitcherClientRpc();
    }

    [ClientRpc]
    void PlayDeathAnimationClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("You died!");
        var localPlayerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayerObject != null) {
            localPlayerObject.GetComponent<AnimationAndMovementControllerNetwork>().enabled = false;
            //localPlayerObject.GetComponent<Animator>().SetTrigger("Die");
            StartCoroutine(waitAndPlayDeathAnimation(localPlayerObject));
        }
        magicInCatcherHand.SetActive(true);
    }
    IEnumerator waitAndPlayDeathAnimation(NetworkObject localPlayerObject)
    {
        yield return new WaitForSeconds(1.5f);
        localPlayerObject.GetComponent<Animator>().SetTrigger("Die");
    }
    public void ChangeCursedPole(PoleType cursedType) {
     
        SetCursedPoleTypeServerRpc(cursedType);
    }
    [ServerRpc(RequireOwnership = false)]
    public void SetCursedPoleTypeServerRpc(PoleType newType)
    {
        cursedPoleType.Value = newType;
    }

    [ClientRpc]
    void CatcherRoutineAfterCatchingSwitcherClientRpc()
    {
        Debug.Log("Attacking");
        
        if (IsOwner)
        {
            animator.SetTrigger("Catcher_Attack");
            movementControllerNetwork.enabled = false;
        }
        magicInCatcherHand.SetActive(true);
        if(currentSwitcherInRange != null)
        {  
           Vector3 targetPos = currentSwitcherInRange.transform.position;
            Vector3 targetPositionForMagic = new Vector3(targetPos.x, targetPos.y + 5, targetPos.z);
            magicInCatcherHand.transform.DOScale(0.5f, 0.5f).SetEase(Ease.InBounce);
            magicInCatcherHand.transform.DOMove(targetPositionForMagic, 1f).SetDelay(0.8f)
                .OnComplete(() =>
                {
                    if (IsOwner)
                    {
                        movementControllerNetwork.enabled = true;
                    }
                    
                    magicInCatcherHand.SetActive(false);

                    magicInCatcherHand.transform.localPosition = magicLocalPosition;
                    magicInCatcherHand.transform.localScale = Vector3.zero;
                    currentSwitcherInRange.GetComponent<PlayerVisuals>().ActivateSwitcherHits();
                    
                });

        }
        
    }

    IEnumerator waitToEnableCatcherMovement()
    {
        yield return new WaitForSeconds(0.5f);
        if (IsOwner)
        {
            movementControllerNetwork.enabled = true;
        }
    }

    void StartPowerDrainTimer()
    {
        if (!IsServer) return;
        if(powerDrainCoroutine  != null) StopCoroutine(powerDrainCoroutine);

        powerDrainCoroutine = StartCoroutine(PowerDrainCoroutine());
    }
    void HandlePowerSourceEnter()
    {   
        Debug.Log("Entered power source");
        if (!IsServer) return;
        if(powerRechargeCoroutine != null) StopCoroutine(powerRechargeCoroutine);
        powerRechargeCoroutine = StartCoroutine(PowerRechargeCoroutine());
        
    }

    void HandlePowerSourceExit()
    {   
        Debug.Log("Exited power source");   
        if (!IsServer) return;
        if(powerRechargeCoroutine != null) StopCoroutine(powerRechargeCoroutine);
        powerRechargeCoroutine = null;
        StartPowerDrainTimer();
    }
    IEnumerator PowerDrainCoroutine()
    {   
        Debug.Log("Started power drain");
        yield return new WaitForSeconds(powerDuration); 
        hasPowers.Value = false;
        powerDrainCoroutine = null;
        Debug.Log("Catcher has powers: " + hasPowers.Value);
    }

    IEnumerator PowerRechargeCoroutine()
    {   
        if(powerDrainCoroutine != null) StopCoroutine(powerDrainCoroutine);
        yield return new WaitForSeconds(powerRechargeDuration);
        hasPowers.Value = true;
        powerRechargeCoroutine = null;
        Debug.Log("Catcher has powers: " + hasPowers.Value);
    }

   

}
