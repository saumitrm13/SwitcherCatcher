using System.Collections;
using System.Collections.Generic;

using Unity.Cinemachine;

using UnityEngine;
using DG.Tweening;
using System;

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
    public Transform anchorForThrowableMagic;
    public Vector3 localPositionForExplosionVFX = new Vector3(0, 0, 0.0058f);

    static ParticleSystem explosionVFX;
    CinemachineCamera _characterFLCam;
    Transform _originalTargetTransformForCharacterFLCam;
    GameObject _throwableMagic;
   
    Vector3 _initialLocalPositionForThrowableMagic;
    Vector3 _initial_LP_For_TM_Anchor;
    Transform _ownerTransform;
    ShakeController _shakeController;
    // ── Internal ────────────────────────────────────────────────────────────
    private Queue<GameObject> _pool;
    private float _spawnTimer;
    bool _isPoolActive = false;

    void Start()
    {
        InitializePool();
        _initial_LP_For_TM_Anchor = anchorForThrowableMagic.transform.localPosition;
        explosionVFX = GameObject.Find("PoleTopExplosion").GetComponent<ParticleSystem>(); 
        GameStartManager.OnRoundEndedClientSignal += ResetDangerVisuals;
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
        obj.transform.rotation = UnityEngine.Random.rotation;
        obj.transform.SetParent(null); // Detach so physics work freely

        // Apply random spread + upward launch velocity
        Vector3 spread = new Vector3(
            UnityEngine.Random.Range(-spreadRadius, spreadRadius),
            0f,
            UnityEngine.Random.Range(-spreadRadius, spreadRadius)
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

    public void AssignSwitcherObjectsWithOwnedPole(Transform originalTargetTransform, CinemachineCamera FLCamera, GameObject throwableMagic, Transform ownerTransform)
    {
        _characterFLCam = FLCamera;
        _originalTargetTransformForCharacterFLCam = originalTargetTransform;

        _throwableMagic = throwableMagic;
        _ownerTransform = ownerTransform;
        _initialLocalPositionForThrowableMagic = throwableMagic.transform.localPosition;
        _shakeController = _characterFLCam.GetComponent<ShakeController>();
        explosionVFX.transform.SetParent(transform);
        explosionVFX.transform.localPosition = localPositionForExplosionVFX;
          

    }

    public void ShowProblem()
    {
        StartCoroutine(showPoleTopForProblemOrSolutionCoroutine(true));
    }

    IEnumerator showPoleTopForProblemOrSolutionCoroutine(bool showProblem)
    {
        _isPoolActive = showProblem;
        problemSolvedVFX.SetActive(!showProblem);
        if (!showProblem)
        {   
            _throwableMagic.SetActive(true);
            _throwableMagic.transform.SetParent(anchorForThrowableMagic);
            _characterFLCam.Target.TrackingTarget = _throwableMagic.transform;
            anchorForThrowableMagic.DOMove(miniCatcherOnTopOfTheTower.transform.position, 1.5f).OnComplete(() =>
            {   
                ShakeCam(true);
                _throwableMagic.transform.DOMove(miniCatcherOnTopOfTheTower.transform.position, 0.5f).OnComplete(() =>
                {
                    _throwableMagic.SetActive(false);
                    explosionVFX.Play();
                    ShakeCam();
                    miniCatcherOnTopOfTheTower.SetActive(showProblem);
                }
                );
            });

            yield return new WaitForSeconds(showProblemForSeconds - 0.6f);
            _throwableMagic.transform.SetParent(_ownerTransform);
            _throwableMagic.transform.localPosition = _initialLocalPositionForThrowableMagic;
            anchorForThrowableMagic.localPosition = _initial_LP_For_TM_Anchor;

        }
        if (_characterFLCam != null && _originalTargetTransformForCharacterFLCam != null && showProblem)
        {
            miniCatcherOnTopOfTheTower.SetActive(showProblem);
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

    public void ShakeCam(bool lightShake = false)
    {
        if (lightShake) {
            _shakeController.TriggerSmallShake();
            return;
        }
        _shakeController.TriggerShake();
    }

    void ResetDangerVisuals()
    {
        StartCoroutine(ResetDangerVisualsCoroutine());  
    }
    
    IEnumerator ResetDangerVisualsCoroutine()
    {   
        miniCatcherOnTopOfTheTower.SetActive(false);
        if(_characterFLCam!=null && _originalTargetTransformForCharacterFLCam!=null)
            _characterFLCam.Target.TrackingTarget = _originalTargetTransformForCharacterFLCam;
        yield return null;
    }

}