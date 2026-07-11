using System;
using System.Collections.Generic;
using UnityEngine;

public class GameSessionData : MonoBehaviour
{
    public static GameSessionData Instance { get; private set; }
    public string CatcherPlayerId { get; set; }
    public bool HasGameStartedYet { get; set; }
    public bool IsRelayHost { get; set; }
    public Dictionary<ulong, string> ClientIdToName { get; private set; } = new Dictionary<ulong, string>();
    public static event Action OnPlayerNamesUpdated;
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
}