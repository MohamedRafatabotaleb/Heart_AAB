using UnityEngine;

public class CircularArrowLoop : MonoBehaviour
{
    [Header("=== ARROW ===")]
    [Tooltip("The primary arrow prefab to be cloned")]
    public GameObject arrowPrefab;

    [Tooltip("Number of arrows present at the same time")]
    [Min(1)]
    public int arrowCount = 6;


    [Header("=== ROTATION CENTER ===")]
    [Tooltip("The center point around which the arrows will rotate")]
    public Transform rotationCenter;


    [Header("=== ARC SETTINGS ===")]
    [Tooltip("The starting angle of the arrow")]
    [Range(-360f, 360f)]
    public float startAngle = 10f;

    [Tooltip("The ending angle of the arrow")]
    [Range(-360f, 360f)]
    public float endAngle = 150f;

    [Tooltip("The rotation radius of the arrows")]
    [Min(0.01f)]
    public float radius = 3f;


    [Header("=== SPACING ===")]
    [Tooltip("Automatically calculate spacing based on arrow count and arc length")]
    public bool autoCalculateSpacing = true;

    [Tooltip("The distance between each arrow and the next on the movement path (Used if Auto Calculate Spacing is false)")]
    [Min(0f)]
    public float spacing = 0.8f;


    [Header("=== MOVEMENT ===")]
    [Tooltip("The direction of movement along the path")]
    public PathDirection pathDirection = PathDirection.StartToEnd;

    [Tooltip("Movement speed of the arrows in degrees per second")]
    public float speed = 60f;


    [Header("=== ROTATION ===")]
    [Tooltip("Make the arrow rotate according to the movement path")]
    public bool rotateWithMovement = true;

    [Tooltip("Direction the arrow should face")]
    public ArrowFacing arrowFacing = ArrowFacing.PathForward;

    [Tooltip("Modify the arrow rotation if its default orientation is incorrect")]
    public Vector3 rotationOffset;


    [Header("=== AXIS ===")]
    [Tooltip("The axis of rotation")]
    public RotationAxis rotationAxis = RotationAxis.Y;


    [Header("=== VISIBILITY & FADING ===")]
    [Tooltip("Hide the arrow when it reaches the end of the path, then show it from the beginning")]
    public bool hideAtEnd = true;

    [Tooltip("Enable fade in and fade out effects based on angle")]
    public bool enableFading = true;

    [Tooltip("The angle range (in degrees) over which the arrow fades in and out")]
    [Min(0.1f)]
    public float fadeAngle = 20f;


    [Header("=== GLOBAL FADE OUT ===")]
    [Tooltip("Duration in seconds for the global fade out effect")]
    [Min(0.1f)]
    public float globalFadeOutDuration = 1f;


    [Header("=== DEBUG ===")]
    public bool showGizmos = true;

    [Tooltip("Color of the debug gizmos")]
    public Color gizmoColor = Color.yellow;


    private GameObject[] arrows;
    private Renderer[] arrowRenderers;
    private Color[] arrowColors;

    private float arcLength;
    private float spacingInDegrees;

    private bool isFadingOut = false;
    private float globalAlpha = 1f;


    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    public enum ArrowFacing
    {
        PathForward,
        PathBackward,
        CenterInward,
        CenterOutward
    }

    public enum PathDirection
    {
        StartToEnd,
        EndToStart
    }


    private void Start()
    {
        if (arrowPrefab == null)
        {
            Debug.LogError("CircularArrowLoop: Please assign Arrow Prefab.");
            return;
        }

        if (rotationCenter == null)
        {
            Debug.LogError("CircularArrowLoop: Please assign Rotation Center.");
            return;
        }

        CreateArrows();
        CalculateSpacing();
    }


    private void Update()
    {
        if (arrows == null || arrows.Length == 0)
            return;

        // Process the global fade out animation over time
        if (isFadingOut)
        {
            globalAlpha -= Time.deltaTime / globalFadeOutDuration;
            globalAlpha = Mathf.Clamp01(globalAlpha);
        }

        MoveArrows();
    }


    // =========================================================
    // CREATE ARROWS
    // =========================================================

    private void CreateArrows()
    {
        arrows = new GameObject[arrowCount];
        arrowRenderers = new Renderer[arrowCount];
        arrowColors = new Color[arrowCount];

        for (int i = 0; i < arrowCount; i++)
        {
            GameObject arrow = Instantiate(arrowPrefab);
            arrow.name = "Arrow_" + i;
            arrows[i] = arrow;

            // Cache renderer and initial color for fading
            arrowRenderers[i] = arrow.GetComponentInChildren<Renderer>();

            if (arrowRenderers[i] != null)
            {
                // URP uses _BaseColor, Standard uses color
                if (arrowRenderers[i].material.HasProperty("_BaseColor"))
                {
                    arrowColors[i] = arrowRenderers[i].material.GetColor("_BaseColor");
                }
                else
                {
                    arrowColors[i] = arrowRenderers[i].material.color;
                }
            }
        }
    }


    // =========================================================
    // CALCULATE SPACING
    // =========================================================

    private void CalculateSpacing()
    {
        float totalAngle = Mathf.Abs(GetArcAngle());

        // Arc length
        arcLength = radius * totalAngle * Mathf.Deg2Rad;

        if (autoCalculateSpacing)
        {
            // Calculate spacing based on number of arrows and total arc angle
            if (arrowCount > 1)
            {
                spacingInDegrees = totalAngle / arrowCount;
            }
            else
            {
                spacingInDegrees = 0f;
            }
        }
        else
        {
            // Convert the required manual spacing distance to degrees
            if (radius > 0)
            {
                spacingInDegrees = (spacing / radius) * Mathf.Rad2Deg;
            }
            else
            {
                spacingInDegrees = 0f;
            }
        }
    }


    // =========================================================
    // MOVE ARROWS
    // =========================================================

    private void MoveArrows()
    {
        float arcDir = Mathf.Sign(endAngle - startAngle);
        float totalAngle = Mathf.Abs(endAngle - startAngle);

        float moveDir = (pathDirection == PathDirection.StartToEnd) ? arcDir : -arcDir;
        float originAngle = (pathDirection == PathDirection.StartToEnd) ? startAngle : endAngle;

        for (int i = 0; i < arrows.Length; i++)
        {
            if (arrows[i] == null)
                continue;

            // Each arrow is delayed from the one ahead of it
            float angleOffset = i * spacingInDegrees;

            float currentAngle = originAngle + (Time.time * speed * moveDir) - (angleOffset * moveDir);


            // =================================================
            // LOOP
            // =================================================

            if (moveDir > 0)
            {
                float targetAngle = Mathf.Max(startAngle, endAngle);
                float startWrap = Mathf.Min(startAngle, endAngle) - totalAngle;

                while (currentAngle > targetAngle)
                {
                    currentAngle -= totalAngle;
                }

                while (currentAngle < startWrap)
                {
                    currentAngle += totalAngle;
                }
            }
            else
            {
                float targetAngle = Mathf.Min(startAngle, endAngle);
                float startWrap = Mathf.Max(startAngle, endAngle) + totalAngle;

                while (currentAngle < targetAngle)
                {
                    currentAngle += totalAngle;
                }

                while (currentAngle > startWrap)
                {
                    currentAngle -= totalAngle;
                }
            }


            // =================================================
            // CALCULATE POSITION
            // =================================================

            Vector3 position = GetPositionOnCircle(currentAngle);
            arrows[i].transform.position = position;


            // =================================================
            // ROTATION
            // =================================================

            if (rotateWithMovement)
            {
                Quaternion movementRotation = GetMovementRotation(currentAngle, moveDir);

                arrows[i].transform.rotation =
                    movementRotation *
                    Quaternion.Euler(rotationOffset);
            }
            else
            {
                arrows[i].transform.rotation = Quaternion.Euler(rotationOffset);
            }


            // =================================================
            // VISIBILITY & FADING
            // =================================================

            UpdateVisibility(arrows[i], i, currentAngle);
        }
    }


    // =========================================================
    // POSITION ON CIRCLE
    // =========================================================

    private Vector3 GetPositionOnCircle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 center = rotationCenter.position;

        switch (rotationAxis)
        {
            case RotationAxis.X:
                return center + new Vector3(0, Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);

            case RotationAxis.Y:
                return center + new Vector3(Mathf.Cos(radians) * radius, 0, Mathf.Sin(radians) * radius);

            case RotationAxis.Z:
                return center + new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0);
        }

        return center;
    }


    // =========================================================
    // ARROW ROTATION
    // =========================================================

    private Quaternion GetMovementRotation(float angle, float moveDir)
    {
        float smallStep = 0.1f;

        Vector3 currentPosition = GetPositionOnCircle(angle);
        Vector3 nextPosition = GetPositionOnCircle(angle + smallStep * moveDir);

        Vector3 movementDirection = (nextPosition - currentPosition).normalized;
        Vector3 finalDirection = Vector3.zero;

        // Apply facing direction logic
        switch (arrowFacing)
        {
            case ArrowFacing.PathForward:
                finalDirection = movementDirection;
                break;

            case ArrowFacing.PathBackward:
                finalDirection = -movementDirection;
                break;

            case ArrowFacing.CenterInward:
                finalDirection = (rotationCenter.position - currentPosition).normalized;
                break;

            case ArrowFacing.CenterOutward:
                finalDirection = (currentPosition - rotationCenter.position).normalized;
                break;
        }

        if (finalDirection == Vector3.zero)
            return Quaternion.identity;

        switch (rotationAxis)
        {
            case RotationAxis.Y:
                return Quaternion.LookRotation(finalDirection, Vector3.up);

            case RotationAxis.X:
                return Quaternion.LookRotation(finalDirection, Vector3.forward);

            case RotationAxis.Z:
                return Quaternion.LookRotation(finalDirection, Vector3.forward);
        }

        return Quaternion.identity;
    }


    // =========================================================
    // VISIBILITY & FADING
    // =========================================================

    private void UpdateVisibility(GameObject arrow, int index, float angle)
    {
        bool visible = true;

        if (hideAtEnd)
        {
            float min = Mathf.Min(startAngle, endAngle);
            float max = Mathf.Max(startAngle, endAngle);
            visible = angle >= min && angle <= max;
        }

        // Apply fading logic if visible and renderer exists
        if (visible && arrowRenderers[index] != null)
        {
            float alphaProgress = 1f;

            if (enableFading)
            {
                float distToStart = Mathf.Abs(angle - startAngle);
                float distToEnd = Mathf.Abs(angle - endAngle);

                // Get the smallest distance to either the start or end of the arc
                float minDistance = Mathf.Min(distToStart, distToEnd);

                // Calculate alpha from 0 to 1 based on fadeAngle
                alphaProgress = Mathf.Clamp01(minDistance / fadeAngle);
            }

            // Apply global fade out multiplier
            alphaProgress *= globalAlpha;

            Color currentColor = arrowColors[index];
            currentColor.a *= alphaProgress;

            if (arrowRenderers[index].material.HasProperty("_BaseColor"))
            {
                arrowRenderers[index].material.SetColor("_BaseColor", currentColor);
            }
            else
            {
                arrowRenderers[index].material.color = currentColor;
            }

            // Hide the object completely if its alpha drops to 0
            if (alphaProgress <= 0f)
            {
                visible = false;
            }
        }

        if (arrow.activeSelf != visible)
        {
            arrow.SetActive(visible);
        }
    }


    // =========================================================
    // ARC ANGLE
    // =========================================================

    private float GetArcAngle()
    {
        float difference = endAngle - startAngle;
        return difference;
    }


    // =========================================================
    // GLOBAL FADE OUT TRIGGERS
    // =========================================================

    public void TriggerFadeOut()
    {
        isFadingOut = true;
    }

    public void ResetFadeOut()
    {
        isFadingOut = false;
        globalAlpha = 1f;
    }


    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        if (!showGizmos || rotationCenter == null)
            return;

        // Apply the color to Gizmos
        Gizmos.color = gizmoColor;

        Gizmos.DrawWireSphere(rotationCenter.position, 0.12f);

        int segments = 50;
        Vector3 previous = GetPositionOnCircle(startAngle);

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            Vector3 current = GetPositionOnCircle(angle);

            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}