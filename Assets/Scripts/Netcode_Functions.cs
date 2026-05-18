using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class Netcode_Functions : NetworkBehaviour
{
    public static Netcode_Functions Instance { get; private set; }

    [SerializeField] Transform[] spawnPoints;
    [SerializeField] TextMeshProUGUI debugText;

    private Dictionary<ulong, string> clientAuthIds = new Dictionary<ulong, string>();

    private void Awake()
    {   
        if (Instance != null) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
        Debug.Log("[Netcode_Functions] Instance initialized");
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Netcode_Functions] OnNetworkSpawn - IsServer: {IsServer}, IsHost: {IsHost}");
        
        if (!IsServer) return;

        Debug.Log("[Netcode_Functions] Server ready");
    }

    public void RegisterClientAuthId(ulong clientId, string authId)
    {
        Debug.Log($"[Netcode_Functions] RegisterClientAuthId - clientId: {clientId}, authId: {authId}");
        clientAuthIds[clientId] = authId;
    }
}