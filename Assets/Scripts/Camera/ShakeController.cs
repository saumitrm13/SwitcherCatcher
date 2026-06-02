
using Unity.Cinemachine;
using UnityEngine;

public class ShakeController : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;
    public bool isShaking = false;

    [SerializeField] private float minForce = 1f;
    [SerializeField] private float maxForce = 2.5f;
    public void TriggerShake()
    {   
        Vector3 randomImpulse = new Vector3(
            Random.Range(-maxForce, maxForce),
            Random.Range(-maxForce, maxForce),
            Random.Range(-maxForce, maxForce)
        );

        impulseSource.GenerateImpulse(randomImpulse);
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