using System.Collections;
using UnityEngine;

public class ResourceCollectionRoutine : MonoBehaviour
{
    [Header("Settings")]
    public GameObject targetObject;
    public float timeToMatchPosition = 1.5f;

    [Tooltip("How close the rotation must be (in degrees) to be considered matched")]
    public float rotationMatchThreshold = 0.5f;

    public Vector3 startPos;
    private Coroutine _matchRoutine;
    private void OnEnable()
    {   
        transform.localPosition = startPos;
        StartMatching();
    }
    /// <summary>
    /// Teleports to startPos, then lerps position to target over timeToMatchPosition seconds,
    /// continuously syncs rotation to the (animating) target, and disables self once both match.
    /// </summary>
    public void StartMatching()
    {
        if (_matchRoutine != null)
            StopCoroutine(_matchRoutine);

        _matchRoutine = StartCoroutine(MatchRoutine());
    }

    private IEnumerator MatchRoutine()
    {
        // ── Step 1: Snap to start position immediately ──────────────────────
        //transform.position = startPos;

        // ── Step 2: Lerp position → target over timeToMatchPosition seconds ─
        // Rotation is continuously updated throughout, since the target animates.
        float elapsed = 0f;
        Vector3 targetPos = targetObject.transform.position; // position won't change, cache it

        while (elapsed < timeToMatchPosition && Vector3.Distance(transform.position,targetPos)>0.5f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / timeToMatchPosition);

            // Position: smooth lerp to the cached target position
            transform.position = Vector3.Lerp(transform.position, targetPos, t);

            // Rotation: copy the target's CURRENT rotation every frame (it keeps changing)
            transform.rotation = targetObject.transform.rotation;

            yield return null;
        }

        // Snap position exactly to target (eliminates floating point drift)
        transform.position = targetPos;

        // ── Step 3: Position matched — wait until rotation is also matched ──
        // The target's animation may still be mid-transition, so we keep
        // updating rotation until it settles within the threshold.
        //while (true)
        //{
        //    transform.rotation = targetObject.transform.rotation;

        //    float angleDiff = Quaternion.Angle(transform.rotation, targetObject.transform.rotation);
        //    if (angleDiff < rotationMatchThreshold)
        //        break;

        //    yield return null;
        //}

        // ── Step 4: Both matched → disable self ────────────────────────────
        gameObject.SetActive(false);
        _matchRoutine = null;
    }

    // Optional: call this to abort mid-flight
    public void CancelMatching()
    {
        if (_matchRoutine != null)
        {
            StopCoroutine(_matchRoutine);
            _matchRoutine = null;
        }
    }
}