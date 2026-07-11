
using Unity.Cinemachine;
using UnityEngine;

public class ShakeController : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;
    public bool isShaking = false;

    [SerializeField] private float minForce = 1f;
    [SerializeField] private float maxForce = 2.5f;
    [SerializeField] private float minSmallForce = 0.25f;
    [SerializeField] private float maxSmallForce = 0.75f;
    public void TriggerShake()
    {   
        Vector3 randomImpulse = new Vector3(
            Random.Range(-maxForce, maxForce),
            Random.Range(-maxForce, maxForce),
            Random.Range(-maxForce, maxForce)
        );

        impulseSource.GenerateImpulse(randomImpulse);
    }

    public void TriggerSmallShake()
    {
        Vector3 randomSmallImpulse = new Vector3(
           Random.Range(minSmallForce, maxSmallForce),
           Random.Range(minSmallForce, maxSmallForce),
           Random.Range(minSmallForce, maxSmallForce)
       );

        impulseSource.GenerateImpulse(randomSmallImpulse);
    }
    //private void Update()
    //{
    //    if (isShaking)
    //    {
    //        TriggerShake();
    //        isShaking = false;
    //    }
    //}
}