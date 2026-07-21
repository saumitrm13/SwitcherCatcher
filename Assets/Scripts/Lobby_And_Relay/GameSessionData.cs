using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class GameSessionData : MonoBehaviour
{
    public static GameSessionData Instance { get; private set; }
    public string CatcherPlayerId { get; set; }
    public bool HasGameStartedYet { get; set; }
    public bool IsRelayHost { get; set; }

    /// <summary>
    /// Tracks how many rounds have been played in the current game session.
    /// 0 means no round has started yet. Incremented by GameStartManager at the
    /// top of StartGame() so round 1 always picks a random catcher and subsequent
    /// rounds go through NewRoundRoutine() instead.
    /// </summary>
    public int RoundNumber { get; set; } = 0;

    public Dictionary<ulong, string> ClientIdToName { get; private set; } = new Dictionary<ulong, string>();
    public static event Action OnPlayerNamesUpdated;

    public Dictionary<ulong, string> ClientIdToLobbyPlayerId { get; private set; } = new Dictionary<ulong, string>();
    public static event Action OnClientIdMappingUpdated;

    /// <summary>
    /// Fired on every machine (via a ClientRpc from GameStartManager) when a new
    /// round starts (round 2+). Subscribe here to react to inter-round resets.
    /// </summary>
    public static event Action OnNewRoundStarted;

    public static event Action OnCatcherWon;

    [HideInInspector]
    public int deadSwitchersCountThisRound = 0;
    [HideInInspector]
    public bool changingDeadSwitcherCount = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Instance.HasGameStartedYet = false;
    }

    public void RegisterName(ulong clientId, string playerName)
    {
        ClientIdToName[clientId] = playerName;
        OnPlayerNamesUpdated?.Invoke();
    }

    public void RegisterLobbyPlayerId(ulong clientId, string lobbyPlayerId)
    {
        // Remove stale entries for the same lobby player (e.g. after a rejoin,
        // where this player gets a new Netcode clientId).
        var staleKeys = ClientIdToLobbyPlayerId
            .Where(kvp => kvp.Value == lobbyPlayerId && kvp.Key != clientId)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var staleKey in staleKeys)
            ClientIdToLobbyPlayerId.Remove(staleKey);

        ClientIdToLobbyPlayerId[clientId] = lobbyPlayerId;
        OnClientIdMappingUpdated?.Invoke();
    }
    /// <summary>
    /// Removes a single departed client's name/id mappings, leaving every other
    /// currently-present player's data untouched. Call this for individual
    /// leave/disconnect events (mid-game client drop, single player leaving a
    /// still-alive lobby) — NOT for full lobby teardown, use ResetSession() there.
    /// </summary>
    public void RemoveClient(ulong clientId)
    {
        bool namesChanged = ClientIdToName.Remove(clientId);
        bool idsChanged = ClientIdToLobbyPlayerId.Remove(clientId);

        if (namesChanged) OnPlayerNamesUpdated?.Invoke();
        if (idsChanged) OnClientIdMappingUpdated?.Invoke();
    }

    /// <summary>
    /// Full teardown: clears every player's name/id mapping and resets round
    /// state. Call this when the lobby itself goes away — host deletes it,
    /// the local player leaves it, or the host disconnects mid-game and
    /// everyone gets kicked back to the lobby screen. Do NOT call this for a
    /// single client dropping out of an otherwise-still-running lobby/game;
    /// use RemoveClient(clientId) for that instead.
    /// </summary>
    public void ResetSession()
    {
        ClientIdToName.Clear();
        ClientIdToLobbyPlayerId.Clear();
        CatcherPlayerId = null;
        RoundNumber = 0;
        HasGameStartedYet = false;

        OnPlayerNamesUpdated?.Invoke();
        OnClientIdMappingUpdated?.Invoke();
    }
    /// <summary>
    /// Called by GameStartManager's ClientRpc on every machine to fire the
    /// OnNewRoundStarted event locally. Kept here so the event's backing field
    /// remains private to this class.
    /// </summary>
    public static void RaiseNewRoundStarted()
    {
        OnNewRoundStarted?.Invoke();
    }

    public IEnumerator ChangeDeadSwitcherCountCoroutine()
    {
        while (GameSessionData.Instance.changingDeadSwitcherCount)
        {
            yield return null;
        }
        GameSessionData.Instance.changingDeadSwitcherCount = true;
        GameSessionData.Instance.deadSwitchersCountThisRound++;
        GameSessionData.Instance.changingDeadSwitcherCount = false;
        if (GameSessionData.Instance.deadSwitchersCountThisRound >= NetworkManager.Singleton.ConnectedClients.Count - 1)
        {
            OnCatcherWon?.Invoke();
        }
    }
}