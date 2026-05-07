using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyInfoUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI lobbyNameText;
    [SerializeField] TextMeshProUGUI lobbyIdText;
    [SerializeField] TextMeshProUGUI playerCountText;
    [SerializeField] Button joinButton;

    public void Setup(Lobby lobby, Action<string> onJoin)
    {
        lobbyNameText.text = lobby.Name;
        lobbyIdText.text = lobby.Id;
        playerCountText.text = lobby.Players.Count.ToString();

        // Fix #3: capture ID in local variable to avoid closure bug
        string capturedId = lobby.Id;
        joinButton.onClick.AddListener(() => onJoin(capturedId));
    }

    // Ensures listeners don't stack if this object is ever reused
    private void OnDestroy()
    {
        joinButton.onClick.RemoveAllListeners();
    }
}