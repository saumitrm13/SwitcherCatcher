using System.Collections;
using System.Collections.Generic;

using Unity.Cinemachine;

using UnityEngine;

/// <summary>
/// Attach this script to an empty GameObject to create a fountain of pooled objects.
/// Assign a prefab (e.g., a sphere) to the `objectPrefab` field in the Inspector.
/// </summary>
public class DangerVisuals : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("The prefab to pool and shoot from the fountain.")]
    public GameObject objectPrefab;

    [Tooltip("Total number of objects in the pool.")]
    public int poolSize = 30;

    [Header("Fountain Settings")]
    [Tooltip("How many objects are launched per second.")]
    public float spawnRate = 5f;

    [Tooltip("Base upward force of the fountain.")]
    public float launchForce = 8f;

    [Tooltip("Horizontal spread radius of the fountain.")]
    public float spreadRadius = 1.5f;

    [Tooltip("How long each object stays active before returning to pool (seconds).")]
    public float lifetime = 3f;

    [Tooltip("Apply gravity scale to pooled Rigidbodies.")]
    public float gravityScale = 1f;

    public GameObject miniCatcherOnTopOfTheTower;

    public float showProblemForSeconds = 4f;

    public GameObject problemSolvedVFX;
    CinemachineCamera _characterFLCam;
    Transform _originalTargetTransformForCharacterFLCam;
    // ── Internal ────────────────────────────────────────────────────────────
    private Queue<GameObject> _pool;
    private float _spawnTimer;
    bool _isPoolActive = false; 

    void Start()
    {
        InitializePool();
    }

    void Update()
    {
        //if (_isPoolActive)
        //{
        //    _spawnTimer += Time.deltaTime;

        //    if (_spawnTimer >= 1f / spawnRate)
        //    {
        //        _spawnTimer = 0f;
        //        LaunchFromPool();
        //    }
        //}
    }

    // ── Pool Lifecycle ───────────────────────────────────────────────────────

    /// <summary>Pre-warms the pool by creating all objects as inactive.</summary>
    void InitializePool()
    {
        _pool = new Queue<GameObject>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(objectPrefab, transform);
            obj.SetActive(false);
            
            _pool.Enqueue(obj);
        }
    }

    /// <summary>Returns an object from the pool, or null if the pool is exhausted.</summary>
    GameObject GetFromPool()
    {
        // If everything is in use, reclaim the oldest active object
        if (_pool.Count == 0)
            return null;

        GameObject obj = _pool.Dequeue();
        obj.SetActive(true);
        return obj;
    }

    /// <summary>Returns an object back to the pool.</summary>
    public void ReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        _pool.Enqueue(obj);
    }

    // ── Fountain Logic ───────────────────────────────────────────────────────

    public void LaunchFromPool()
    {
        GameObject obj = GetFromPool();
        if (obj == null) return;

        // Reset position to fountain origin
        obj.transform.position = transform.position;
        obj.transform.rotation = Random.rotation;
        obj.transform.SetParent(null); // Detach so physics work freely

        // Apply random spread + upward launch velocity
        Vector3 spread = new Vector3(
            Random.Range(-spreadRadius, spreadRadius),
            0f,
            Random.Range(-spreadRadius, spreadRadius)
        );
        Vector3 velocity = (Vector3.up * launchForce + spread).normalized
                           * launchForce;

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            rb.AddForce(velocity, ForceMode.VelocityChange);
        }

        // Schedule return to pool after lifetime expires
        StartCoroutine(ReturnAfterDelay(obj, lifetime));
    }

    IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Guard: object may already have been returned early
        if (obj.activeSelf)
            ReturnToPool(obj);
    }

    // ── Gizmo ────────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, spreadRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position,
                        transform.position + Vector3.up * launchForce * 0.5f);
    }

    public void AssignFLCamData(Transform originalTargetTransform, CinemachineCamera FLCamera)
    {
        _characterFLCam = FLCamera;
        _originalTargetTransformForCharacterFLCam = originalTargetTransform;
    }

    public void ShowProblem()
    {
        StartCoroutine(showPoleTopForProblemOrSolutionCoroutine(true));
    }

    IEnumerator showPoleTopForProblemOrSolutionCoroutine(bool showProblem)
    {
        _isPoolActive = showProblem;
        problemSolvedVFX.SetActive(!showProblem);
        miniCatcherOnTopOfTheTower.SetActive(showProblem);
        if (_characterFLCam != null && _originalTargetTransformForCharacterFLCam != null)
        {
            _characterFLCam.Target.TrackingTarget = transform;
            yield return new WaitForSeconds(showProblemForSeconds);
            _characterFLCam.Target.TrackingTarget = _originalTargetTransformForCharacterFLCam;


        }
      
        yield return null;
    }
    
    public void StopShowingTheProblem()
    {
        StartCoroutine(showPoleTopForProblemOrSolutionCoroutine(false));
    }

    public void ShakeCam()
    {
        _characterFLCam.GetComponent<ShakeController>().TriggerShake(); 
    }

}