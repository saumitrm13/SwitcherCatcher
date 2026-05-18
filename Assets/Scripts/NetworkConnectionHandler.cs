using Unity.Netcode;
using UnityEngine;

public class NetworkConnectionHandler : MonoBehaviour
{
    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("[NetworkConnectionHandler] Setting up ConnectionApprovalCallback");
            NetworkManager.Singleton.ConnectionApprovalCallback += HandleConnectionApproval;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= HandleConnectionApproval;
        }
    }

    private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("[NetworkConnectionHandler] HandleConnectionApproval called");

        response.CreatePlayerObject = true;
        response.Approved = true;

        Debug.Log("[NetworkConnectionHandler] Connection APPROVED - CreatePlayerObject: true");
    }
}