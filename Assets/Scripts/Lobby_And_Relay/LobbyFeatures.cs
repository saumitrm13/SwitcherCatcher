using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyFeatures : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameForDisplayText;
    [SerializeField] private TextMeshProUGUI lobbyJoinCodeForDisplayText;
    [SerializeField] private GameObject playerInfoPrefab;
    [SerializeField] private RectTransform verticalLayoutGroupForPlayerInfo;
    private static Lobby currentLobby;

    private void OnEnable()
    {
        ShowLobbyInfo();
    }
    private void ShowLobbyInfo()
    {   
        
        if (currentLobby != null) {
            lobbyNameForDisplayText.text = currentLobby.Name;
            lobbyJoinCodeForDisplayText.text = currentLobby.LobbyCode;
            foreach (Player player in currentLobby.Players) {

                GameObject playerInfo = Instantiate(playerInfoPrefab, verticalLayoutGroupForPlayerInfo);
                playerInfo.transform.Find("PlayerIDText").GetComponent<TextMeshProUGUI>().text = player.Id;
                playerInfo.transform.Find("PlayerNameText").GetComponent<TextMeshProUGUI>().text
                    = player.Data != null && player.Data.ContainsKey("PlayerName")
                    ? player.Data["PlayerName"].Value : "Unknown";
            }
        }
    }

    public static Lobby GetCurrentLobby()
    {
        return currentLobby;
    }

    public static void SetCurrentLobby(Lobby newCurrentLobby)
    {
        currentLobby = newCurrentLobby;
    }
}
