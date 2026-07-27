using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;


public class LobbyCanvasFunction : MonoBehaviour
{
    [SerializeField] private int maxPlayersInALobby = 4;
    [SerializeField] private TMP_InputField privateLobbyNameInputField;
    [SerializeField] private TMP_InputField publicLobbyNameInputField;
    [SerializeField] private TMP_InputField lobbyJoinCodeInputField;
    [SerializeField] private GameObject playerNamePanel;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TextMeshProUGUI nameErrorText;
    [SerializeField] private GameObject FirstPanel;
    [SerializeField] private GameObject[] allPanels;
    [SerializeField] GameObject boundariesBeforeGameStart;
    public TextMeshProUGUI userNameText;
    public TextMeshProUGUI debugText;


    private const string PlayerNameDataKey = "PlayerName";
    private const string PlayerNamePrefsKey = "PlayerName";

    private Lobby currentLobby;
    private string _localPlayerName = "";
    private async void Awake()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                string profile = "Player1";

                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-profile" && i + 1 < args.Length)
                    {
                        profile = args[i + 1];
                        break;
                    }
                }

                var options = new InitializationOptions();
                options.SetProfile(profile);

                await UnityServices.InitializeAsync(options);
                Debug.Log($"Unity Services initialized | Profile: {profile}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to initialize Unity Services: " + e.Message);
            return;
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Player signed in anonymously | PlayerId: {AuthenticationService.Instance.PlayerId}");
                if (nameErrorText != null) nameErrorText.text = "";


            }
            catch (Exception e)
            {
                Debug.LogError("Failed to sign in anonymously: " + e.Message);
            }
        }
        else
        {
            Debug.Log($"Player already signed in | PlayerId: {AuthenticationService.Instance.PlayerId}");
        }
    }

    private void Start()
    {
        LoadSavedPlayerName();

    }

    public void ActivatePanel(GameObject panel)
    {
        foreach (GameObject currentPanel in allPanels)
            currentPanel.SetActive(currentPanel == panel);
    }

    public async void CreateLobby(GameObject currentLobbyInfoPanel)
    {
        if (!HasValidPlayerName())
            return;

        if (privateLobbyNameInputField == null || string.IsNullOrEmpty(privateLobbyNameInputField.text))
        {
            Debug.LogError("Lobby name input field is not assigned or empty");
            return;
        }
        const int totalSteps = 6;
        LoadingProgress.StartFlow("Creating private lobby", totalSteps);
        try
        {
            string lobbyName = privateLobbyNameInputField.text;
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = true,
                IsLocked = false,
                Player = BuildPlayerWithName()
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersInALobby, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            LoadingProgress.SetStep(1, totalSteps, "Creating private lobby");
            // ── Create relay immediately on lobby creation ──
            Debug.Log("[Host] Creating relay allocation for private lobby...");
            string relayJoinCode = await RelayManager.CreateRelayAndGetJoinCode(maxPlayersInALobby);
            if (relayJoinCode == null)
            {
                Debug.LogError("[Host] Failed to create relay for private lobby");
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                LobbyFeatures.SetCurrentLobby(null);
                currentLobby = null;
                debugText.text = "Failed to create relay";
                LoadingProgress.FailFlow("Failed to create relay");
                return;
            }
            LoadingProgress.SetStep(2, totalSteps, "Relay allocated");
            // Store relay code in lobby data
            await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { LobbyKeys.RelayJoinCode, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { LobbyKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Public,"false") }
                }

            });
            LoadingProgress.SetStep(3, totalSteps, "Lobby data updated");
            // Refresh lobby reference to include relay code

            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            LoadingProgress.SetStep(4, totalSteps, "Lobby synced");

            // Subscribe once, right after the lobby is created
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();
            LoadingProgress.SetStep(5, totalSteps, "Subscribed to lobby events");
            // ── Start the host ──
            Debug.Log("[Host] Starting NetworkManager as host...");
            GameSessionData.Instance.IsRelayHost = true;
            LoadingProgress.SetStep(6, totalSteps, "Host started");
            //if (LobbyDataContainer == null)
            //{
            //    Debug.LogError("[Host] LobbyDataContainer not assigned!");
            //    return;
            //}

            //// Hide lobby UI and activate game session
            //LobbyDataContainer.SetActive(false);
            //if (GameSessionObjects != null)
            //    GameSessionObjects.SetActive(true);

            await LobbyFeatures.EnsureNetworkManagerShutdownComplete();
            NetworkManager.Singleton.StartHost();
            boundariesBeforeGameStart.SetActive(true);
            ActivatePanel(currentLobbyInfoPanel);
            LoadingProgress.FinishFlow();
            Debug.Log($"[Host] Private Lobby created and host started: {lobbyName}, Relay Code: {relayJoinCode}");
        }
        catch (Exception e)
        {
            LoadingProgress.FailFlow("Failed to create lobby");
            Debug.LogError("Failed to create lobby: " + e.Message);
            debugText.text = e.Message;
        }
    }

    public async void CreatePublicLobby(GameObject currentLobbyInfoPanel)
    {
        if (!HasValidPlayerName())
            return;

        if (publicLobbyNameInputField == null || string.IsNullOrEmpty(publicLobbyNameInputField.text))
        {
            Debug.LogError("Lobby name input field is not assigned or empty");
            return;
        }
        const int totalSteps = 6;
        LoadingProgress.StartFlow("Creating public lobby", totalSteps);
        try
        {
            string lobbyName = publicLobbyNameInputField.text;
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                IsLocked = false,
                Player = BuildPlayerWithName()
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersInALobby, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            LoadingProgress.SetStep(1, totalSteps, "Creating public lobby");
            // ── Create relay immediately on lobby creation ──
            Debug.Log("[Host] Creating relay allocation for public lobby...");
            string relayJoinCode = await RelayManager.CreateRelayAndGetJoinCode(maxPlayersInALobby);
            if (relayJoinCode == null)
            {
                Debug.LogError("[Host] Failed to create relay for public lobby");
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                LobbyFeatures.SetCurrentLobby(null);
                currentLobby = null;
                debugText.text = "Failed to create relay";
                LoadingProgress.FailFlow("Failed to create relay");
                return;
            }
            LoadingProgress.SetStep(2, totalSteps, "Relay allocated");
            // Store relay code in lobby data
            await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { LobbyKeys.RelayJoinCode, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    {LobbyKeys.GameStarted, new DataObject(DataObject.VisibilityOptions.Public,"false") }
                }
            });
            LoadingProgress.SetStep(3, totalSteps, "Lobby data updated");
            // Refresh lobby reference to include relay code
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            LoadingProgress.SetStep(4, totalSteps, "Lobby synced");
            // Subscribe once, right after the lobby is created
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();
            LoadingProgress.SetStep(5, totalSteps, "Subscribed to lobby events");
            // ── Start the host ──
            Debug.Log("[Host] Starting NetworkManager as host...");
            GameSessionData.Instance.IsRelayHost = true;


            //if (LobbyDataContainer == null)
            //{
            //    Debug.LogError("[Host] LobbyDataContainer not assigned!");
            //    return;
            //}

            //// Hide lobby UI and activate game session
            //LobbyDataContainer.SetActive(false);
            //if (GameSessionObjects != null)
            //    GameSessionObjects.SetActive(true);

            await LobbyFeatures.EnsureNetworkManagerShutdownComplete();
            NetworkManager.Singleton.StartHost();
            boundariesBeforeGameStart.SetActive(true);
            LoadingProgress.SetStep(6, totalSteps, "Host started");
            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"[Host] Public Lobby created and host started: {lobbyName}, Relay Code: {relayJoinCode}");
            LoadingProgress.FinishFlow();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create lobby: " + e.Message);

            LoadingProgress.FailFlow("Failed to create lobby");
            debugText.text = e.Message;
        }
    }

    public async void JoinLobbyByCode(GameObject currentLobbyInfoPanel)
    {
        if (!HasValidPlayerName())
            return;

        if (lobbyJoinCodeInputField == null || string.IsNullOrEmpty(lobbyJoinCodeInputField.text))
        {
            Debug.LogError("Lobby join code input field is not assigned or empty");
            return;
        }

        const int totalSteps = 6;
        LoadingProgress.StartFlow("Joining lobby", totalSteps);

        try
        {
            string lobbyCode = lobbyJoinCodeInputField.text;

            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = BuildPlayerWithName(),
            };

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
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

                Debug.Log("[Client] Starting NetworkManager as client...");

                await LobbyFeatures.EnsureNetworkManagerShutdownComplete();

                LoadingProgress.SetStep(4, totalSteps, "Network ready");

                NetworkManager.Singleton.StartClient();
                boundariesBeforeGameStart.SetActive(true);

                LoadingProgress.SetStep(5, totalSteps, "Client started");
            }
            else
            {
                Debug.LogWarning("[Client] No relay code found in lobby data");
                debugText.text = "Failed to join lobby - relay not initialized";
                LoadingProgress.FailFlow("Failed to join the lobby");
                return;
            }

            // Subscribe once, right after joining
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();
            LoadingProgress.SetStep(6, totalSteps, "Subscribed to lobby events");

            ActivatePanel(currentLobbyInfoPanel);

            Debug.Log($"[Client] Joined lobby with code: {lobbyCode}");

            LoadingProgress.FinishFlow();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join lobby: " + e.Message);
            debugText.text = e.Message;
            LoadingProgress.FailFlow("Failed to join the lobby");
        }
    }

    public async void LeaveLobbyAsync(GameObject FirstPanel)
    {
        debugText.text = "Attempting to leave lobby...";

        if (LobbyFeatures.GetCurrentLobby() == null)
        {
            debugText.text = "No current lobby active";
            return;
        }
        try
        {
            await LobbyFeatures.UnsubscribeFromCurrentLobbyEvents();

            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(LobbyFeatures.GetCurrentLobby().Id, playerId);
            LobbyFeatures.SetCurrentLobby(null);
            currentLobby = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
            GameSessionData.Instance?.ResetSession();
            ActivatePanel(FirstPanel);
            debugText.text = "Left lobby successfully";
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Leave lobby error: {e.Message}");
            debugText.text = e.Message;
        }
    }

    public async void DeleteLobbyAsync(GameObject FirstPanel)
    {
        if (LobbyFeatures.IsHost())
        {
            try
            {
                await LobbyFeatures.UnsubscribeFromCurrentLobbyEvents();
                await LobbyService.Instance.DeleteLobbyAsync(LobbyFeatures.GetCurrentLobby().Id);
                LobbyFeatures.SetCurrentLobby(null);
                currentLobby = null;
                debugText.text = "Lobby deleted successfully";
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
                GameSessionData.Instance?.ResetSession();
                ActivatePanel(FirstPanel);
                boundariesBeforeGameStart.SetActive(true);
            }
            catch (LobbyServiceException e)
            {
                debugText.text = e.Message;
            }
        }
        else
        {
            debugText.text = "Only host can delete the lobby!";
        }
    }

    public bool HasValidPlayerName()
    {
        if (playerNameInputField == null)
        {
            Debug.LogError("Player name input field is not assigned");
            if (debugText != null) debugText.text = "Player name input field is not assigned";
            return false;
        }

        string playerName = playerNameInputField.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            if (debugText != null) debugText.text = "Please enter your player name";
            playerNameInputField.Select();
            playerNameInputField.ActivateInputField();
            return false;
        }
        _localPlayerName = playerName;
        PlayerPrefs.SetString(PlayerNamePrefsKey, playerName);
        PlayerPrefs.Save();
        return true;
    }



    private void LoadSavedPlayerName()
    {
        if (playerNameInputField == null)
        {
            return;
        }

        string savedPlayerName = PlayerPrefs.GetString(PlayerNamePrefsKey, string.Empty);
        if (string.IsNullOrEmpty(savedPlayerName))
        {
            ActivatePanel(playerNamePanel);
            return;
        }
        userNameText.text = savedPlayerName;
        ActivatePanel(FirstPanel);
        if (!string.IsNullOrWhiteSpace(savedPlayerName) && string.IsNullOrWhiteSpace(playerNameInputField.text))
            playerNameInputField.text = savedPlayerName;
    }



    public void ConfirmPlayerName(GameObject FirstPanel)
    {
        string name = playerNameInputField.text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            if (nameErrorText != null)
                nameErrorText.text = "Name cannot be empty.";
            return;
        }
        _localPlayerName = name;
        // (Optional) Persist across sessions
        PlayerPrefs.SetString("PlayerName", _localPlayerName);
        PlayerPrefs.Save();
        userNameText.text = name;
        // Navigate to the main lobby menu panel
        ActivatePanel(FirstPanel);
    }

    public Unity.Services.Lobbies.Models.Player BuildPlayerWithName()
    {
        return new Unity.Services.Lobbies.Models.Player
        {
            Data = new Dictionary<String, PlayerDataObject>
        {
            {
                "PlayerName",
                new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Public,
                    _localPlayerName
                )
            }
        }
        };
    }

}