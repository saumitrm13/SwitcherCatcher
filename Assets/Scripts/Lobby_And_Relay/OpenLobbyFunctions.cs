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
                Count = maxLobbiesToShowAtATime   // Fix #5: use serialized field
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
        // Fix #6: prevent spamming / stacking async calls
        if (_isQuerying) return;
        _isQuerying = true;

        try
        {   
            
            // Fix #1: destroy old lobby UI entries before creating new ones
            foreach (Transform child in verticalLayOutForLobbyInfo)
                Destroy(child.gameObject);

            lobbiesInOneSearch.Clear();
            lobbiesInOneSearch = await QueryAllLobbies();

            // Fix #2: null guard in case query failed
            if (lobbiesInOneSearch == null)
            {
                debugText.text = "Failed to retrieve lobbies.";
                return;
            }
            if(lobbiesInOneSearch.Count == 0)
            {
                debugText.text = "No lobbies found";
            }
            foreach (Lobby lobby in lobbiesInOneSearch)
            {
                GameObject lobbyGO = Instantiate(individualLobbyInfoPrefab, verticalLayOutForLobbyInfo);
                LobbyInfoUI lobbyInfoUI = lobbyGO.GetComponent<LobbyInfoUI>();

                // Fix #4: delegate UI setup to the prefab's own component
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
            Lobby currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            LobbyFeatures.SetCurrentLobby(currentLobby);
            lobbyCanvasFunction.ActivatePanel(currentLobbyInfoPanel);

        }
        catch (Exception e)
        {
            debugText.text = "Join lobby exception : "+e.Message;
        }
    }
}