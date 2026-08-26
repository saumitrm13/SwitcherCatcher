using System.Collections;
using UnityEngine;

public class PoleExplosionEffect : MonoBehaviour
{
    [SerializeField] GameObject cubeStackPrefab;
    [SerializeField] float explosionForce = 600f;
    [SerializeField] float explosionRadius = 4f;
    [SerializeField] float upwardsModifier = 1.5f;
    [SerializeField] float timeForPoleDestruction = 3f;
    [SerializeField] Collider sphereCollider;
    [SerializeField] Vector3 cubeStackSpawnLocalPos = new Vector3(0.003f, 0.0067f, 0.128f);
    [SerializeField] Vector3 cubeStackSpawnLocalRot = new Vector3(0, -90, -90);
    [SerializeField] Vector3 cubeStackSpaenLocalScale = new Vector3(0.01f, 0.01f, 0.01f);
    [SerializeField] Animator thisPoleAnimator;
    MeshRenderer meshRenderer;
    GameObject currentCubeStack;
    DangerVisuals dangerVisuals;

    bool isDestroyed = false;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        StartCoroutine(ActivateCubeStackCoroutine());
        GameStartManager.OnRoundEndedClientSignal += ResetPole;
        GameStartManager.OnNewRoundStartedClientSignal += ResetPole;
        dangerVisuals = GetComponentInChildren<DangerVisuals>();    
    }
    public void Explode()
    {
        if (!GameSessionData.Instance.HasGameStartedYet)
        {
            return;
        }
        if (currentCubeStack == null) return;
        Debug.Log($"[PoleExplosionEffect] Exploding pole: {gameObject.name}");
        currentCubeStack.SetActive(true);
        Vector3 origin = currentCubeStack.transform.position;
        isDestroyed = true;
        foreach (var rb in currentCubeStack.GetComponentsInChildren<Rigidbody>())
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
        Destroy(currentCubeStack);
        currentCubeStack = null;
        dangerVisuals?.ResetDangerVisuals();
    }

    public void ResetPole()
    {
        if (isDestroyed)
        {
            StartCoroutine(ActivateCubeStackCoroutine());
        }
    }

    IEnumerator ActivateCubeStackCoroutine()
    {
        thisPoleAnimator.Rebind();
        thisPoleAnimator.Update(0f);
        thisPoleAnimator.enabled = false;
        meshRenderer.enabled = true;
        Debug.Log($"[PoleExplosionEffect] Activating cube stack for pole: {gameObject.name}");
        if (cubeStackPrefab != null && currentCubeStack == null)
        {
            currentCubeStack = Instantiate(cubeStackPrefab, transform);
            currentCubeStack.transform.localPosition = cubeStackSpawnLocalPos;
            currentCubeStack.transform.localRotation = Quaternion.EulerRotation(cubeStackSpawnLocalRot);
            currentCubeStack.transform.localScale = cubeStackSpaenLocalScale;
            currentCubeStack.SetActive(false);
            Debug.Log($"[PoleExplosionEffect] Cube stack instantiated for pole: {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[PoleExplosionEffect] Cube stack prefab is not assigned for pole: {gameObject.name}");
        }
        yield return null;
    }
}