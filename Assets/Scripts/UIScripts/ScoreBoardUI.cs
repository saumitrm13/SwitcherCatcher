using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a world-space or screen-space Canvas that shows the live scoreboard.
/// Wire up the prefab and container in the Inspector.
///
/// Works on ALL clients — it reads directly from ScoreManager.PlayerScores,
/// which is already a fully-replicated NetworkList.
/// </summary>
public class ScoreboardUI : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Parent RectTransform that holds the score rows (e.g. a Vertical Layout Group).")]
    [SerializeField] Transform rowContainer;

    [Tooltip("Prefab with two TextMeshProUGUI children named 'PlayerLabel' and 'ScoreLabel'.")]
    [SerializeField] GameObject scoreRowPrefab;

    [Header("Optional toast")]
    [Tooltip("Temporary text to flash when any score changes. Leave empty to skip.")]
    [SerializeField] TextMeshProUGUI toastText;
    [SerializeField] float toastDuration = 2f;
    
    bool isScoreBoardVisible = false;   

    private Coroutine _toastCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        GameSessionData.OnPlayerNamesUpdated += RefreshUI;  
        // ScoreManager may not be ready immediately (network startup timing).
        // Poll until it is, then subscribe.
        StartCoroutine(WaitForScoreManagerAndSubscribe());
    }

    private void OnDisable()
    {
        GameSessionData.OnPlayerNamesUpdated += RefreshUI;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.PlayerScores.OnListChanged -= OnScoresChanged;
    }

    // ── Subscription ─────────────────────────────────────────────────────────
    private IEnumerator WaitForScoreManagerAndSubscribe()
    {
        // Wait until ScoreManager exists AND the NetworkList has been spawned
        while (ScoreManager.Instance == null ||
               !ScoreManager.Instance.IsSpawned)
        {
            yield return null;
        }

        ScoreManager.Instance.PlayerScores.OnListChanged += OnScoresChanged;
        RefreshUI(); // draw initial state
    }

    private void OnScoresChanged(NetworkListEvent<PlayerScore> changeEvent)
    {
        RefreshUI();

        // Show a toast for ADD and VALUE_CHANGED events
        if (changeEvent.Type == NetworkListEvent<PlayerScore>.EventType.Value ||
            changeEvent.Type == NetworkListEvent<PlayerScore>.EventType.Add)
        {
            ShowToast(changeEvent.Value);
        }
    }

    private void RefreshUI()
    {
        if (ScoreManager.Instance == null) return;

        foreach (Transform child in rowContainer)
            Destroy(child.gameObject);

        var scores = new List<PlayerScore>();
        foreach (var entry in ScoreManager.Instance.PlayerScores)
            scores.Add(entry);

        scores.Sort((a, b) => b.Score.CompareTo(a.Score));

        ulong localClientId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        foreach (var entry in scores)
        {
            GameObject row = Instantiate(scoreRowPrefab, rowContainer);

            bool isLocal = entry.ClientId == localClientId;
            string name = GetPlayerName(entry.ClientId);
            string label = isLocal ? $"<b>{name} (You)</b>" : name;

            var playerLabel = row.transform.Find("PlayerLabel")?.GetComponent<TextMeshProUGUI>();
            var scoreLabel = row.transform.Find("ScoreLabel")?.GetComponent<TextMeshProUGUI>();

            if (playerLabel != null) playerLabel.text = label;
            if (scoreLabel != null) scoreLabel.text = entry.Score.ToString();
        }
    }

    private string GetPlayerName(ulong clientId)
    {
        if (GameSessionData.Instance != null &&
            GameSessionData.Instance.ClientIdToName.TryGetValue(clientId, out string name) &&
            !string.IsNullOrEmpty(name))
            return name;

        return $"Player {clientId}";   // fallback if name hasn't arrived yet
    }

    // ── Toast ─────────────────────────────────────────────────────────────────
    private void ShowToast(PlayerScore entry)
    {
        if (toastText == null) return;

        ulong localClientId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        string who = entry.ClientId == localClientId ? "You" : $"Player {entry.ClientId}";
        toastText.text = $"{who} — {entry.Score} pts";

        if (_toastCoroutine != null)
            StopCoroutine(_toastCoroutine);
        _toastCoroutine = StartCoroutine(HideToastAfterDelay());
    }

    private IEnumerator HideToastAfterDelay()
    {
        toastText.gameObject.SetActive(true);
        yield return new WaitForSeconds(toastDuration);
        toastText.gameObject.SetActive(false);
    }

    public void ToggleScoreBoard(GameObject scoreBoard)
    {
        isScoreBoardVisible = !isScoreBoardVisible;
        scoreBoard.SetActive(isScoreBoardVisible);  
    }
}