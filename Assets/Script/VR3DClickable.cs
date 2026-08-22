using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

public class VR3DClickable : MonoBehaviour
{
    // The axes the drag movement is measured against.
    public enum DragSpace
    {
        // World axes, like the Global mode in the Unity Scene view.
        Global,

        // The model own axes, like the Local mode in the Unity Scene view.
        Local,

        // The axes of another transform, for example the camera or the hand.
        CustomTransform
    }

    // Which controller is read for the thumbstick.
    public enum ControllerHand
    {
        Right,
        Left,
        Both
    }

    // Which thumbstick direction pushes the model away.
    public enum ThumbstickAxis
    {
        // Push forward / pull back.
        Vertical,

        // Push right / left.
        Horizontal
    }

    // The direction the model travels when using the thumbstick.
    public enum DistanceDirection
    {
        // Along the line between the hand and the model.
        AwayFromHand,

        // Along the forward direction of the hand.
        HandForward
    }

    // Which rotation of the hand drives the model rotation.
    public enum HandRotationAxis
    {
        // Turning the hand left / right around the world up axis.
        Yaw,

        // Twisting the wrist around the hand forward axis.
        Roll,

        // Tilting the hand up / down around the hand right axis.
        Pitch
    }

    [Header("Click")]
    public UnityEvent OnClick;

    [Header("Hover")]
    public UnityEvent OnHoverEnter;
    public UnityEvent OnHoverExit;

    [Header("Drag")]
    public bool enableDrag = true;

    [Tooltip("Global = world axes. Local = the model own axes.")]
    public DragSpace dragSpace = DragSpace.Global;

    [Tooltip("Used only when Drag Space is set to Custom Transform.")]
    public Transform dragSpaceReference;

    [Tooltip("Unchecked = the axis is KEPT at its original value.")]
    public bool moveX = true;

    [Tooltip("Unchecked = the axis is KEPT at its original value.")]
    public bool moveY = true;

    [Tooltip("Unchecked = the axis is KEPT at its original value.")]
    public bool moveZ = false;

    [Tooltip(
        "Local / Custom only. Recalculates the axes every frame " +
        "instead of freezing them when the drag starts."
    )]
    public bool updateAxesWhileDragging = false;

    [Header("Hand Rotation While Dragging")]
    [Tooltip("Rotate the model by turning your hand while holding it.")]
    public bool enableHandRotation = true;

    [Tooltip(
        "The hand / controller transform. " +
        "If empty, the Hand Attach Point of the Model Controls Manager is used."
    )]
    public Transform handTransform;

    [Tooltip("Which hand rotation is used to rotate the model.")]
    public HandRotationAxis handRotationAxis = HandRotationAxis.Yaw;

    [Tooltip("1 = the model turns exactly like your hand. 2 = double.")]
    public float handRotationSensitivity = 1f;

    [Tooltip("Reverses the hand rotation direction.")]
    public bool invertHandRotation = false;

    [Tooltip("Ignores very small hand movements, in degrees per frame.")]
    public float handRotationDeadZone = 0.02f;

    [Tooltip("Safety clamp for the maximum degrees applied in one frame.")]
    public float maximumHandRotationPerFrame = 20f;

    [Tooltip("Prints the measured hand rotation in the Console to help debugging.")]
    public bool debugHandRotation = false;

    [Header("Push / Pull With Thumbstick While Dragging")]
    [Tooltip("Move the model closer or further from the hand using the thumbstick.")]
    public bool enableDistanceControl = true;

    [Tooltip("Which controller thumbstick is read.")]
    public ControllerHand distanceControlHand = ControllerHand.Both;

    [Tooltip("Vertical = push the stick forward / back.")]
    public ThumbstickAxis distanceThumbstickAxis = ThumbstickAxis.Vertical;

    [Tooltip("The line the model travels along.")]
    public DistanceDirection distanceDirection = DistanceDirection.AwayFromHand;

    [Tooltip("Meters per second at full stick push.")]
    public float distanceSpeed = 0.6f;

    [Tooltip("Ignores small stick movements.")]
    [Range(0f, 0.9f)]
    public float distanceDeadZone = 0.15f;

    [Tooltip("Reverses the push / pull direction.")]
    public bool invertDistanceControl = false;

    [Tooltip("The model can never get closer to the hand than this.")]
    public float minimumHandDistance = 0.15f;

    [Tooltip("The model can never get further from the hand than this.")]
    public float maximumHandDistance = 3f;

    [Tooltip("Optional: the point the model moves away from. Defaults to the Hand Transform.")]
    public Transform distanceReferencePoint;

    [Tooltip(
        "Measures the distance from the CENTER of the model instead of its Pivot, " +
        "like the Center / Pivot button in the Unity Scene view."
    )]
    public bool useModelCenterForDistance = true;

    [Tooltip("Editor only: Up / Down arrow keys also push and pull.")]
    public bool useKeyboardFallbackInEditor = true;

    [Tooltip("Prints the thumbstick value in the Console.")]
    public bool debugDistanceInput = false;

    [Header("Model Controls")]
    [Tooltip(
        "Manager used to select this model after Drag. " +
        "If empty, it will be found automatically."
    )]
    [SerializeField]
    private ModelControlsManager modelControlsManager;

    private Vector3 dragStartPosition;
    private Vector3 dragStartHitPoint;
    private Quaternion dragSpaceRotation = Quaternion.identity;

    private bool dragging;

    private Transform pointerTransform;

    private Quaternion previousHandRotation;
    private bool handRotationReady;

    private bool missingHandWarningSent;
    private float nextDebugLogTime;
    private float nextDistanceLogTime;

    private float externalDistanceInput;
    private bool hasExternalDistanceInput;

    private Vector3 cachedLocalCenter;
    private bool hasCachedLocalCenter;

    private void Awake()
    {
        FindControlsManagerIfNeeded();
    }

    private void FindControlsManagerIfNeeded()
    {
        if (modelControlsManager != null)
            return;

        modelControlsManager =
            FindFirstObjectByType<ModelControlsManager>();
    }

    public void PointerEnter()
    {
        OnHoverEnter?.Invoke();
    }

    public void PointerExit()
    {
        OnHoverExit?.Invoke();
    }

    public void Click()
    {
        Debug.Log(
            "CLICKED 3D MODEL: " + gameObject.name,
            gameObject
        );

        OnClick?.Invoke();
    }

    // =========================================================
    // DRAG
    // =========================================================

    // Optional: the raycaster can pass its own controller transform.
    public void SetPointerTransform(Transform pointer)
    {
        pointerTransform = pointer;
    }

    // Overload so the raycaster can send the controller with the hit point.
    public void BeginDrag(Vector3 hitPoint, Transform pointer)
    {
        pointerTransform = pointer;
        BeginDrag(hitPoint);
    }

    public void BeginDrag(Vector3 hitPoint)
    {
        if (!enableDrag)
            return;

        // If the raycaster calls BeginDrag every frame,
        // the hand tracking must NOT restart every frame.
        bool wasAlreadyDragging = dragging;

        dragging = true;

        dragStartPosition =
            transform.position;

        dragStartHitPoint =
            hitPoint;

        dragSpaceRotation =
            GetDragSpaceRotation();

        FindControlsManagerIfNeeded();

        if (modelControlsManager != null)
        {
            modelControlsManager.SetSelectedModel(
                transform
            );
        }

        if (!wasAlreadyDragging)
        {
            ResetHandRotationTracking();
        }

        if (wasAlreadyDragging)
            return;

        Debug.Log(
            "BEGIN DRAG + SELECT MODEL: " +
            gameObject.name,
            gameObject
        );
    }

    public void Drag(Vector3 currentHitPoint, Transform pointer)
    {
        pointerTransform = pointer;
        Drag(currentHitPoint);
    }

    public void Drag(Vector3 currentHitPoint)
    {
        if (!dragging ||
            !enableDrag)
        {
            return;
        }

        if (updateAxesWhileDragging)
        {
            dragSpaceRotation = GetDragSpaceRotation();
        }

        Vector3 worldDelta =
            currentHitPoint -
            dragStartHitPoint;

        // Bring the movement into the chosen axes.
        Vector3 axisDelta =
            Quaternion.Inverse(dragSpaceRotation) *
            worldDelta;

        // A disabled axis is simply KEPT at its original value.
        if (!moveX) axisDelta.x = 0f;
        if (!moveY) axisDelta.y = 0f;
        if (!moveZ) axisDelta.z = 0f;

        // Back to world space.
        Vector3 allowedWorldDelta =
            dragSpaceRotation *
            axisDelta;

        transform.position =
            dragStartPosition +
            allowedWorldDelta;
    }

    // Returns the rotation that defines the drag axes.
    private Quaternion GetDragSpaceRotation()
    {
        switch (dragSpace)
        {
            case DragSpace.Local:
                return transform.rotation;

            case DragSpace.CustomTransform:
                if (dragSpaceReference != null)
                    return dragSpaceReference.rotation;

                return Quaternion.identity;

            default:
                return Quaternion.identity;
        }
    }

    // =========================================================
    // DRAG SETTINGS API
    // =========================================================

    public void SetMoveX(bool value) { moveX = value; }
    public void SetMoveY(bool value) { moveY = value; }
    public void SetMoveZ(bool value) { moveZ = value; }

    public void SetKeepX(bool value) { moveX = !value; }
    public void SetKeepY(bool value) { moveY = !value; }
    public void SetKeepZ(bool value) { moveZ = !value; }

    public void SetMoveAxes(bool allowX, bool allowY, bool allowZ)
    {
        moveX = allowX;
        moveY = allowY;
        moveZ = allowZ;
    }

    // 0 = Global, 1 = Local, 2 = Custom Transform.
    public void SetDragSpace(int space)
    {
        dragSpace = (DragSpace)Mathf.Clamp(space, 0, 2);

        if (dragging)
            dragSpaceRotation = GetDragSpaceRotation();
    }

    public void UseGlobalDragSpace() { SetDragSpace(0); }
    public void UseLocalDragSpace() { SetDragSpace(1); }

    // Switches between Global and Local, like the Scene view button.
    public void ToggleGlobalLocalDragSpace()
    {
        SetDragSpace(dragSpace == DragSpace.Global ? 1 : 0);
    }

    public bool IsUsingLocalDragSpace()
    {
        return dragSpace == DragSpace.Local;
    }

    public void EndDrag()
    {
        dragging = false;
        handRotationReady = false;
    }

    public bool IsDragging()
    {
        return dragging;
    }

    // Keeps the drag reference in sync when something else moves the model.
    public void ApplyDragOffset(Vector3 worldPositionDelta)
    {
        if (!dragging)
            return;

        dragStartPosition += worldPositionDelta;
    }

    // =========================================================
    // HAND ROTATION
    // =========================================================

    private void Update()
    {
        // Nothing here works unless the model is being dragged,
        // so the thumbstick has no effect after you release it.
        if (!dragging)
        {
            hasExternalDistanceInput = false;
            externalDistanceInput = 0f;
            return;
        }

        if (enableHandRotation)
        {
            HandleHandRotation();
        }

        if (enableDistanceControl)
        {
            HandleDistanceControl();
        }

        hasExternalDistanceInput = false;
        externalDistanceInput = 0f;
    }

    // =========================================================
    // PUSH / PULL
    // =========================================================

    // Lets another input script drive the push / pull, from -1 to 1.
    public void SetDistanceInput(float value)
    {
        externalDistanceInput = Mathf.Clamp(value, -1f, 1f);
        hasExternalDistanceInput = true;
    }

    // Moves the model closer to or further from the hand.
    private void HandleDistanceControl()
    {
        float input = ReadDistanceInput();

        if (Mathf.Abs(input) < 0.0001f)
            return;

        Transform reference = ResolveDistanceReference();

        if (reference == null)
            return;

        Vector3 referencePosition = reference.position;

        // Measured from the model CENTER, not from the Transform pivot.
        Vector3 modelPoint = GetModelMeasurePoint();

        Vector3 towardModel = modelPoint - referencePosition;
        float currentDistance = towardModel.magnitude;

        Vector3 direction;

        if (distanceDirection == DistanceDirection.HandForward)
        {
            direction = reference.forward;
        }
        else
        {
            direction = currentDistance > 0.0001f
                ? towardModel / currentDistance
                : reference.forward;
        }

        if (direction.sqrMagnitude < 0.000001f)
            return;

        direction = direction.normalized;

        float targetDistance = Mathf.Clamp(
            currentDistance + (input * distanceSpeed * Time.deltaTime),
            minimumHandDistance,
            maximumHandDistance
        );

        float distanceChange = targetDistance - currentDistance;

        if (Mathf.Abs(distanceChange) < 0.000001f)
            return;

        Vector3 movement = direction * distanceChange;

        transform.position += movement;

        // The drag reference must follow, otherwise the drag
        // would pull the model straight back next frame.
        ApplyDragOffset(movement);
    }

    // Returns the point the model moves away from.
    private Transform ResolveDistanceReference()
    {
        if (distanceReferencePoint != null)
            return distanceReferencePoint;

        return ResolveHandTransform();
    }

    // Returns the point of the model used to measure the distance.
    public Vector3 GetModelMeasurePoint()
    {
        if (!useModelCenterForDistance)
            return transform.position;

        FindControlsManagerIfNeeded();

        if (modelControlsManager != null)
        {
            return modelControlsManager.GetModelBoundsCenterWorld(transform);
        }

        return transform.TransformPoint(GetLocalCenter());
    }

    // Calculates the visual center of the model in its own local space, once.
    private Vector3 GetLocalCenter()
    {
        if (hasCachedLocalCenter)
            return cachedLocalCenter;

        cachedLocalCenter = Vector3.zero;
        hasCachedLocalCenter = true;

        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
            return cachedLocalCenter;

        bool hasBounds = false;
        Bounds worldBounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return cachedLocalCenter;

        cachedLocalCenter =
            transform.InverseTransformPoint(worldBounds.center);

        return cachedLocalCenter;
    }

    // Forces the center to be calculated again.
    [ContextMenu("Recalculate Model Center")]
    public void ClearCachedModelCenter()
    {
        hasCachedLocalCenter = false;
    }

    // Reads the thumbstick, from -1 to 1.
    private float ReadDistanceInput()
    {
        float value = 0f;

        if (hasExternalDistanceInput)
        {
            value = externalDistanceInput;
        }
        else
        {
            if (distanceControlHand != ControllerHand.Left)
            {
                value += ReadThumbstick(XRNode.RightHand);
            }

            if (distanceControlHand != ControllerHand.Right)
            {
                value += ReadThumbstick(XRNode.LeftHand);
            }

            value = Mathf.Clamp(value, -1f, 1f);

            if (Mathf.Abs(value) < 0.0001f)
            {
                value = ReadKeyboardFallback();
            }
        }

        if (debugDistanceInput &&
            Time.unscaledTime >= nextDistanceLogTime)
        {
            nextDistanceLogTime = Time.unscaledTime + 0.25f;

            Debug.Log(
                "THUMBSTICK INPUT: " + value.ToString("F3"),
                gameObject
            );
        }

        if (Mathf.Abs(value) < distanceDeadZone)
            return 0f;

        // Removes the dead zone step so the movement starts smoothly.
        float sign = Mathf.Sign(value);

        value = sign *
            Mathf.InverseLerp(distanceDeadZone, 1f, Mathf.Abs(value));

        if (invertDistanceControl)
            value = -value;

        return value;
    }

    // Reads the thumbstick of one controller.
    private float ReadThumbstick(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);

        if (!device.isValid)
            return 0f;

        if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            return 0f;

        return distanceThumbstickAxis == ThumbstickAxis.Vertical
            ? axis.y
            : axis.x;
    }

    // Editor helper so the feature can be tested without a headset.
    private float ReadKeyboardFallback()
    {
        if (!useKeyboardFallbackInEditor)
            return 0f;

        if (!Application.isEditor)
            return 0f;

        try
        {
            if (Input.GetKey(KeyCode.UpArrow)) return 1f;
            if (Input.GetKey(KeyCode.DownArrow)) return -1f;
        }
        catch (System.Exception)
        {
            // The old Input Manager is disabled in this project.
            return 0f;
        }

        return 0f;
    }

    // Returns the transform used to read the hand rotation.
    private Transform ResolveHandTransform()
    {
        if (handTransform != null)
            return handTransform;

        if (pointerTransform != null)
            return pointerTransform;

        FindControlsManagerIfNeeded();

        if (modelControlsManager != null)
            return modelControlsManager.GetHandAttachPoint();

        return null;
    }

    private void ResetHandRotationTracking()
    {
        handRotationReady = false;

        Transform hand = ResolveHandTransform();

        if (hand == null)
            return;

        previousHandRotation = hand.rotation;
        handRotationReady = true;
    }

    // Reads how much the hand turned this frame and rotates the model.
    private void HandleHandRotation()
    {
        Transform hand = ResolveHandTransform();

        if (hand == null)
        {
            if (!missingHandWarningSent)
            {
                missingHandWarningSent = true;

                Debug.LogWarning(
                    "Hand Rotation is enabled but no Hand Transform was found on: " +
                    gameObject.name,
                    gameObject
                );
            }

            return;
        }

        if (!handRotationReady)
        {
            previousHandRotation = hand.rotation;
            handRotationReady = true;
            return;
        }

        Quaternion deltaRotation =
            hand.rotation *
            Quaternion.Inverse(previousHandRotation);

        previousHandRotation = hand.rotation;

        deltaRotation.ToAngleAxis(
            out float deltaAngle,
            out Vector3 deltaAxis
        );

        if (float.IsNaN(deltaAxis.x) ||
            float.IsInfinity(deltaAxis.x) ||
            deltaAxis.sqrMagnitude < 0.000001f)
        {
            return;
        }

        if (deltaAngle > 180f)
            deltaAngle -= 360f;

        if (Mathf.Abs(deltaAngle) < 0.0001f)
            return;

        Vector3 measureAxis = GetHandMeasureAxis(hand);

        if (measureAxis.sqrMagnitude < 0.000001f)
            return;

        // Only the part of the hand rotation around the chosen axis.
        float signedAngle =
            deltaAngle *
            Vector3.Dot(
                deltaAxis.normalized,
                measureAxis.normalized
            );

        signedAngle *= handRotationSensitivity;

        if (invertHandRotation)
            signedAngle = -signedAngle;

        if (Mathf.Abs(signedAngle) < handRotationDeadZone)
        {
            if (debugHandRotation &&
                Time.unscaledTime >= nextDebugLogTime)
            {
                nextDebugLogTime = Time.unscaledTime + 0.25f;

                Debug.Log(
                    "HAND ROTATION BLOCKED BY DEAD ZONE | Hand: " + hand.name +
                    " | Raw: " + deltaAngle.ToString("F4") +
                    " | Signed: " + signedAngle.ToString("F4"),
                    gameObject
                );
            }

            return;
        }

        signedAngle = Mathf.Clamp(
            signedAngle,
            -maximumHandRotationPerFrame,
            maximumHandRotationPerFrame
        );

        if (debugHandRotation &&
            Time.unscaledTime >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.unscaledTime + 0.25f;

            Debug.Log(
                "HAND ROTATION | Hand: " + hand.name +
                " | Raw: " + deltaAngle.ToString("F3") +
                " | Applied: " + signedAngle.ToString("F3"),
                gameObject
            );
        }

        RotateModelByAngle(signedAngle);
    }

    // Returns the world axis of the hand that is being measured.
    private Vector3 GetHandMeasureAxis(Transform hand)
    {
        switch (handRotationAxis)
        {
            case HandRotationAxis.Roll:
                return hand.forward;

            case HandRotationAxis.Pitch:
                return hand.right;

            default:
                return Vector3.up;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        if (!useModelCenterForDistance) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(GetModelMeasurePoint(), 0.02f);
    }

    // Rotates the model using the same pivot settings as the rotation buttons.
    private void RotateModelByAngle(float angleDegrees)
    {
        Vector3 positionBeforeRotation = transform.position;

        FindControlsManagerIfNeeded();

        if (modelControlsManager != null)
        {
            modelControlsManager.RotateModelAroundPivot(
                transform,
                angleDegrees
            );
        }
        else
        {
            transform.Rotate(
                Vector3.up,
                angleDegrees,
                Space.World
            );
        }

        // Rotating around a center moves the model,
        // so the drag reference must move with it.
        ApplyDragOffset(
            transform.position - positionBeforeRotation
        );
    }
}