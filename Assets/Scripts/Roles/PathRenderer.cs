using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System;
using DG.Tweening;

public class PathRenderer : MonoBehaviour
{
    [SerializeField] SwitcherScript switcherScript;
    [SerializeField] Transform originTransform;

    [Header("Arrow Settings")]
    [SerializeField] GameObject arrowPrefab;
    [SerializeField] float arrowSpacing = 1.5f;
    [SerializeField] float arrowHeightOffset = 0.2f;

    [Header("Performance Settings")]
    [SerializeField] float recalcInterval = 0.3f;       // Seconds between path recalcs
    [SerializeField] float moveThreshold = 0.5f;        // Min player movement to trigger recalc
    [SerializeField] int maxPoolSize = 50;              // Cap on pooled arrow objects

    //[SerializeField] Slider timeRemainingSlider;
    //[SerializeField] GameObject taskIndicaatorVFX;
    float totalTime = new float();

    NavMeshPath path;
    PoleType lastKnownTargetType = PoleType.None;
    Vector3 cachedTargetPosition;

    float timeSinceLastRecalc = 0f;
    Vector3 lastPlayerPosition;

    // Tracks which arrows are currently active so we can release them back cleanly
    readonly List<GameObject> activeArrows = new List<GameObject>();

    IObjectPool<GameObject> arrowPool;

    void Awake()
    {
        arrowPool = new UnityEngine.Pool.ObjectPool<GameObject>(
            createFunc: () => Instantiate(arrowPrefab),
            actionOnGet: arrow => arrow.SetActive(true),
            actionOnRelease: arrow => arrow.SetActive(false),
            actionOnDestroy: arrow => Destroy(arrow),
            collectionCheck: false,   // Skip double-release checks in builds for perf
            defaultCapacity: 10,
            maxSize: maxPoolSize
        );
        
    }

    void Start()
    {
        path = new NavMeshPath();
        totalTime = switcherScript.GetTaskTimeLimit();
       // switcherScript.isCompletingATask.OnValueChanged += HandleTimeRemainingSlider;
    }

    //private void HandleTimeRemainingSlider(bool previousValue, bool newValue)
    //{   
    //    Debug.Log($"HandleTimeRemainingSlider called with newValue: {newValue}");

    //    if (newValue)
    //    {   
    //        Debug.Log($"Activating time remaining slider with totalTime: {totalTime}");
    //        timeRemainingSlider.value = 0;
    //        timeRemainingSlider.gameObject.SetActive(true);
    //        taskIndicaatorVFX.SetActive(true);
    //        timeRemainingSlider.DOValue(1f,totalTime).SetEase(Ease.Linear).OnComplete(() => timeRemainingSlider.gameObject.SetActive(false));

    //    }
    //    else
    //    {
    //        timeRemainingSlider.gameObject.SetActive(false);    
    //        taskIndicaatorVFX.SetActive(false);
    //    }
    //}

    void Update()
    {
        if (switcherScript == null || !switcherScript.IsOwner)
        {
            ReleaseAllArrows();
            return;
        }

        bool ownsAPole = switcherScript.ownedPoleType.Value != PoleType.None;
        bool hasTarget = switcherScript.targetPoleType.Value != PoleType.None;

        if (!ownsAPole || !hasTarget)
        {
            ReleaseAllArrows();
            return;
        }

        // Re-cache destination when target pole changes
        PoleType targetType = switcherScript.targetPoleType.Value;
        bool targetChanged = targetType != lastKnownTargetType;
        if (targetChanged)
        {
            lastKnownTargetType = targetType;
            GameObject poleObj = GameObject.Find(targetType.ToString() + "Pole");
            cachedTargetPosition = poleObj != null ? poleObj.transform.position : Vector3.zero;
        }

        // Throttle: only recalc if enough time passed, player moved enough, or target changed
        timeSinceLastRecalc += Time.deltaTime;
        bool timerElapsed = timeSinceLastRecalc >= recalcInterval;
        bool playerMoved = Vector3.Distance(switcherScript.transform.position, lastPlayerPosition) > moveThreshold;

        if (!timerElapsed && !playerMoved && !targetChanged)
            return;

        timeSinceLastRecalc = 0f;
        lastPlayerPosition = switcherScript.transform.position;

        // Sample nav mesh positions
        if (!NavMesh.SamplePosition(switcherScript.transform.position, out NavMeshHit startHit, 5f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(cachedTargetPosition, out NavMeshHit endHit, 5f, NavMesh.AllAreas))
        {
            ReleaseAllArrows();
            return;
        }

        NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path);

        if (path.status == NavMeshPathStatus.PathComplete)
            PlaceArrows(path.corners);
        else
            ReleaseAllArrows();
    }

    void PlaceArrows(Vector3[] corners)
    {
        // Release previously active arrows back to the pool before placing new ones
        ReleaseAllArrows();

        float distanceSinceLastArrow = 0f;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 segStart = corners[i];
            Vector3 segEnd = corners[i + 1];
            float segLen = Vector3.Distance(segStart, segEnd);
            Vector3 segDir = (segEnd - segStart).normalized;
            Quaternion rot = Quaternion.LookRotation(segDir, Vector3.up);

            float walked = 0f;
            float firstStepInSeg = arrowSpacing - distanceSinceLastArrow;

            for (float d = firstStepInSeg; d <= segLen; d += arrowSpacing)
            {
                // Stop if pool cap would be exceeded
                if (activeArrows.Count >= maxPoolSize) break;

                Vector3 pos = segStart + segDir * d;
                pos.y += arrowHeightOffset;

                GameObject arrow = arrowPool.Get();
                arrow.transform.SetPositionAndRotation(pos, rot);
                activeArrows.Add(arrow);

                walked = d;
            }

            distanceSinceLastArrow = segLen - walked;
            if (walked == 0f) distanceSinceLastArrow += segLen;

            if (activeArrows.Count >= maxPoolSize) break;
        }
    }

    void ReleaseAllArrows()
    {
        foreach (var arrow in activeArrows)
        {
            if (arrow != null)
                arrowPool.Release(arrow);
        }
        activeArrows.Clear();
    }

    void OnDisable() => ReleaseAllArrows();
    void OnDestroy() => ReleaseAllArrows();

    
}