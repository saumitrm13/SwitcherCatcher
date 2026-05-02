using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;


public class Netcode_Functions : NetworkBehaviour
{
    [SerializeField] NetworkObject catcherPrefab;
    [SerializeField] NetworkObject swithcer1Prefab;

    [SerializeField] Transform transformForCatcher;
    [SerializeField] Transform transformForSwitcher1;

    [SerializeField] Transform[] spawnPoints;
    [SerializeField] TextMeshProUGUI debugText;

    private bool started = false;

    private void Awake()
    {
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!started)
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                started = true;
                StartHost();
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                started = true;
                StartClient();
            }
        }
    }

    public void StartHost()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
        NetworkManager.Singleton.StartHost();
        SpawnPlayer(NetworkManager.Singleton.LocalClientId, catcherPrefab, transformForCatcher);
    }
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool isHost = (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId);
        if ((isHost))
        {
            // reject automatic player object spawning
            response.CreatePlayerObject = false;
        }
        else
        {
            response.CreatePlayerObject = true;
        }
       
        response.Approved = true;

    }
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();

    }

    private void SpawnPlayer(ulong clientId, NetworkObject playerPrefab, Transform spawnTransform)
    {
        var obj = Instantiate(playerPrefab, spawnTransform.position, spawnTransform.rotation);
        obj.SpawnAsPlayerObject(clientId);
    }



    //private void OnClientConnected(ulong clientId)
    //{   
    //    if (!NetworkManager.Singleton.IsServer || (clientId == NetworkManager.Singleton.LocalClientId)) return;
    //    debugText.text = "Executing OCCC";
    //    // Choose a spawn point — e.g., randomly or based on index
    //    Vector3 spawnPos = spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    //    Quaternion spawnRot = spawnPoints[0].rotation;
    //    debugText.text += $"\n spawn point is {spawnPos}";
    //    // Instantiate at the custom location
    //    var playerInstance = Instantiate(swithcer1Prefab, spawnPos, spawnRot);
    //    debugText.text += $"\n spawned the player at {playerInstance.transform.position}";
    //    // Spawn as that client’s player object
    //    playerInstance.GetComponent<NetworkObject>()
    //                  .SpawnAsPlayerObject(clientId);
        
       

    //}

    

    
}
