using System;
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
    [SerializeField] private GameObject[] allPanels;
    public TextMeshProUGUI debugText;

    private Lobby currentLobby;

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
            currentPanel.SetActive(currentPanel == panel);
    }

    public async void CreateLobby(GameObject currentLobbyInfoPanel)
    {
        if (privateLobbyNameInputField == null || string.IsNullOrEmpty(privateLobbyNameInputField.text))
        {
            Debug.LogError("Lobby name input field is not assigned or empty");
            return;
        }

        try
        {
            string lobbyName = privateLobbyNameInputField.text;
            CreateLobbyOptions options = new CreateLobbyOptions { IsPrivate = true, IsLocked = false };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersInALobby, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);

            // Subscribe once, right after the lobby is created.
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"Lobby created: {lobbyName}, Code: {currentLobby.LobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create lobby: " + e.Message);
            debugText.text = e.Message;
        }
    }

    public async void CreatePublicLobby(GameObject currentLobbyInfoPanel)
    {
        if (publicLobbyNameInputField == null || string.IsNullOrEmpty(publicLobbyNameInputField.text))
        {
            Debug.LogError("Lobby name input field is not assigned or empty");
            return;
        }

        try
        {
            string lobbyName = publicLobbyNameInputField.text;
            CreateLobbyOptions options = new CreateLobbyOptions { IsPrivate = false, IsLocked = false };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayersInALobby, options);
            LobbyFeatures.SetCurrentLobby(currentLobby);

            // Subscribe once, right after the lobby is created.
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"Lobby created: {lobbyName}, Code: {currentLobby.LobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to create lobby: " + e.Message);
            debugText.text = e.Message;
        }
    }

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
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            LobbyFeatures.SetCurrentLobby(currentLobby);

            // Subscribe once, right after joining.
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            ActivatePanel(currentLobbyInfoPanel);
            Debug.Log($"Joined lobby with code: {lobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join lobby: " + e.Message);
            debugText.text = e.Message;
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
            // Unsubscribe before leaving so we don't receive a spurious KickedFromLobby.
            await LobbyFeatures.UnsubscribeFromCurrentLobbyEvents();

            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(LobbyFeatures.GetCurrentLobby().Id, playerId);
            LobbyFeatures.SetCurrentLobby(null);
            currentLobby = null;
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
                ActivatePanel(FirstPanel);
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
}