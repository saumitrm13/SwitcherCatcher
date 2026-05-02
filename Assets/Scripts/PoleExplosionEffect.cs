using System.Collections;
using UnityEngine;

public class PoleExplosionEffect : MonoBehaviour
{
    [SerializeField] GameObject cubeStack;
    [SerializeField] float explosionForce = 600f;
    [SerializeField] float explosionRadius = 4f;
    [SerializeField] float upwardsModifier = 1.5f;
    [SerializeField] float timeForPoleDestruction = 3f;
    [SerializeField] Collider sphereCollider;
    MeshRenderer meshRenderer;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }
    public void Explode()
    {
        if (cubeStack == null) return;
       
        cubeStack.SetActive(true);
        Vector3 origin = cubeStack.transform.position;
        
        foreach (var rb in cubeStack.GetComponentsInChildren<Rigidbody>())
        {
            rb.AddExplosionForce(
                explosionForce,
                origin,
                explosionRadius,
                upwardsModifier,
                ForceMode.Impulse
            );
        }
        meshRenderer.enabled = false;
        //sphereCollider.enabled = false;
        StartCoroutine(PoleDestroyRoutine());
    }

    IEnumerator PoleDestroyRoutine()
    {
        yield return new WaitForSeconds(timeForPoleDestruction);
        Destroy(cubeStack);
        
    }
}