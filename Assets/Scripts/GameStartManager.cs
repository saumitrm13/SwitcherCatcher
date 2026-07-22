using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies;
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
    [SerializeField] PoleExplosionEffect[] poles;

    public static event Action OnRoundEnded;
    public static event Action OnRoundEndedClientSignal;
    public static event Action OnNewRoundStarted;
    public static event Action OnNewRoundStartedClientSignal;


    Coroutine roundTimerCoroutine;



    // Populated externally when players connect (Auth ID → Netcode Client ID)
    // e.g. fill this from your player spawn manager on client connect
    public static Dictionary<string, ulong> AuthToClientId = new();

    public async void StartGame()
    {
        CatcherPowerSource.SetActive(true);
        if (!NetworkManager.Singleton.IsServer) return;
        OnNewRoundStarted?.Invoke();
        // Increment before branching so RoundNumber == 1 on the very first call.
        GameSessionData.Instance.RoundNumber++;
        Debug.Log($"[GameStartManager] Starting round {GameSessionData.Instance.RoundNumber}.");
        await LobbyService.Instance.UpdateLobbyAsync(LobbyFeatures.GetCurrentLobby().Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { LobbyKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Public, "true") }
            }
        });
        if (GameSessionData.Instance.RoundNumber == 1)
        {
            // ── First round: choose a random catcher ──────────────────────────


            AssignRandomCatcher();
            setUpAllSwitchersForNewRound();
            StartGameForEveryClientClientRpc();

            if (roundTimerCoroutine != null) StopCoroutine(roundTimerCoroutine);
            roundTimerCoroutine = StartCoroutine(RoundTimerCoroutine());
        }
        else
        {
            // ── Subsequent rounds: run the inter-round routine instead ─────────
            NewRoundRoutine();
        }
        GameSessionData.OnCatcherWon += OnAllSwitchersCaught;
        SwitcherScript.localOwnerInstance.AddGuardToSwitcherClientRpc();
    }

    async void OnAllSwitchersCaught()
    {

        StartCoroutine(CatcherWinsRoundEndCoroutine());
    }

    IEnumerator CatcherWinsRoundEndCoroutine()
    {

        yield return new WaitForSeconds(5f);

        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
            roundTimerCoroutine = null;
        }
        RoutineAfterRoundEnd();
    }


    /// <summary>
    /// Server-only. Demotes the current catcher (if any) back to Switcher, then
    /// promotes a new randomly-chosen connected client to Catcher, excluding the
    /// player who was just demoted so the role always rotates to someone else.
    /// Updates GameSessionData.CatcherPlayerId to match the new catcher.
    /// </summary>
    private void AssignRandomCatcher()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var connectedClientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        if (connectedClientIds.Count == 0) return;

        ulong? previousCatcherId = null;

        // ── Demote the previous catcher, if there was one ──────────────────────
        if (!string.IsNullOrEmpty(GameSessionData.Instance.CatcherPlayerId) &&
            ulong.TryParse(GameSessionData.Instance.CatcherPlayerId, out ulong parsedPreviousId))
        {
            previousCatcherId = parsedPreviousId;

            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(parsedPreviousId, out var previousClient) &&
                previousClient.PlayerObject != null)
            {
                previousClient.PlayerObject.GetComponent<PlayerVisuals>().AssignAsSwitcher();
                Debug.Log($"[GameStartManager] Demoted previous catcher: client {parsedPreviousId}");
            }
        }

        // ── Build the pool, excluding the previous catcher if possible ─────────
        List<ulong> pool = previousCatcherId.HasValue
            ? connectedClientIds.Where(id => id != previousCatcherId.Value).ToList()
            : connectedClientIds;

        // Fallback: if excluding the previous catcher leaves nobody (e.g. only
        // one player connected), fall back to the full list so the game doesn't stall.
        if (pool.Count == 0)
            pool = connectedClientIds;

        ulong catcherClientId = pool[UnityEngine.Random.Range(0, pool.Count)];

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

        // ── 0. Rotate the catcher: demote previous, promote a new one ──────────
        AssignRandomCatcher();

        // ── 1. Reset every Pole's ownership and occupancy state ───────────────
        var allPoleScripts = FindObjectsByType<PoleScript>(FindObjectsSortMode.None);
        foreach (var poleScript in allPoleScripts)
        {
            poleScript.thisPole.Abandon();
        }
        Debug.Log($"[GameStartManager] Reset {allPoleScripts.Length} poles to ownerless state.");

        // ── 2. Reset every SwitcherScript's server-side state ─────────────────
        setUpAllSwitchersForNewRound();

        // ── 3. Teleport every player NetworkObject back to the origin ──────────
        // (Already covers the catcher too — ConnectedClients includes everyone.)
        foreach (var kvp in NetworkManager.Singleton.ConnectedClients)
        {
            NetworkObject playerObj = kvp.Value.PlayerObject;
            if (playerObj != null)
            {
                var cc = playerObj.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerObj.transform.position = new Vector3(0, 0.4580349f, 0);
                if (cc != null) cc.enabled = true;
            }
        }
        Debug.Log("[GameStartManager] Teleported all players to Vector3.zero.");

        // ── 4. Broadcast to all clients for local cleanup + fire the event ─────
        ResetForNewRoundClientRpc();

        // ── 5. Re-use the normal game-start broadcast and restart the timer ────
        StartGameForEveryClientClientRpc();

        if (roundTimerCoroutine != null) StopCoroutine(roundTimerCoroutine);
        roundTimerCoroutine = StartCoroutine(RoundTimerCoroutine());
    }

    void setUpAllSwitchersForNewRound()
    {
        var allSwitchers = FindObjectsByType<SwitcherScript>(FindObjectsSortMode.None);
        foreach (var sw in allSwitchers)
        {
            sw.ownedPoleType.Value = PoleType.None;
            sw.targetPoleType.Value = PoleType.None;
            sw.isCompletingATask.Value = false;

            if (sw.thisSwitcher != null)
                sw.thisSwitcher.ResetForNewRound();

            sw.StopTaskTimer();
            sw.hasNecessaryResource = false;
            sw.GetAllPoles();
            sw.AddGuardToSwitcherClientRpc();
        }
        Debug.Log($"[GameStartManager] Reset {allSwitchers.Length} switcher states.");
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
        Debug.Log($"[GameStartManager] Round timer expired after {TimePerRound} seconds.");
        RoutineAfterRoundEnd();
    }

    public async void RoutineAfterRoundEnd()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        await LobbyService.Instance.UpdateLobbyAsync(LobbyFeatures.GetCurrentLobby().Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { LobbyKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Public, "false") }
            }
        });
        GameSessionData.OnCatcherWon -= OnAllSwitchersCaught;
        Debug.Log("Round has ended. Executing server-side cleanup and notifying clients.");
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
        OnNewRoundStartedClientSignal?.Invoke();
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


            movementController.RevivePlayerMovements();
            debugText.text = "[GameStartManager] Reviving dead switcher for new round.";

        }
        else if (CatcherScript.localOwnerInstance != null)
        {
            CatcherScript.localOwnerInstance.GetComponent<AnimationAndMovementControllerNetwork>().enabled = true;
        }

    }

    [ClientRpc]
    void RoutineAfterRoundEndClientRpc()
    {
        Debug.Log("[GameStartManager] RoutineAfterRoundEndClientRpc() called on client.");
        GameSessionData.Instance.HasGameStartedYet = false;
        CatcherPowerSource.SetActive(false);
        boundariesBeforeGameStart.SetActive(true);
        gameStartCanvas.localScale = (Vector3.zero);
        lobbyCanvas.localScale = (Vector3.one);
        Debug.Log("End round signal sent to clients.");
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

    void OnNetworkDespawn()
    {
        if (roundTimerCoroutine != null)
        {
            StopCoroutine(roundTimerCoroutine);
            roundTimerCoroutine = null;

        }
        GameSessionData.OnCatcherWon -= OnAllSwitchersCaught;
    }
}