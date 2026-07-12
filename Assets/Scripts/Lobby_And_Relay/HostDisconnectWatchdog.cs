using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Plain, non-networked watchdog whose only job is to reliably catch
/// NetworkManager.OnClientStopped even when Netcode is in the middle of
/// destroying every NetworkObject in the scene (which is exactly what
/// happens right after a host disappears). Because this object has no
/// NetworkObject component, Netcode's connection teardown cannot despawn,
/// disable, or destroy it, so the subscription always survives long enough
/// to fire.
///
/// This exists specifically to work around DisconnectManager (a
/// NetworkBehaviour) being unable to reliably catch its own disconnect:
/// Netcode may despawn/destroy networked objects as part of the same
/// shutdown sequence that raises OnClientStopped, and the ordering between
/// the two is not guaranteed.
/// </summary>
public class HostDisconnectWatchdog : MonoBehaviour
{
    private static HostDisconnectWatchdog _instance;
    private bool _subscribedToCurrentSingleton;

    public static void EnsureExists(DisconnectManager owner)
    {
        if (_instance == null)
        {
            var go = new GameObject("HostDisconnectWatchdog");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<HostDisconnectWatchdog>();
        }

        // Always attempt (re-)subscription: this is called again from
        // DisconnectManager.OnNetworkSpawn every time a new game session
        // starts (fresh host/join), which is exactly when we need to make
        // sure we're hooked to the current NetworkManager.Singleton instance.
        _instance.Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribedToCurrentSingleton) return;
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientStopped += OnClientStopped;
        _subscribedToCurrentSingleton = true;
    }

    private void OnClientStopped(bool wasHost)
    {
        Debug.Log($"[HostDisconnectWatchdog] OnClientStopped fired (wasHost={wasHost}).");

        // Unsubscribe immediately: NetworkManager is shutting down, and if a
        // new session later restarts on the same Singleton instance we don't
        // want this fired twice for one disconnect.
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
        }
        _subscribedToCurrentSingleton = false;

        DisconnectManager.RunHostLeftLobbyDeleteThenCleanup(wasHost);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && _subscribedToCurrentSingleton)
        {
            NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
        }
        _instance = null;
    }
}