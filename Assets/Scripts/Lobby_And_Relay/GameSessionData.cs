using System;
using System.Collections.Generic;
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
        ClientIdToLobbyPlayerId[clientId] = lobbyPlayerId;
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
}