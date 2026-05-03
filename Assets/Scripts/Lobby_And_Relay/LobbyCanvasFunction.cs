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
    
    private Lobby currentLobby;

    private async void Awake()
    {
        try
        {
            if(UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
                Debug.Log("Unity Services initialized");
            }
        }catch(Exception e)
        {
            Debug.LogError("Failed to initialize Unity Services: " + e.Message);
            return;
        }
        // Sign in anonymously if not already signed in
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Player signed in anonymously");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to sign in anonymously: " + e.Message);
            }
        }
        else
        {
            Debug.Log("Player already signed in");
        }
    }

    void Start()
    {
        
    }

    void Update()
    {
        
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
        }
    }

   
}
