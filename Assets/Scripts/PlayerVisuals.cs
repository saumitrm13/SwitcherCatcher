using Unity.Netcode;

using UnityEngine;

public class PlayerVisuals : NetworkBehaviour
{
    [SerializeField] private GameObject switcherBody;
    [SerializeField] private GameObject catcherBody;
    [SerializeField] private Animator playerPrefabAnimator;
    [SerializeField] private Avatar catcherAvatar;
    [SerializeField] private Vector3 catcherColliderCentre = new Vector3(0, 0.8f, 0.91f);
    [SerializeField] private Vector3 catcherColliderSize = new Vector3(1, 1.84f, 1.67f);
    [SerializeField] private ParticleSystem[] switcherHitsParticleSystems;



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
        playerPrefabAnimator.avatar = catcher ? catcherAvatar : playerPrefabAnimator.avatar;
        if (catcher)
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            collider.center = catcherColliderCentre;
            collider.size = catcherColliderSize;
            gameObject.tag = "Catcher";
        }
    }

    // Called by GameStartManager on the server after picking the catcher
    public void AssignAsCatcher()
    {
        if (!IsServer) return;
        isCatcher.Value = true;
    }

    public void ActivateSwitcherHits()
    {
        foreach (ParticleSystem system in switcherHitsParticleSystems)
        {
            system.Play();

        }

    }
}