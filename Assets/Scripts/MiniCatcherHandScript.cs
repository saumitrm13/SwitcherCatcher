using UnityEngine;

public class MiniCatcherHandScript : MonoBehaviour
{
    [SerializeField] DangerVisuals dangerVisuals;
    [SerializeField] ParticleSystem handExplosionEffect;

    private void OnTriggerEnter(Collider other)
    {
        dangerVisuals.LaunchFromPool();
        handExplosionEffect.Play();
    }
    private void OnTriggerStay(Collider other)
    {
        dangerVisuals.LaunchFromPool();
    }
    private void OnTriggerExit(Collider other)
    {
        
    }
}
