using UnityEngine;
using Unity.Netcode;
using System.Linq;
using System;

public class CatcherScript : NetworkBehaviour
{
    Catcher catcher;
    public static NetworkVariable<PoleType> cursedPoleType = new NetworkVariable<PoleType>(PoleType.None);
    [SerializeField] GameObject catcherCanvas;
    private void Awake()
    {
        catcher = new Catcher(); 
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            catcherCanvas.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) { return; }
        Debug.Log("Player caught");
        if (other.gameObject.CompareTag("Switcher"))
        {
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
        switcherScript.enabled = false;

        // 5. Tell the caught client to play death anim
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { caughtClientId }
            }
        };
        PlayDeathAnimationClientRpc(clientRpcParams);
    }

    [ClientRpc]
    void PlayDeathAnimationClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("You died!");
        var localPlayerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
        if (localPlayerObject != null) {
            localPlayerObject.GetComponent<AnimationAndMovementControllerNetwork>().enabled = false;
            localPlayerObject.GetComponent<Animator>().SetTrigger("Die");
        }
    }

    public void ChangeCursedPole(PoleType cursedType) {
     
        cursedPoleType.Value = cursedType;
    }
}
