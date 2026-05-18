using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class DefaultPlayerPrefab : NetworkBehaviour
{
    private NetworkAnimator networkAnimator;
    
    private void Awake()
    {
        Debug.Log("[DefaultPlayerPrefab] Awake called");
        networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator != null)
        {
            networkAnimator.enabled = false;
            Debug.Log("[DefaultPlayerPrefab] NetworkAnimator disabled in Awake");
        }
    }

    private void Start()
    {
        Debug.Log("[DefaultPlayerPrefab] Start called");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[DefaultPlayerPrefab] OnNetworkSpawn - ClientId: {OwnerClientId}, IsOwner: {IsOwner}");
        
        if (networkAnimator != null)
        {
            networkAnimator.enabled = true;
            Debug.Log("[DefaultPlayerPrefab] NetworkAnimator enabled");
        }
    }
}