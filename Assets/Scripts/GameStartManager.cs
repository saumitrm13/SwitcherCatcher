using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class GameStartManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private Button GameStartBtn;
    [SerializeField] private Avatar CatcherAvatar;
    [SerializeField] private GameObject boundariesBeforeGameStart;
    [SerializeField] private RectTransform gameStartCanvas;
    [SerializeField] private RectTransform lobbyCanvas;
    [SerializeField] private GameObject CatcherPowerSource;
    [SerializeField] private float TimePerRound = 120f;

    public static event Action OnRoundEnded;
    public static event Action OnRoundEndedClientSignal;

    Coroutine roundTimerCoroutine;

    // Populated externally when players connect (Auth ID → Netcode Client ID)
    // e.g. fill this from your player spawn manager on client connect
    public static Dictionary<string, ulong> AuthToClientId = new();

    public void StartGame()
    {
        CatcherPowerSource.SetActive(true);
        if (!NetworkManager.Singleton.IsServer) return;

        // Increment before branching so RoundNumber == 1 on the very first call.
        GameSessionData.Instance.RoundNumber++;
        Debug.Log($"[GameStartManager] Starting round {GameSessionData.Instance.RoundNumber}.");

        if (GameSessionData.Instance.RoundNumber == 1)
        {
            // ── First round: choose a random catcher ──────────────────────────
            var connectedClientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            if (connectedClientIds.Count == 0) return;

            ulong catcherClientId = connectedClientIds[UnityEngine.Random.Range(0, connectedClientIds.Count)];

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(catcherClientId, out var client))
            {
                client.PlayerObject.GetComponent<PlayerVisuals>().AssignAsCatcher();
                GameSessionData.Instance.CatcherPlayerId = catcherClientId.ToString();
                Debug.Log($"[GameStartManager] Catcher assigned: client {catcherClientId}");
            }
            else
            {
                Debug.LogError($"[GameStartManager] Could not find player object for client {catcherClientId}");
            }

            StartGameForEveryClientClientRpc();

            if (roundTimerCoroutine != null) StopCoroutine(roundTimerCoroutine);
            roundTimerCoroutine = StartCoroutine(RoundTimerCoroutine());
        }
        else
        {
            // ── Subsequent rounds: run the inter-round routine instead ─────────
            NewRoundRoutine();
        }
    }

    /// <summary>
    /// Server-only. Called at the start of every round after the first.
    /// Resets all pole ownership, player positions, and switcher state.
    /// Scores in ScoreManager are intentionally preserved across rounds.
    /// </summary>
    private void NewRoundRoutine()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[GameStartManager] NewRoundRoutine() — round {GameSessionData.Instance.RoundNumber}.");

        // ── 1. Reset every Pole's ownership and occupancy state ───────────────
        var allPoleScripts = FindObjectsByType<PoleScript>(FindObjectsSortMode.None);
        foreach (var poleScript in allPoleScripts)
        {
            poleScript.thisPole.Abandon();
        }
        Debug.Log($"[GameStartManager] Reset {allPoleScripts.Length} poles to ownerless state.");

        // ── 2. Reset every SwitcherScript's server-side state ─────────────────
        var allSwitchers = FindObjectsByType<SwitcherScript>(FindObjectsSortMode.None);
        foreach (var sw in allSwitchers)
        {
            // Clear replicated NetworkVariables (automatically propagated to clients)
            sw.ownedPoleType.Value  = PoleType.None;
            sw.targetPoleType.Value = PoleType.None;
            sw.isCompletingATask.Value = false;

            // Clear the local Switcher data object (owned pole, target pole)
            if (sw.thisSwitcher != null)
                sw.thisSwitcher.ResetForNewRound();

            // Stop any running task timer and hide its UI on the owning client
             sw.StopTaskTimer();

            // Reset in-transit resource flag (server-side field)
            sw.hasNecessaryResource = false;
        }
        Debug.Log($"[GameStartManager] Reset {allSwitchers.Length} switcher states.");
        
        // ── 3. Teleport every player NetworkObject back to the origin ──────────
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            NetworkObject playerObj = kvp.Value.PlayerObject;
            if (playerObj != null)
                playerObj.transform.position = new Vector3(0, 0.4580349f,0);
        }
        Debug.Log("[GameStartManager] Teleported all players to Vector3.zero.");

        // ── 4. Broadcast to all clients for local cleanup + fire the event ─────
        ResetForNewRoundClientRpc();

        // ── 5. Re-use the normal game-start broadcast and restart the timer ────
        StartGameForEveryClientClientRpc();

        if (roundTimerCoroutine != null) StopCoroutine(roundTimerCoroutine);
        roundTimerCoroutine = StartCoroutine(RoundTimerCoroutine());
    }

    /// <summary>
    /// Runs on every machine when a new round (2+) starts.
    /// Clears any client-local state that the server can't reach directly,
    /// then fires the OnNewRoundStarted event so subscribers can react.
    /// </summary>
    [ClientRpc]
    private void ResetForNewRoundClientRpc()
    {
        // Reset the local SwitcherScript instance's resource flag
        // (hasNecessaryResource is a plain bool, not a NetworkVariable)
        if (SwitcherScript.localOwnerInstance != null)
            SwitcherScript.localOwnerInstance.hasNecessaryResource = false;

        // Fire the global new-round event so any listener (UI, VFX, audio…) can react
        GameSessionData.RaiseNewRoundStarted();

        Debug.Log($"[GameStartManager] New round started (round {GameSessionData.Instance.RoundNumber}). OnNewRoundStarted fired.");
    }

    IEnumerator RoundTimerCoroutine()
    {
        yield return new WaitForSeconds(TimePerRound);
        roundTimerCoroutine = null;

        RoutineAfterRoundEnd();
    }

    public void RoutineAfterRoundEnd()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
            roundTimerCoroutine = null;
        }
        
        OnRoundEnded?.Invoke();
        RoutineAfterRoundEndClientRpc();
        var allSwitchers = FindObjectsByType<SwitcherScript>(FindObjectsSortMode.None);
        foreach (var sw in allSwitchers)
        {
            sw.isInSafeZone.Value = true;
        }
    }

    [ClientRpc]
    private void MakePlayerCatcherClientRpc(ulong catcherClientId, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[GameStartManager] I am the Catcher! Client ID: {catcherClientId}");
        debugText.text = $"[GameStartManager] I am the Catcher! Client ID: {catcherClientId}";
        // assign catcher role to local player here
    }

    [ClientRpc]
    void StartGameForEveryClientClientRpc()
    {
        GameSessionData.Instance.HasGameStartedYet = true;
        CatcherPowerSource.SetActive(true);
        boundariesBeforeGameStart.SetActive(false);
        gameStartCanvas.localScale = (Vector3.one);
        lobbyCanvas.localScale = (Vector3.zero);
        DisconnectManager.MarkGameStarted();
        if (SwitcherScript.localOwnerInstance != null)
        {   
            var movementController = SwitcherScript.localOwnerInstance.gameObject.GetComponent<AnimationAndMovementControllerNetwork>();
            movementController.enabled = true;
            
            if (SwitcherScript.localOwnerInstance.thisSwitcher.IsDead()) {
                movementController.RevivePlayerMovements();
            }
        }
        else
        {
            CatcherScript.localOwnerInstance.GetComponent<AnimationAndMovementControllerNetwork>().enabled = true;
        }
    }

    [ClientRpc]
    void RoutineAfterRoundEndClientRpc()
    {
        GameSessionData.Instance.HasGameStartedYet = false;
        CatcherPowerSource.SetActive(false);
        boundariesBeforeGameStart.SetActive(true);
        gameStartCanvas.localScale = (Vector3.zero);
        lobbyCanvas.localScale = (Vector3.one);
        OnRoundEndedClientSignal?.Invoke();
        if (SwitcherScript.localOwnerInstance != null)
        {
            SwitcherScript.localOwnerInstance.gameObject.GetComponent<AnimationAndMovementControllerNetwork>().enabled = false;

        }
        else
        {
            CatcherScript.localOwnerInstance.GetComponent<AnimationAndMovementControllerNetwork>().enabled = false;
        }
    }
}