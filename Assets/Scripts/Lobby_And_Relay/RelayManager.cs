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
            Debug.Log($"[Host] Creating Relay allocation for {maxPlayers} players...");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            Debug.Log($"[Host] Allocation created: {allocation.AllocationId}");

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[Host] Join code received: {joinCode}");

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Host] NetworkManager.Singleton is null.");
                return null;
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[Host] UnityTransport not found on NetworkManager.");
                return null;
            }

            // ── Use the modern RelayServerData struct ───────────────────────────
            // The old SetRelayServerData(ip, port, ...) overload is DEPRECATED in
            // Unity Transport 2.x and encodes DTLS data incorrectly, which causes
            // the relay server to actively reject connecting clients.
            var relayServerData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            Debug.Log($"[Host] Relay configured successfully (DTLS) - " +
                      $"IP: {allocation.RelayServer.IpV4}, Port: {allocation.RelayServer.Port}");
            return joinCode;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Host] Error in CreateRelayAndGetJoinCode: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    public static async Task JoinRelay(string joinCode)
    {
        try
        {
            Debug.Log($"[Client] Attempting to join Relay with code: {joinCode}");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Debug.Log($"[Client] Successfully joined allocation: {joinAllocation.AllocationId}");

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[Client] NetworkManager.Singleton is null.");
                return;
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[Client] UnityTransport not found on NetworkManager.");
                return;
            }

            // ── Use the modern RelayServerData struct ───────────────────────────
            // The old overload mis-encodes HostConnectionData for DTLS, causing
            // the relay to reject the connection at transport level.
            var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            Debug.Log($"[Client] Relay configured successfully (DTLS) - " +
                      $"IP: {joinAllocation.RelayServer.IpV4}, Port: {joinAllocation.RelayServer.Port}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Client] Error in JoinRelay: {ex.Message}\n{ex.StackTrace}");
        }
    }
}