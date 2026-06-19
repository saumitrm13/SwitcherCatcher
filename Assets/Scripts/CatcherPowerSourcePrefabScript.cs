using UnityEngine;
using DG.Tweening;
public class CatcherPowerSourcePrefabScript : MonoBehaviour
{
    [SerializeField] private float scale = 0.4f;
    [SerializeField] private float scaleTime = 0.4f;
    [SerializeField] Rigidbody CatcherPowerSourcePrefabRigidBody;

    private void Awake()
    {
        transform.DOScale(scale, scaleTime).SetEase(Ease.OutBack).OnComplete(() =>
        {
            CatcherPowerSourcePrefabRigidBody.useGravity = true;
        });

    }
 
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Catcher"))
        {
            // Call the method in the CatcherPowerSourceScript
            CatcherScript catcher = other.GetComponent<CatcherScript>();
            if (catcher != null)
            {
                catcher.ScaleRoutine();

                Destroy(gameObject);
            }
        }
    }
}

