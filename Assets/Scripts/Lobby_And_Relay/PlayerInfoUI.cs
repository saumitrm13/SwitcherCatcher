using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerInfoUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI playerScoreText;

    string _lobbyPlayerId;
    ulong? _resolvedClientId;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetupPlayerScoreTracking(string lobbyPlayerId)
    {
        _lobbyPlayerId = lobbyPlayerId;
        playerScoreText.text = "-";

        GameSessionData.OnClientIdMappingUpdated += TryResolveClientId;
        TryResolveClientId();
    }

    void TryResolveClientId()
    {
        if (_resolvedClientId.HasValue) return;
        if (GameSessionData.Instance == null) return;

        foreach (var kvp in GameSessionData.Instance.ClientIdToLobbyPlayerId)
        {
            if (kvp.Value == _lobbyPlayerId)
            {
                _resolvedClientId = kvp.Key;
                SubscribeToScore();
                break;
            }
        }
    }

    void SubscribeToScore()
    {
        if (ScoreManager.Instance == null || !ScoreManager.Instance.IsSpawned)
        {
            StartCoroutine(WaitForScoreManager());
            return;
        }
        ScoreManager.Instance.PlayerScores.OnListChanged += OnScoresChanged;
        RefreshScoreText();
    }

    IEnumerator WaitForScoreManager()
    {
        while (ScoreManager.Instance == null || !ScoreManager.Instance.IsSpawned)
            yield return null;
        ScoreManager.Instance.PlayerScores.OnListChanged += OnScoresChanged;
        RefreshScoreText();
    }

    void OnScoresChanged(NetworkListEvent<PlayerScore> _) => RefreshScoreText();

    void RefreshScoreText()
    {
        if (!_resolvedClientId.HasValue || playerScoreText == null) return;
        foreach (var entry in ScoreManager.Instance.PlayerScores)
        {
            if (entry.ClientId == _resolvedClientId.Value)
            {
                playerScoreText.text = entry.Score.ToString();
                return;
            }
        }
    }

    private void OnDestroy()
    {
      
        GameSessionData.OnClientIdMappingUpdated -= TryResolveClientId;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.PlayerScores.OnListChanged -= OnScoresChanged;
    }

}
