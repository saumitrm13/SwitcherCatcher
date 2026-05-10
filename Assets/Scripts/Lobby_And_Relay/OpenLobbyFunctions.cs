using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class OpenLobbyFunctions : MonoBehaviour
{
    [SerializeField] int maxLobbiesToShowAtATime = 10;
    [SerializeField] GameObject individualLobbyInfoPrefab;
    [SerializeField] Transform verticalLayOutForLobbyInfo;
    [SerializeField] LobbyCanvasFunction lobbyCanvasFunction;
    [SerializeField] GameObject currentLobbyInfoPanel;
    [SerializeField] TextMeshProUGUI debugText;

    private List<Lobby> lobbiesInOneSearch = new List<Lobby>();
    private bool _isQuerying = false;

    private void OnEnable()
    {
        Debug.Log("Getting lobbies");
        GetAllPublicLobbies();
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
            debugText.text = e.Message;
            return null;
        }
    }

    async void GetAllPublicLobbies()
    {
        if (_isQuerying) return;
        _isQuerying = true;

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
        }
    }

    async void JoinLobbyById(string lobbyId)
    {
        try
        {
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            LobbyFeatures.SetCurrentLobby(joinedLobby);

            // Subscribe once, right after joining by ID.
            await LobbyFeatures.SubscribeToCurrentLobbyEvents();

            lobbyCanvasFunction.ActivatePanel(currentLobbyInfoPanel);
        }
        catch (Exception e)
        {
            debugText.text = "Join lobby exception: " + e.Message;
        }
    }
}