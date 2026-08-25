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

            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            LobbyFeatures.SetCurrentLobby(joinedLobby);

            // ── Join relay using code from lobby data ──
            if (joinedLobby.Data != null && joinedLobby.Data.ContainsKey("RelayJoinCode"))
            {
                string relayJoinCode = joinedLobby.Data["RelayJoinCode"].Value;
                Debug.Log("[Client] Relay code found, joining relay...");

                GameSessionData.Instance.IsRelayHost = false;
                await RelayManager.JoinRelay(relayJoinCode);

                // Hide lobby UI and activate game session
                var lobbyDataContainer = GameObject.FindGameObjectWithTag("LobbyDataContainer");
                var gameSessionObjects = GameObject.FindGameObjectWithTag("GameSessionObjects");

                if (lobbyDataContainer != null)
                    lobbyDataContainer.SetActive(false);
                if (gameSessionObjects != null)
                    gameSessionObjects.SetActive(true);

                // Start as client
                Debug.Log("[Client] Starting NetworkManager as client...");
                NetworkManager.Singleton.StartClient();
            }
            else
            {
                Debug.LogWarning("[Client] No relay code found in lobby data");
                ToastScript.Toast("⚠ Lobby relay not yet initialized");
                return;
            }

            // Subscribe once, right after joining by ID
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            lobbyCanvasFunction.ActivatePanel(currentLobbyInfoPanel);
            ToastScript.Toast("✓ Joined lobby!");
            Debug.Log($"[Client] Joined lobby by ID: {lobbyId}");
        }
        catch (Exception e)
        {
            ToastScript.Toast($"❌ {e.Message}");
            Debug.LogError($"Join lobby exception: {e.Message}");
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