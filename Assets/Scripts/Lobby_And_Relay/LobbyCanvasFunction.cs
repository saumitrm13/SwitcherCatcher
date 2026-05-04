using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCanvasFunction : MonoBehaviour
{
    [SerializeField] private int maxPlayersInALobby = 4;
    [SerializeField] private TMP_InputField lobbyNameInputField;
    [SerializeField] private TMP_InputField lobbyJoinCodeInputField;
    [SerializeField] private GameObject[] allPanels;
    [SerializeField] private TextMeshProUGUI debugText;
    private Lobby currentLobby;

    private async void Awake()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // ── CHANGE 1: Read -profile arg before initializing ──────────────
                string profile = "Player1"; // default for Unity Editor

                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-profile" && i + 1 < args.Length)
                    {
                        profile = args[i + 1];
                        break;
                    }
                }

                // ── CHANGE 2: Pass profile into InitializationOptions ────────────
                var options = new InitializationOptions();
                options.SetProfile(profile);

                await UnityServices.InitializeAsync(options);   // ← was InitializeAsync()
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
                // ── CHANGE 3: Log the actual PlayerId so you can verify ──────────
                Debug.Log($"Player signed in anonymously | PlayerId: {AuthenticationService.Instance.PlayerId}");
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



    public void ActivatePanel(GameObject panel)
    {
        foreach (GameObject currentPanel in allPanels)
        {
            if (currentPanel != panel)
            {
                currentPanel.SetActive(false);
            }
            else
            {
                currentPanel.SetActive(true);
            }
        }
    }
    /// <summary>
    /// Creates a lobby with the specified name and max players
    /// </summary>
    public async void CreateLobby(GameObject currentLobbyInfoPanel)
    {
        if (lobbyNameInputField == null || string.IsNullOrEmpty(lobbyNameInputField.text))
        {
            Debug.LogError("Lobby name input field is not assigned or empty");
            return;
        }

        try
        {
            string lobbyName = lobbyNameInputField.text;
            CreateLobbyOptions options = new CreateLobbyOptions()
            {
                IsPrivate = false,
                IsLocked = false
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersInALobby, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"Lobby created with name: {lobbyName}, Code: {currentLobby.LobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create lobby: " + e.Message);
            debugText.text = e.Message;
        }
    }

    /// <summary>
    /// Joins a lobby using the provided lobby code
    /// </summary>
    public async void JoinLobbyByCode(GameObject currentLobbyInfoPanel)
    {
        if (lobbyJoinCodeInputField == null || string.IsNullOrEmpty(lobbyJoinCodeInputField.text))
        {
            Debug.LogError("Lobby join code input field is not assigned or empty");
            return;
        }

        try
        {
            string lobbyCode = lobbyJoinCodeInputField.text;
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions();
           

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"Successfully joined lobby with code: {lobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join lobby: " + e.Message);
            debugText.text = e.Message;
        }
    }

    public async void LeaveLobbyAsync(GameObject FirstPanel)
    {
        if(LobbyFeatures.GetCurrentLobby() == null)
        {
            debugText.text = "No current lobby active";
            return;
        }
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(LobbyFeatures.GetCurrentLobby().Id, playerId);
            LobbyFeatures.SetCurrentLobby(null);
            currentLobby = null;
            ActivatePanel(FirstPanel);

        }
        catch (LobbyServiceException e) {
            Debug.LogError($"Leave lobby error : {e.Message}");
            debugText.text = e.Message; 

        }
    }

   
}
