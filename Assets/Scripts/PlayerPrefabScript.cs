using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

public class PlayerPrefabScript : NetworkBehaviour
{
    [SerializeField] GameObject CatcherObject;
    [SerializeField] GameObject SwitcherObject;
     GameObject SwitcherCanvas;
    [SerializeField] Animator PlayerPrefabAnimator;
    [SerializeField] RuntimeAnimatorController CatcherAnimatorController;
    [SerializeField] Avatar catcherAvatar;
    [SerializeField] GameObject CatcherCanvas;
    [SerializeField] CinemachineCamera CatcherCinemachineCamera;
    [SerializeField] AudioListener CatcherCineCamListener;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void OnNetworkSpawn()
    {
        // Only the owning client needs to decide its own role.
        // Non-owning instances (observers on other machines) skip this.
        if (!IsOwner) return;

        if (GameSessionData.Instance == null)
        {
            Debug.LogError("[PlayerPrefabScript] GameSessionData.Instance is null – cannot determine role.");
            return;
        }

        string thisClientAuthId = AuthenticationService.Instance.PlayerId;
        string catcherAuthId    = GameSessionData.Instance.CatcherPlayerId;

        Debug.Log($"[PlayerPrefabScript] OnNetworkSpawn – myAuthId: {thisClientAuthId}, catcherAuthId: {catcherAuthId}");

        bool isCatcher = (thisClientAuthId == catcherAuthId);

        if (isCatcher)
        {
            Debug.Log("[PlayerPrefabScript] This client is the CATCHER – running CatcherRoutine.");
            CatcherRoutine();
        }
        else
        {
            Debug.Log("[PlayerPrefabScript] This client is a SWITCHER – no special setup needed.");
        }
    }

    void CatcherRoutine()
    {
        CatcherObject.SetActive(true);
        SwitcherObject.SetActive(false);

        GetComponent<CatcherScript>().enabled = true;
        GetComponent<SwitcherScript>().enabled = false;
        GetComponent<SwitcherRquestHandler>().enabled = false;
        GetComponent<BoxCollider>().enabled = true;
       
        GetComponent<AnimationAndMovementControllerNetwork>().FLCam = CatcherCinemachineCamera;
        
        GetComponent<AnimationAndMovementControllerNetwork>().listener = CatcherCineCamListener;

        AudioListener listener = GetComponent<AnimationAndMovementControllerNetwork>().listener;
        CinemachineCamera FLCam = GetComponent<AnimationAndMovementControllerNetwork>().FLCam;
        if (FLCam == null)
            Debug.LogError("[AnimationController] FLCam is not assigned in the Inspector!");
        else
            FLCam.Priority = 1;

        if (listener == null)
            Debug.LogError("[AnimationController] AudioListener is not assigned in the Inspector!");
        else
            listener.enabled = true;
        PlayerPrefabAnimator.runtimeAnimatorController = CatcherAnimatorController;
        PlayerPrefabAnimator.avatar = catcherAvatar;
       // GameObject.Find("SwitcherCanvas").SetActive(false);
        if (!IsOwner)
        {
            CatcherCanvas.SetActive(false);
        }
    }
}
