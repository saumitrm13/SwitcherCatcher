using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class OpenLobbyFunctions : MonoBehaviour
{
    [SerializeField] int maxLobbiesToShowAtATime = 10;
    [SerializeField] GameObject individualLobbyInfoPrefab;
    [SerializeField] Transform verticalLayOutForLobbyInfo;
    [SerializeField] LobbyCanvasFunction lobbyCanvasFunction;   
    [SerializeField] GameObject currentLobbyInfoPanel;
    [SerializeField] TextMeshProUGUI debugText;
    [SerializeField] Button refreshButton;
    [SerializeField] GameObject boundariesBeforeGameStart;


    private List<Lobby> lobbiesInOneSearch = new List<Lobby>();
    private bool _isQuerying = false;

    private void OnEnable()
    {
        Debug.Log("Getting lobbies");
        GetAllPublicLobbies();
    }

    async void JoinLobbyById(string lobbyId)
    {
        if (!lobbyCanvasFunction.HasValidPlayerName())
            return;

        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = lobbyCanvasFunction.BuildPlayerWithName()
            };

            ToastScript.Toast("Joining lobby...");
            const int totalSteps = 6;
            LoadingProgress.StartFlow("Joining lobby", totalSteps);
            Lobby currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            LoadingProgress.SetStep(1, totalSteps, $"Joining lobby");

            bool gameStarted = false;
            if (currentLobby.Data != null &&
                currentLobby.Data.TryGetValue("GameStarted", out DataObject gameStartedData))
            {
                bool.TryParse(gameStartedData.Value, out gameStarted);
            }

            if (gameStarted)
            {
                Debug.Log("[Client] Cannot join. Game has already started.");

                await LobbyService.Instance.RemovePlayerAsync(
                    currentLobby.Id,
                    AuthenticationService.Instance.PlayerId);

                LobbyFeatures.SetCurrentLobby(null);
                currentLobby = null;

                debugText.text = "Game has already started.";
                LoadingProgress.FailFlow("This lobby's game has already started.");

                return;
            }

            LoadingProgress.SetStep(2, totalSteps, "Lobby validated");
            // ── Join relay using code from lobby data ──
            if (currentLobby.Data != null && currentLobby.Data.ContainsKey("RelayJoinCode"))
            {   

                string relayJoinCode = currentLobby.Data["RelayJoinCode"].Value;
                Debug.Log("[Client] Relay code found, joining relay...");

                GameSessionData.Instance.IsRelayHost = false;
                await RelayManager.JoinRelay(relayJoinCode);
                LoadingProgress.SetStep(3, totalSteps, "Connected to relay");

                await LobbyFeatures.EnsureNetworkManagerShutdownComplete();

                LoadingProgress.SetStep(4, totalSteps, "Network ready");
                // Hide lobby UI and activate game session
                //var lobbyDataContainer = GameObject.FindGameObjectWithTag("LobbyDataContainer");
                //var gameSessionObjects = GameObject.FindGameObjectWithTag("GameSessionObjects");

                //if (lobbyDataContainer != null)
                //    lobbyDataContainer.SetActive(false);
                //if (gameSessionObjects != null)
                //    gameSessionObjects.SetActive(true);

                // Start as client
                Debug.Log("[Client] Starting NetworkManager as client...");
                NetworkManager.Singleton.StartClient();
                boundariesBeforeGameStart.SetActive(true);

                LoadingProgress.SetStep(5, totalSteps, "Client started");
            }
            else
            {
                Debug.LogWarning("[Client] No relay code found in lobby data");
                ToastScript.Toast("⚠ Lobby relay not yet initialized");
                LoadingProgress.FailFlow("Failed to join the lobby");
                return;
            }

            // Subscribe once, right after joining by ID
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            lobbyCanvasFunction.ActivatePanel(currentLobbyInfoPanel);
            ToastScript.Toast("✓ Joined lobby!");
            Debug.Log($"[Client] Joined lobby by ID: {lobbyId}");
            LoadingProgress.SetStep(6, totalSteps, "Subscribed to lobby events");
            lobbyCanvasFunction.ActivatePanel(currentLobbyInfoPanel);

            

            LoadingProgress.FinishFlow();
        }
        catch (Exception e)
        {
            ToastScript.Toast($"❌ {e.Message}");
            Debug.LogError($"Join lobby exception: {e.Message}");
            LoadingProgress.FailFlow("Failed to join the lobby");
        }
    }

    async Task<List<Lobby>> QueryAllLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = maxLobbiesToShowAtATime
            };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);
            return response.Results;
        }
        catch (Exception e)
        {
            ToastScript.Toast($"❌ Failed to query lobbies: {e.Message}");
            return null;
        }
    }

    async void GetAllPublicLobbies()
    {
        if (_isQuerying) return;
        _isQuerying = true;

        refreshButton.interactable = false;
        try
        {
            foreach (Transform child in verticalLayOutForLobbyInfo)
                Destroy(child.gameObject);

            lobbiesInOneSearch.Clear();
            lobbiesInOneSearch = await QueryAllLobbies();

            if (lobbiesInOneSearch == null)
            {
                debugText.text = "Failed to retrieve lobbies.";
                return;
            }
            if (lobbiesInOneSearch.Count == 0)
            {
                debugText.text = "No lobbies found";
            }

            foreach (Lobby lobby in lobbiesInOneSearch)
            {
                GameObject lobbyGO = Instantiate(individualLobbyInfoPrefab, verticalLayOutForLobbyInfo);
                LobbyInfoUI lobbyInfoUI = lobbyGO.GetComponent<LobbyInfoUI>();
                lobbyInfoUI.Setup(lobby, JoinLobbyById);
            }
        }
        finally
        {
            _isQuerying = false;
            refreshButton.interactable = true;
        }
    }


    public void OnRefreshBtnClicked()
    {
        GetAllPublicLobbies();
    }
}