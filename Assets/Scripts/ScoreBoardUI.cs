using System.Collections;
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
        // ScoreManager may not be ready immediately (network startup timing).
        // Poll until it is, then subscribe.
        StartCoroutine(WaitForScoreManagerAndSubscribe());
    }

    private void OnDisable()
    {
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

    // ── UI refresh ────────────────────────────────────────────────────────────
    private void RefreshUI()
    {
        if (ScoreManager.Instance == null) return;

        // Clear old rows
        foreach (Transform child in rowContainer)
            Destroy(child.gameObject);

        // Rebuild sorted by score descending
        var scores = new System.Collections.Generic.List<PlayerScore>();
        foreach (var entry in ScoreManager.Instance.PlayerScores)
            scores.Add(entry);

        scores.Sort((a, b) => b.Score.CompareTo(a.Score));

        ulong localClientId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        foreach (var entry in scores)
        {
            GameObject row = Instantiate(scoreRowPrefab, rowContainer);

            // "You" highlight for the local client
            bool isLocal = entry.ClientId == localClientId;
            string label = isLocal
                ? $"<b>You  (ID {entry.ClientId})</b>"
                : $"Player  {entry.ClientId}";

            // Find child labels by name — adjust names to match your prefab
            var playerLabel = row.transform.Find("PlayerLabel")?.GetComponent<TextMeshProUGUI>();
            var scoreLabel = row.transform.Find("ScoreLabel")?.GetComponent<TextMeshProUGUI>();

            if (playerLabel != null) playerLabel.text = label;
            if (scoreLabel != null) scoreLabel.text = entry.Score.ToString();
        }
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