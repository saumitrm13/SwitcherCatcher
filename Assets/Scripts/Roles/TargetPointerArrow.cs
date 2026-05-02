using UnityEngine;

public class TargetPointerArrow : MonoBehaviour
{
    // Drag the SwitcherScript component here in the Inspector
    [SerializeField] SwitcherScript switcherScript;

    // Drag the Arrow Image's RectTransform here
    [SerializeField] RectTransform arrowRectTransform;

    // The world-space canvas RectTransform (SwitcherBodyCanvas)
    [SerializeField] RectTransform canvasRectTransform;

    [SerializeField] Transform origin;

    private PoleType lastKnownTargetType = PoleType.None;
    private Transform cachedTargetTransform = null;
    [SerializeField] float circleRadius = 50f;

    void Update()
    {
        if (switcherScript == null || !switcherScript.IsOwner)
        {
            arrowRectTransform.gameObject.SetActive(false);
            return;
        }

        bool ownsAPole = switcherScript.ownedPoleType.Value != PoleType.None;
        bool hasTarget = switcherScript.targetPoleType.Value != PoleType.None;

        if (!ownsAPole || !hasTarget)
        {
            arrowRectTransform.gameObject.SetActive(false);
            return;
        }

        arrowRectTransform.gameObject.SetActive(true);

        PoleType targetType = switcherScript.targetPoleType.Value;
        if (targetType != lastKnownTargetType || cachedTargetTransform == null)
        {
            lastKnownTargetType = targetType;
            string poleName = targetType.ToString() + "Pole";
            GameObject poleObj = GameObject.Find(poleName);
            cachedTargetTransform = poleObj != null ? poleObj.transform : null;
        }

        if (cachedTargetTransform == null) return;

        PointArrowAt(cachedTargetTransform.localPosition);
    }

    void PointArrowAt(Vector3 worldTargetPosition)
    {
        Debug.Log($"World Target : {worldTargetPosition.x}, {worldTargetPosition.y}, {worldTargetPosition.z}");
        Vector3 worldDirection = worldTargetPosition - origin.position;
        worldDirection.y = 0f;
        Debug.Log($"World Direction : {worldDirection.x}, {worldDirection.y}, {worldDirection.z}");
        if (worldDirection.sqrMagnitude < 0.001f) return;
        Debug.Log($"World direction magnitude : {worldDirection.sqrMagnitude}");
        Vector3 canvasRight = canvasRectTransform.right;
        Vector3 canvasUp = -canvasRectTransform.forward; // forward is the depth axis of a vertical canvas
        float x = Vector3.Dot(worldDirection.normalized, canvasRight);
        float y = Vector3.Dot(worldDirection.normalized, canvasUp);

       

        float angleDegrees = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        Debug.Log($"Angle : {angleDegrees}");
        // Rotation — arrow sprite points toward target
        arrowRectTransform.localRotation = Quaternion.Euler(0f, 0f, angleDegrees - 90f);

        // Position — move along the circle at the same angle
        float angleRad = angleDegrees * Mathf.Deg2Rad;
        arrowRectTransform.localPosition = new Vector3(
            Mathf.Cos(angleRad) * circleRadius,
            3.849f,
            Mathf.Sin(angleRad) * circleRadius
        );
    }
}