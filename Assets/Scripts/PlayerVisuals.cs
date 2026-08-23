using Unity.Netcode;

using UnityEngine;

public class PlayerVisuals : NetworkBehaviour
{
    [SerializeField] private GameObject switcherBody;
    [SerializeField] private GameObject catcherBody;
    [SerializeField] private Animator playerPrefabAnimator;
    [SerializeField] private Avatar catcherAvatar;
    [SerializeField] private Avatar switcherAvatar;
    [SerializeField] private Vector3 catcherColliderCentre = new Vector3(0, 0.8f, 0.91f);
    [SerializeField] private Vector3 catcherColliderSize = new Vector3(1, 1.84f, 1.67f);
    [SerializeField] private ParticleSystem[] switcherHitsParticleSystems;
    
    [SerializeField] private Vector3 switcherColliderCentre = new Vector3(0, 0.9f, 0); // TODO: set to your real switcher values
    [SerializeField] private Vector3 switcherColliderSize = new Vector3(1, 1.8f, 1);   // TODO: set to your real switcher values

   
    // Server writes, all clients read automatically
    private NetworkVariable<bool> isCatcher = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Subscribe on ALL clients including late joiners
        isCatcher.OnValueChanged += OnCatcherStateChanged;

        // Apply the current value immediately on spawn
        // (handles late joiners correctly)
        ApplyVisuals(isCatcher.Value);
    }

    public override void OnNetworkDespawn()
    {
        isCatcher.OnValueChanged -= OnCatcherStateChanged;
    }
    private void Awake()
    {


    }
    private void OnCatcherStateChanged(bool oldValue, bool newValue)
    {
        ApplyVisuals(newValue);
    }

    private void ApplyVisuals(bool catcher)
    {
        switcherBody.SetActive(!catcher);
        catcherBody.SetActive(catcher);
        GetComponent<CatcherScript>().enabled = catcher;
        GetComponent<SwitcherScript>().enabled = !catcher;
        GetComponent<SwitcherRquestHandler>().enabled = !catcher;
        playerPrefabAnimator.avatar = catcher ? catcherAvatar : switcherAvatar;

        BoxCollider collider = GetComponent<BoxCollider>();
        if (catcher)
        {
            collider.center = catcherColliderCentre;
            collider.size = catcherColliderSize;
            gameObject.tag = "Catcher";
            GetComponent<AnimationAndMovementControllerNetwork>().movementSpeed = 3.3f; // adjust speed for catcher if needed

        }
        else
        {
            collider.center = switcherColliderCentre;
            collider.size = switcherColliderSize;
            gameObject.tag = "Switcher"; // match whatever tag your switcher prefab normally uses
            GetComponent<AnimationAndMovementControllerNetwork>().movementSpeed = 3f;
        }
    }

    // Called by GameStartManager on the server after picking the catcher
    public void AssignAsCatcher()
    {
        if (!IsServer) return;
        isCatcher.Value = true;
    }

    // Called by GameStartManager on the server to demote the previous catcher back to Switcher
    public void AssignAsSwitcher()
    {
        if (!IsServer) return;
        isCatcher.Value = false;
    }

    public void ActivateSwitcherHits()
    {
        foreach (ParticleSystem system in switcherHitsParticleSystems)
        {
            system.Play();

        }

    }
}