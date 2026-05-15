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

            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                null,
                true
            );

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

            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                true
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in JoinRelay: {ex.Message}\n{ex.StackTrace}");
        }
    }
}