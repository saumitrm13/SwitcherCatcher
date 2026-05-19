using Unity.Netcode;

using UnityEngine;

public class PlayerVisuals : NetworkBehaviour
{
    [SerializeField] private GameObject switcherBody;
    [SerializeField] private GameObject catcherBody;
   
    
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

    private void OnCatcherStateChanged(bool oldValue, bool newValue)
    {
        ApplyVisuals(newValue);
    }

    private void ApplyVisuals(bool catcher)
    {
        switcherBody.SetActive(!catcher);
        catcherBody.SetActive(catcher);
        GetComponent<CatcherScript>().enabled = true;
        
        GetComponent<SwitcherScript>().enabled = false; 

        GetComponent<SwitcherRquestHandler>().enabled = false ;
    }

    // Called by GameStartManager on the server after picking the catcher
    public void AssignAsCatcher()
    {
        if (!IsServer) return;
        isCatcher.Value = true;
    }
}