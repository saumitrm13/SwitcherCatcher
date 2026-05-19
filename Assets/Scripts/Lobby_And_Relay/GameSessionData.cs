using UnityEngine;

public class GameSessionData : MonoBehaviour
{
    public static GameSessionData Instance { get; private set; }
    public string CatcherPlayerId { get; set; }
    public bool HasGameStartedYet { get; set; }
    public bool IsRelayHost { get; set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Instance.HasGameStartedYet = false;
    }
}