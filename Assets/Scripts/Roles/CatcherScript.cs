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
    [SerializeField] ParticleSystem electricHitAfterCatcherMagic;
    Vector3 electricHitInitialPos = new Vector3();
    Vector3 magicLocalPosition = new Vector3();
    ClientRpcParams thisClientRpcParams;
    Animator animator;
    GameObject currentSwitcherInRange;


    private void Awake()
    {
        catcher = new Catcher();
        magicLocalPosition = magicInCatcherHand.transform.localPosition;
        electricHitInitialPos = electricHitAfterCatcherMagic.transform.localPosition;
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
        if (!other.gameObject.CompareTag("Switcher")){return;}
        currentSwitcherInRange = other.gameObject;
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
        }
        magicInCatcherHand.SetActive(true);
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
                    StartCoroutine(electricHitCoroutine());
                    magicInCatcherHand.SetActive(false);
                    magicInCatcherHand.transform.localPosition = magicLocalPosition;
                    magicInCatcherHand.transform.localScale = Vector3.zero;
                    currentSwitcherInRange.GetComponent<PlayerVisuals>().ActivateSwitcherHits();
                    
                });

        }
    }

    IEnumerator electricHitCoroutine()
    {
        electricHitAfterCatcherMagic.transform.SetParent(null);
        electricHitAfterCatcherMagic.Play();
        yield return new WaitForSeconds(0.5f);
        electricHitAfterCatcherMagic.transform.SetParent(magicInCatcherHand.transform);
        electricHitAfterCatcherMagic.transform.localPosition = electricHitInitialPos;
    }
}
