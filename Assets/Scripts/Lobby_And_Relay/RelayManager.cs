using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public static class RelayManager
{
    public static async Task<string> CreateRelayAndGetJoinCode(int maxPlayers)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Get the NetworkManager and check if it exists
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager.Singleton is null. Make sure NetworkManager is initialized.");
                return null;
            }

            // Get the UnityTransport component
            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager. Make sure it's attached.");
                return null;
            }

            // Verify allocation data
            if (allocation?.RelayServer == null)
            {
                Debug.LogError("Relay allocation or server data is null.");
                return null;
            }

            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            return joinCode;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in CreateRelayAndGetJoinCode: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public static async Task JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager.Singleton is null.");
                return;
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("UnityTransport component not found on NetworkManager.");
                return;
            }

            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in JoinRelay: {ex.Message}\n{ex.StackTrace}");
        }
    }
}