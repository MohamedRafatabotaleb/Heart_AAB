using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Manages the rotation, scale, and positioning of 3D models.
[DisallowMultipleComponent]
public class ModelControlsManager : MonoBehaviour
{
    // Enum defining available control actions.
    public enum ControlAction
    {
        RotateRight, RotateLeft, ScaleUp, ScaleDown, AttachToHand, ReleaseFromHand, Reset
    }

    // Enum defining the point the model rotates around.
    public enum RotationPivotMode
    {
        // Rotates around the Transform pivot (default Unity behaviour).
        TransformPivot,

        // Rotates around the visual center of all renderers.
        RendererBoundsCenter,

        // Rotates around another Transform in the scene.
        CustomTransform,

        // Rotates around a manual local offset from the Transform pivot.
        CustomLocalOffset
    }

    // Group mapping an action to a list of UI buttons.
    [Serializable]
    public class ButtonControlGroup
    {
        // The action assigned to these buttons.
        public ControlAction controlAction;

        // The list of buttons that trigger this action.
        public List<Button> buttons = new List<Button>();
    }

    // Binds EventTrigger entries for UI interactions.
    private class TriggerBinding
    {
        // The EventTrigger component on the UI element.
        public EventTrigger eventTrigger;

        // The list of active trigger entries.
        public List<EventTrigger.Entry> entries = new List<EventTrigger.Entry>();
    }

    // Stores the initial transform state of draggable objects.
    private class DraggableInitialState
    {
        // Reference to the draggable component.
        public VR3DClickable clickable;

        // The transform of the draggable object.
        public Transform transform;

        // The original parent transform.
        public Transform initialParent;

        // The original local position.
        public Vector3 initialLocalPosition;

        // The original local rotation.
        public Quaternion initialLocalRotation;

        // The original local scale.
        public Vector3 initialLocalScale;

        // The original world scale.
        public Vector3 initialWorldScale;
    }

    // Stores the state of colliders to restore them later.
    private class ColliderState
    {
        // The collider component.
        public Collider collider;

        // Whether the collider was enabled originally.
        public bool wasEnabled;
    }

    // The main model that will be controlled.
    [Header("Target Model")]
    [SerializeField] private Transform targetModel;

    // The last model dragged by the user.
    [Header("Selected Model")]
    [SerializeField] private Transform selectedModel;

    // The attachment point under the hand controller.
    [Header("Hand Attach")]
    [SerializeField] private Transform handAttachPoint;

    // Position of the model relative to the hand attach point.
    [SerializeField] private Vector3 handLocalPosition = Vector3.zero;

    // Rotation of the model relative to the hand attach point.
    [SerializeField] private Vector3 handLocalEulerAngles = Vector3.zero;

    // How long the selected model takes to move into the hand.
    [Header("Smooth Hand Attach")]
    [SerializeField] private float handAttachDuration = 0.5f;

    // The easing curve for the hand attach animation.
    [SerializeField] private AnimationCurve handAttachEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Blocks rotation and scaling while attaching to hand.
    [SerializeField] private bool blockControlsDuringHandAttach = true;

    // List of grouped buttons for controlling actions.
    [Header("Button Control Groups")]
    [SerializeField] private List<ButtonControlGroup> buttonControlGroups = new List<ButtonControlGroup>();

    // Rotation speed multiplier.
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 100f;

    // The axis around which the model rotates.
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    // Determines if rotation happens in local or world space.
    [SerializeField] private bool useLocalRotation = true;

    // Reverses the rotation direction if enabled.
    [SerializeField] private bool invertRotationDirection = false;

    // Defines what point the model rotates around.
    [Header("Rotation Pivot")]
    [SerializeField] private RotationPivotMode rotationPivotMode = RotationPivotMode.RendererBoundsCenter;

    // Optional transform used as the pivot when Custom Transform mode is selected.
    [SerializeField] private Transform customRotationPivot;

    // Optional local offset used as the pivot when Custom Local Offset mode is selected.
    [SerializeField] private Vector3 customRotationPivotLocalOffset = Vector3.zero;

    // Includes disabled renderers when calculating the bounds center.
    [SerializeField] private bool includeInactiveRenderers = true;

    // Scales the model around the same pivot instead of the transform pivot.
    [SerializeField] private bool scaleAroundRotationPivot = true;

    // Draws the pivot point in the Scene view while this object is selected.
    [SerializeField] private bool showRotationPivotGizmo = true;

    // Time it takes to reach full rotation speed.
    [Header("Rotation Ease Settings")]
    [SerializeField] private float rotationEaseInTime = 0.2f;

    // Time it takes to stop rotating after releasing.
    [SerializeField] private float rotationEaseOutTime = 0.4f;

    // Speed multiplier for scaling up and down.
    [Header("Scale Settings")]
    [SerializeField] private float scaleSpeed = 1f;

    // The absolute minimum scale value the model can reach in the world.
    [Header("Absolute Scale Limits")]
    [SerializeField] private float absoluteMinimumScale = 0.5f;

    // The absolute maximum scale value the model can reach in the world.
    [SerializeField] private float absoluteMaximumScale = 5f;

    // The absolute scale the model will smoothly transition to on start.
    [Header("Start Scale Transition")]
    [SerializeField] private float targetStartScale = 1f;

    // How long the start scale transition takes.
    [SerializeField] private float startScaleDuration = 1.5f;

    // The curve used for the smooth scale transition at start.
    [SerializeField] private AnimationCurve startScaleEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Blocks other controls while the initial scale transition is happening.
    [SerializeField] private bool blockControlsDuringStart = true;

    // Time it takes to reach full scale speed.
    [Header("Scale Ease Settings")]
    [SerializeField] private float scaleEaseInTime = 0.2f;

    // Time it takes to stop scaling after releasing.
    [SerializeField] private float scaleEaseOutTime = 0.4f;

    // Duration of the smooth reset animation.
    [Header("Smooth Reset Settings")]
    [SerializeField] private float resetDuration = 0.8f;

    // The easing curve for the smooth reset animation.
    [SerializeField] private AnimationCurve resetEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Blocks rotation and scaling while resetting.
    [SerializeField] private bool blockControlsDuringReset = true;

    // Automatically finds draggable child models to track.
    [Header("Drag Reset")]
    [SerializeField] private bool autoFindDraggableModels = true;

    // Optional list of external draggable models to track.
    [SerializeField] private List<VR3DClickable> extraDraggableModels = new List<VR3DClickable>();

    // Tracks the original local position of the target model.
    private Vector3 initialLocalPosition;

    // Tracks the original local rotation of the target model.
    private Quaternion initialLocalRotation;

    // Tracks the original local scale of the target model.
    private Vector3 initialLocalScale;

    // Tracks the original world scale of the target model.
    private Vector3 initialWorldScale;

    // The current calculated multiplier applied to the base scale.
    private float currentScaleMultiplier = 1f;

    // Counts active presses for rotating right.
    private int rotateRightPressCount;

    // Counts active presses for rotating left.
    private int rotateLeftPressCount;

    // Counts active presses for scaling up.
    private int scaleUpPressCount;

    // Counts active presses for scaling down.
    private int scaleDownPressCount;

    // Current speed of rotation taking ease into account.
    private float currentRotationSpeed;

    // Internal velocity tracker for smooth damping rotation.
    private float rotationSmoothVelocity;

    // Current speed of scaling taking ease into account.
    private float currentScaleSpeed;

    // Internal velocity tracker for smooth damping scaling.
    private float scaleSmoothVelocity;

    // Reference to the active reset coroutine.
    private Coroutine resetCoroutine;

    // Flag indicating if a reset animation is playing.
    private bool isResetting;

    // Reference to the active hand attach coroutine.
    private Coroutine handAttachCoroutine;

    // Flag indicating if the hand attach animation is playing.
    private bool isAttachingToHand;

    // Flag indicating if the start scale transition is playing.
    private bool isStartingScale;

    // List of all created UI trigger bindings for cleanup.
    private readonly List<TriggerBinding> triggerBindings = new List<TriggerBinding>();

    // List of original states for all draggable models.
    private readonly List<DraggableInitialState> draggableInitialStates = new List<DraggableInitialState>();

    // Dictionary mapping models to their saved collider states.
    private readonly Dictionary<Transform, List<ColliderState>> savedColliderStates = new Dictionary<Transform, List<ColliderState>>();

    // Dictionary caching the local space bounds center of every model.
    private readonly Dictionary<Transform, Vector3> cachedLocalBoundsCenters = new Dictionary<Transform, Vector3>();

    // Called when the script instance is being loaded.
    private void Awake()
    {
        if (targetModel == null)
        {
            Debug.LogError("Target Model is not assigned.", gameObject);
            enabled = false;
            return;
        }

        ClearRotationPivotCache();
        SaveInitialTransform();
        SaveInitialDraggableTransforms();
        SetupButtons();
    }

    // Called before the first frame update to trigger the start scale.
    private void Start()
    {
        if (targetModel != null && startScaleDuration > 0f)
        {
            StartCoroutine(SmoothStartScaleRoutine());
        }
    }

    // Updates rotation and scale every frame based on input.
    private void Update()
    {
        if (isStartingScale && blockControlsDuringStart) return;
        if (isResetting && blockControlsDuringReset) return;
        if (isAttachingToHand && blockControlsDuringHandAttach) return;

        HandleRotation();
        HandleScale();
    }

    // Coroutine that smoothly scales the model to the target start scale.
    private IEnumerator SmoothStartScaleRoutine()
    {
        isStartingScale = true;
        float elapsedTime = 0f;
        float startMultiplier = currentScaleMultiplier;

        float baseScale = Mathf.Abs(initialWorldScale.x) > 0.0001f ? Mathf.Abs(initialWorldScale.x) : 1f;
        float targetMultiplier = targetStartScale / baseScale;

        float minMult = absoluteMinimumScale / baseScale;
        float maxMult = absoluteMaximumScale / baseScale;
        targetMultiplier = Mathf.Clamp(targetMultiplier, minMult, maxMult);

        while (elapsedTime < startScaleDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / startScaleDuration);
            float easedTime = startScaleEaseCurve.Evaluate(normalizedTime);

            currentScaleMultiplier = Mathf.Lerp(startMultiplier, targetMultiplier, easedTime);
            Vector3 desiredWorldScale = initialWorldScale * currentScaleMultiplier;
            ApplyWorldScaleKeepingPivot(targetModel, desiredWorldScale);

            yield return null;
        }

        currentScaleMultiplier = targetMultiplier;
        ApplyWorldScaleKeepingPivot(targetModel, initialWorldScale * currentScaleMultiplier);

        initialLocalScale = targetModel.localScale;
        initialWorldScale = targetModel.lossyScale;

        isStartingScale = false;
    }

    // =========================================================
    // SELECTED MODEL
    // =========================================================

    // Assigns a model to be the actively selected one.
    public void SetSelectedModel(Transform model)
    {
        if (model == null) return;

        selectedModel = model;
        CalculateCurrentScaleMultiplierForTarget(GetScaleTarget());
        Debug.Log("Selected Model: " + selectedModel.name, selectedModel.gameObject);
    }

    // Clears the actively selected model.
    public void ClearSelectedModel()
    {
        selectedModel = null;
        CalculateCurrentScaleMultiplierForTarget(targetModel);
    }

    // Returns the currently selected model transform.
    public Transform GetSelectedModel()
    {
        return selectedModel;
    }

    // Determines the appropriate target for rotation.
    private Transform GetRotationTarget()
    {
        if (selectedModel != null) return selectedModel;
        return targetModel;
    }

    // Determines the appropriate target for scaling.
    private Transform GetScaleTarget()
    {
        if (selectedModel != null) return selectedModel;
        return targetModel;
    }

    // =========================================================
    // HAND ATTACH
    // =========================================================

    // Initiates attaching the selected model to the hand.
    public void AttachSelectedModelToHand()
    {
        if (selectedModel == null)
        {
            Debug.LogWarning("No Selected Model. Drag a model first.", gameObject);
            return;
        }

        if (handAttachPoint == null)
        {
            Debug.LogWarning("Hand Attach Point is not assigned.", gameObject);
            return;
        }

        if (handAttachCoroutine != null)
        {
            StopCoroutine(handAttachCoroutine);
            handAttachCoroutine = null;
        }

        handAttachCoroutine = StartCoroutine(SmoothAttachSelectedModelToHandRoutine());
    }

    // Coroutine that smoothly animates the model to the hand attachment point.
    private IEnumerator SmoothAttachSelectedModelToHandRoutine()
    {
        if (selectedModel == null || handAttachPoint == null)
        {
            handAttachCoroutine = null;
            yield break;
        }

        isAttachingToHand = true;
        StopAllActions();
        StopMovementImmediately();

        Transform model = selectedModel;
        VR3DClickable clickable = model.GetComponent<VR3DClickable>();

        if (clickable != null)
        {
            clickable.EndDrag();
        }

        DisableModelColliders(model);

        Vector3 startWorldPosition = model.position;
        Quaternion startWorldRotation = model.rotation;
        Vector3 startWorldScale = model.lossyScale;

        if (handAttachDuration <= 0f)
        {
            FinishAttachToHand(model, startWorldScale);
            isAttachingToHand = false;
            handAttachCoroutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < handAttachDuration)
        {
            if (model == null || selectedModel != model || handAttachPoint == null)
            {
                isAttachingToHand = false;
                handAttachCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / handAttachDuration);
            float easedTime = handAttachEaseCurve.Evaluate(normalizedTime);

            Vector3 targetWorldPosition = handAttachPoint.TransformPoint(handLocalPosition);
            Quaternion targetWorldRotation = handAttachPoint.rotation * Quaternion.Euler(handLocalEulerAngles);

            model.position = Vector3.LerpUnclamped(startWorldPosition, targetWorldPosition, easedTime);
            model.rotation = Quaternion.SlerpUnclamped(startWorldRotation, targetWorldRotation, easedTime);

            SetWorldScale(model, startWorldScale);
            yield return null;
        }

        if (model != null && selectedModel == model && handAttachPoint != null)
        {
            FinishAttachToHand(model, startWorldScale);
        }

        isAttachingToHand = false;
        handAttachCoroutine = null;
    }

    // Finalizes the attachment of the model to the hand.
    private void FinishAttachToHand(Transform model, Vector3 preservedWorldScale)
    {
        if (model == null || handAttachPoint == null) return;

        model.SetParent(handAttachPoint, true);
        model.localPosition = handLocalPosition;
        model.localRotation = Quaternion.Euler(handLocalEulerAngles);

        SetWorldScale(model, preservedWorldScale);
        CalculateCurrentScaleMultiplierForTarget(model);

        Debug.Log("Smoothly Attached To Hand: " + model.name, model.gameObject);
    }

    // Applies a desired world scale to a transform safely.
    private void SetWorldScale(Transform model, Vector3 desiredWorldScale)
    {
        if (model == null) return;

        Transform parent = model.parent;
        if (parent == null)
        {
            model.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        model.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentScale.x),
            SafeDivide(desiredWorldScale.y, parentScale.y),
            SafeDivide(desiredWorldScale.z, parentScale.z)
        );
    }

    // Safely divides two floats to avoid divide-by-zero errors.
    private float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.00001f) return value;
        return value / divisor;
    }

    // Releases the currently selected model back to its original parent.
    public void ReleaseSelectedModelFromHand()
    {
        CancelHandAttach();

        if (selectedModel == null)
        {
            Debug.LogWarning("No Selected Model.", gameObject);
            return;
        }

        DraggableInitialState state = FindInitialState(selectedModel);

        if (state == null)
        {
            Debug.LogWarning("Initial state was not found for: " + selectedModel.name, selectedModel.gameObject);
            return;
        }

        selectedModel.SetParent(state.initialParent, true);
        RestoreModelColliders(selectedModel);
        CalculateCurrentScaleMultiplierForTarget(selectedModel);

        Debug.Log("Released From Hand: " + selectedModel.name, selectedModel.gameObject);
    }

    // Checks if a specific model is attached to the hand.
    private bool IsAttachedToHand(Transform model)
    {
        if (model == null || handAttachPoint == null) return false;
        return model.parent == handAttachPoint;
    }

    // =========================================================
    // BUTTON SETUP
    // =========================================================

    // Configures UI buttons with the required event triggers.
    private void SetupButtons()
    {
        RemoveCreatedTriggerEntries();

        foreach (ButtonControlGroup controlGroup in buttonControlGroups)
        {
            if (controlGroup == null || controlGroup.buttons == null) continue;

            ControlAction selectedAction = controlGroup.controlAction;

            foreach (Button button in controlGroup.buttons)
            {
                if (button == null) continue;
                SetupButton(button, selectedAction);
            }
        }
    }

    // Sets up individual pointer events for a specific button.
    private void SetupButton(Button button, ControlAction action)
    {
        EventTrigger eventTrigger = button.GetComponent<EventTrigger>();
        if (eventTrigger == null) eventTrigger = button.gameObject.AddComponent<EventTrigger>();

        if (eventTrigger.triggers == null) eventTrigger.triggers = new List<EventTrigger.Entry>();

        TriggerBinding binding = new TriggerBinding { eventTrigger = eventTrigger };

        AddTriggerEntry(binding, EventTriggerType.PointerDown, delegate { StartAction(action); });
        AddTriggerEntry(binding, EventTriggerType.PointerUp, delegate { StopAction(action); });
        AddTriggerEntry(binding, EventTriggerType.PointerExit, delegate { StopAction(action); });
        AddTriggerEntry(binding, EventTriggerType.Cancel, delegate { StopAction(action); });

        triggerBindings.Add(binding);
    }

    // Adds a specific event entry to the EventTrigger binding.
    private void AddTriggerEntry(TriggerBinding binding, EventTriggerType eventType, Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = eventType;
        entry.callback.AddListener(delegate (BaseEventData eventData) { callback(eventData); });

        binding.eventTrigger.triggers.Add(entry);
        binding.entries.Add(entry);
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    // Starts executing the requested control action.
    private void StartAction(ControlAction action)
    {
        if (action == ControlAction.Reset)
        {
            SmoothResetModel();
            return;
        }

        if (action == ControlAction.AttachToHand)
        {
            AttachSelectedModelToHand();
            return;
        }

        if (action == ControlAction.ReleaseFromHand)
        {
            ReleaseSelectedModelFromHand();
            return;
        }

        if (isResetting)
        {
            if (blockControlsDuringReset) return;
            CancelReset();
        }

        switch (action)
        {
            case ControlAction.RotateRight: rotateRightPressCount++; break;
            case ControlAction.RotateLeft: rotateLeftPressCount++; break;
            case ControlAction.ScaleUp: scaleUpPressCount++; break;
            case ControlAction.ScaleDown: scaleDownPressCount++; break;
        }
    }

    // Stops executing the requested control action.
    private void StopAction(ControlAction action)
    {
        switch (action)
        {
            case ControlAction.RotateRight: rotateRightPressCount = Mathf.Max(0, rotateRightPressCount - 1); break;
            case ControlAction.RotateLeft: rotateLeftPressCount = Mathf.Max(0, rotateLeftPressCount - 1); break;
            case ControlAction.ScaleUp: scaleUpPressCount = Mathf.Max(0, scaleUpPressCount - 1); break;
            case ControlAction.ScaleDown: scaleDownPressCount = Mathf.Max(0, scaleDownPressCount - 1); break;
        }
    }

    // =========================================================
    // ROTATION
    // =========================================================

    // Calculates and applies rotation based on input state.
    private void HandleRotation()
    {
        float direction = 0f;
        if (rotateRightPressCount > 0) direction -= 1f;
        if (rotateLeftPressCount > 0) direction += 1f;

        if (invertRotationDirection) direction *= -1f;

        float targetRotationSpeed = direction * rotationSpeed;
        float smoothTime = Mathf.Abs(targetRotationSpeed) > 0.001f ? rotationEaseInTime : rotationEaseOutTime;

        if (smoothTime <= 0f)
        {
            currentRotationSpeed = targetRotationSpeed;
            rotationSmoothVelocity = 0f;
        }
        else
        {
            currentRotationSpeed = Mathf.SmoothDamp(currentRotationSpeed, targetRotationSpeed, ref rotationSmoothVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
        }

        if (Mathf.Abs(currentRotationSpeed) < 0.01f && Mathf.Abs(targetRotationSpeed) < 0.01f)
        {
            currentRotationSpeed = 0f;
            rotationSmoothVelocity = 0f;
            return;
        }

        Transform rotationTarget = GetRotationTarget();
        if (rotationTarget == null) return;

        float rotationAngle = currentRotationSpeed * Time.deltaTime;

        if (rotationPivotMode == RotationPivotMode.TransformPivot)
        {
            Space rotationSpace = useLocalRotation ? Space.Self : Space.World;
            rotationTarget.Rotate(rotationAxis.normalized, rotationAngle, rotationSpace);
            return;
        }

        Vector3 pivotWorldPosition = GetRotationPivotWorldPosition(rotationTarget);
        Vector3 worldAxis = GetRotationWorldAxis(rotationTarget);

        Vector3 positionBeforeRotation = rotationTarget.position;

        rotationTarget.RotateAround(pivotWorldPosition, worldAxis, rotationAngle);

        NotifyDraggedModelMoved(rotationTarget, rotationTarget.position - positionBeforeRotation);
    }

    // =========================================================
    // ROTATION PIVOT
    // =========================================================

    // Exposes the hand attach point so other scripts can read the hand rotation.
    public Transform GetHandAttachPoint()
    {
        return handAttachPoint;
    }

    // Rotates any model using the current pivot settings.
    public void RotateModelAroundPivot(Transform model, float angleDegrees)
    {
        if (model == null) return;
        if (Mathf.Abs(angleDegrees) < 0.00001f) return;

        if (rotationPivotMode == RotationPivotMode.TransformPivot)
        {
            Space rotationSpace = useLocalRotation ? Space.Self : Space.World;
            model.Rotate(rotationAxis.normalized, angleDegrees, rotationSpace);
            return;
        }

        model.RotateAround(
            GetRotationPivotWorldPosition(model),
            GetRotationWorldAxis(model),
            angleDegrees
        );
    }

    // Tells a dragged model that something else moved it.
    private void NotifyDraggedModelMoved(Transform model, Vector3 positionDelta)
    {
        if (model == null) return;
        if (positionDelta.sqrMagnitude < 0.0000001f) return;

        VR3DClickable clickable = model.GetComponent<VR3DClickable>();

        if (clickable == null) return;
        if (!clickable.IsDragging()) return;

        clickable.ApplyDragOffset(positionDelta);
    }

    // Returns the world axis the model should rotate around.
    private Vector3 GetRotationWorldAxis(Transform model)
    {
        Vector3 axis = rotationAxis.normalized;

        if (axis.sqrMagnitude < 0.000001f) axis = Vector3.up;

        if (useLocalRotation && model != null)
        {
            axis = model.rotation * axis;
        }

        if (axis.sqrMagnitude < 0.000001f) return Vector3.up;

        return axis.normalized;
    }

    // Returns the world position the model should rotate around.
    private Vector3 GetRotationPivotWorldPosition(Transform model)
    {
        if (model == null) return Vector3.zero;

        switch (rotationPivotMode)
        {
            case RotationPivotMode.CustomTransform:
                if (customRotationPivot != null) return customRotationPivot.position;
                return model.position;

            case RotationPivotMode.CustomLocalOffset:
                return model.TransformPoint(customRotationPivotLocalOffset);

            case RotationPivotMode.RendererBoundsCenter:
                return model.TransformPoint(GetLocalBoundsCenter(model));

            default:
                return model.position;
        }
    }

    // Exposes the visual center of a model in world space.
    public Vector3 GetModelBoundsCenterWorld(Transform model)
    {
        if (model == null) return Vector3.zero;

        return model.TransformPoint(GetLocalBoundsCenter(model));
    }

    // Returns the cached local space center of a model, calculating it once.
    private Vector3 GetLocalBoundsCenter(Transform model)
    {
        if (model == null) return Vector3.zero;

        if (cachedLocalBoundsCenters.TryGetValue(model, out Vector3 cachedCenter))
        {
            return cachedCenter;
        }

        Vector3 center = CalculateLocalBoundsCenter(model);
        cachedLocalBoundsCenters[model] = center;

        return center;
    }

    // Calculates the visual center of all renderers in the model's local space.
    private Vector3 CalculateLocalBoundsCenter(Transform model)
    {
        if (model == null) return Vector3.zero;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(includeInactiveRenderers);

        if (renderers == null || renderers.Length == 0)
        {
            return Vector3.zero;
        }

        Matrix4x4 worldToLocal = model.worldToLocalMatrix;

        bool hasBounds = false;
        Bounds localBounds = new Bounds();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            Mesh mesh = GetStaticMeshFromRenderer(renderer);

            if (mesh != null)
            {
                Matrix4x4 meshToLocal = worldToLocal * renderer.transform.localToWorldMatrix;
                EncapsulateBoundsCorners(mesh.bounds, meshToLocal, ref localBounds, ref hasBounds);
            }
            else
            {
                EncapsulateBoundsCorners(renderer.bounds, worldToLocal, ref localBounds, ref hasBounds);
            }
        }

        if (!hasBounds) return Vector3.zero;

        return localBounds.center;
    }

    // Returns a usable mesh from a renderer, or null for skinned meshes.
    private Mesh GetStaticMeshFromRenderer(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer) return null;

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

        if (meshFilter == null) return null;

        return meshFilter.sharedMesh;
    }

    // Transforms the 8 corners of a bounds and encapsulates them into a target bounds.
    private void EncapsulateBoundsCorners(Bounds sourceBounds, Matrix4x4 matrix, ref Bounds targetBounds, ref bool hasBounds)
    {
        Vector3 boundsCenter = sourceBounds.center;
        Vector3 boundsExtents = sourceBounds.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                boundsCenter.x + ((i & 1) == 0 ? -boundsExtents.x : boundsExtents.x),
                boundsCenter.y + ((i & 2) == 0 ? -boundsExtents.y : boundsExtents.y),
                boundsCenter.z + ((i & 4) == 0 ? -boundsExtents.z : boundsExtents.z)
            );

            Vector3 transformedCorner = matrix.MultiplyPoint3x4(corner);

            if (!hasBounds)
            {
                targetBounds = new Bounds(transformedCorner, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                targetBounds.Encapsulate(transformedCorner);
            }
        }
    }

    // Applies a world scale while keeping the rotation pivot in place.
    private void ApplyWorldScaleKeepingPivot(Transform model, Vector3 desiredWorldScale)
    {
        if (model == null) return;

        if (!scaleAroundRotationPivot || rotationPivotMode == RotationPivotMode.TransformPivot)
        {
            SetWorldScale(model, desiredWorldScale);
            return;
        }

        Vector3 pivotBeforeScale = GetRotationPivotWorldPosition(model);

        SetWorldScale(model, desiredWorldScale);

        Vector3 pivotAfterScale = GetRotationPivotWorldPosition(model);

        Vector3 positionDelta = pivotBeforeScale - pivotAfterScale;

        model.position += positionDelta;

        NotifyDraggedModelMoved(model, positionDelta);
    }

    // Clears the cached centers so they are recalculated on the next frame.
    [ContextMenu("Recalculate Rotation Pivots")]
    public void ClearRotationPivotCache()
    {
        cachedLocalBoundsCenters.Clear();
    }

    // Draws the current rotation pivot in the Scene view.
    private void OnDrawGizmosSelected()
    {
        if (!showRotationPivotGizmo) return;
        if (!Application.isPlaying) return;

        Transform gizmoTarget = GetRotationTarget();
        if (gizmoTarget == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(GetRotationPivotWorldPosition(gizmoTarget), 0.03f);
    }

    // =========================================================
    // SCALE
    // =========================================================

    // Calculates and applies scaling bounded by absolute limits.
    private void HandleScale()
    {
        float direction = 0f;
        if (scaleUpPressCount > 0) direction += 1f;
        if (scaleDownPressCount > 0) direction -= 1f;

        float targetScaleSpeed = direction * scaleSpeed;
        float smoothTime = Mathf.Abs(targetScaleSpeed) > 0.001f ? scaleEaseInTime : scaleEaseOutTime;

        if (smoothTime <= 0f)
        {
            currentScaleSpeed = targetScaleSpeed;
            scaleSmoothVelocity = 0f;
        }
        else
        {
            currentScaleSpeed = Mathf.SmoothDamp(currentScaleSpeed, targetScaleSpeed, ref scaleSmoothVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
        }

        if (Mathf.Abs(currentScaleSpeed) < 0.001f && Mathf.Abs(targetScaleSpeed) < 0.001f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
            return;
        }

        currentScaleMultiplier += currentScaleSpeed * Time.deltaTime;

        Transform scaleTarget = GetScaleTarget();
        if (scaleTarget == null) return;

        Vector3 baseWorldScale = GetInitialWorldScaleForTransform(scaleTarget);
        float baseScaleValue = Mathf.Abs(baseWorldScale.x) > 0.0001f ? Mathf.Abs(baseWorldScale.x) : 1f;

        float minMult = absoluteMinimumScale / baseScaleValue;
        float maxMult = absoluteMaximumScale / baseScaleValue;

        currentScaleMultiplier = Mathf.Clamp(currentScaleMultiplier, minMult, maxMult);

        if (currentScaleMultiplier >= maxMult && currentScaleSpeed > 0f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
        }

        if (currentScaleMultiplier <= minMult && currentScaleSpeed < 0f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
        }

        Vector3 desiredWorldScale = baseWorldScale * currentScaleMultiplier;
        ApplyWorldScaleKeepingPivot(scaleTarget, desiredWorldScale);
    }

    // Returns the original world scale of a given transform.
    private Vector3 GetInitialWorldScaleForTransform(Transform model)
    {
        if (model == null) return Vector3.one;
        if (model == targetModel) return initialWorldScale;

        DraggableInitialState state = FindInitialState(model);
        if (state != null) return state.initialWorldScale;

        return model.lossyScale;
    }

    // =========================================================
    // RESET
    // =========================================================

    // Initiates the smooth reset animation sequence.
    public void SmoothResetModel()
    {
        if (targetModel == null) return;

        CancelHandAttach();

        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(SmoothResetRoutine());
    }

    // Coroutine that smoothly returns models to their initial transforms.
    private IEnumerator SmoothResetRoutine()
    {
        isResetting = true;
        StopAllActions();
        StopMovementImmediately();

        RestoreOriginalParentsKeepingWorldTransform();

        Vector3 startPosition = targetModel.localPosition;
        Quaternion startRotation = targetModel.localRotation;
        Vector3 startScale = targetModel.localScale;

        List<Vector3> draggableStartPositions = new List<Vector3>(draggableInitialStates.Count);
        List<Quaternion> draggableStartRotations = new List<Quaternion>(draggableInitialStates.Count);
        List<Vector3> draggableStartScales = new List<Vector3>(draggableInitialStates.Count);

        for (int i = 0; i < draggableInitialStates.Count; i++)
        {
            DraggableInitialState state = draggableInitialStates[i];
            if (state == null || state.transform == null)
            {
                draggableStartPositions.Add(Vector3.zero);
                draggableStartRotations.Add(Quaternion.identity);
                draggableStartScales.Add(Vector3.one);
                continue;
            }

            if (state.clickable != null) state.clickable.EndDrag();

            draggableStartPositions.Add(state.transform.localPosition);
            draggableStartRotations.Add(state.transform.localRotation);
            draggableStartScales.Add(state.transform.localScale);
        }

        if (resetDuration <= 0f)
        {
            ApplyInitialTransform();
            ApplyInitialDraggableTransforms();

            selectedModel = null;
            isResetting = false;
            resetCoroutine = null;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < resetDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / resetDuration);
            float easedTime = resetEaseCurve.Evaluate(normalizedTime);

            targetModel.localPosition = Vector3.LerpUnclamped(startPosition, initialLocalPosition, easedTime);
            targetModel.localRotation = Quaternion.SlerpUnclamped(startRotation, initialLocalRotation, easedTime);
            targetModel.localScale = Vector3.LerpUnclamped(startScale, initialLocalScale, easedTime);

            for (int i = 0; i < draggableInitialStates.Count; i++)
            {
                DraggableInitialState state = draggableInitialStates[i];
                if (state == null || state.transform == null) continue;
                if (state.transform == targetModel) continue;

                state.transform.localPosition = Vector3.LerpUnclamped(draggableStartPositions[i], state.initialLocalPosition, easedTime);
                state.transform.localRotation = Quaternion.SlerpUnclamped(draggableStartRotations[i], state.initialLocalRotation, easedTime);
                state.transform.localScale = Vector3.LerpUnclamped(draggableStartScales[i], state.initialLocalScale, easedTime);
            }

            yield return null;
        }

        ApplyInitialTransform();
        ApplyInitialDraggableTransforms();

        selectedModel = null;
        isResetting = false;
        resetCoroutine = null;
    }

    // Reparents draggables without changing their visual world transform.
    private void RestoreOriginalParentsKeepingWorldTransform()
    {
        for (int i = 0; i < draggableInitialStates.Count; i++)
        {
            DraggableInitialState state = draggableInitialStates[i];
            if (state == null || state.transform == null) continue;

            if (state.clickable != null) state.clickable.EndDrag();

            if (state.transform.parent != state.initialParent)
            {
                state.transform.SetParent(state.initialParent, true);
            }

            RestoreModelColliders(state.transform);
        }
    }

    // Applies the stored original local transform values directly to the target.
    private void ApplyInitialTransform()
    {
        targetModel.localPosition = initialLocalPosition;
        targetModel.localRotation = initialLocalRotation;
        targetModel.localScale = initialLocalScale;
        currentScaleMultiplier = 1f;
    }

    // Applies the stored original local transform values to all draggables.
    private void ApplyInitialDraggableTransforms()
    {
        for (int i = 0; i < draggableInitialStates.Count; i++)
        {
            DraggableInitialState state = draggableInitialStates[i];
            if (state == null || state.transform == null) continue;

            if (state.clickable != null) state.clickable.EndDrag();

            if (state.transform.parent != state.initialParent)
            {
                state.transform.SetParent(state.initialParent, false);
            }

            RestoreModelColliders(state.transform);

            if (state.transform == targetModel) continue;

            state.transform.localPosition = state.initialLocalPosition;
            state.transform.localRotation = state.initialLocalRotation;
            state.transform.localScale = state.initialLocalScale;
        }
    }

    // Stops the reset animation and calculates the new scale multiplier.
    private void CancelReset()
    {
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }

        isResetting = false;
        CalculateCurrentScaleMultiplier();
    }

    // =========================================================
    // SCALE MULTIPLIER
    // =========================================================

    // Recalculates the scale multiplier using the current active target.
    private void CalculateCurrentScaleMultiplier()
    {
        CalculateCurrentScaleMultiplierForTarget(GetScaleTarget());
    }

    // Recalculates and clamps the current scale multiplier based on absolute limits.
    private void CalculateCurrentScaleMultiplierForTarget(Transform model)
    {
        if (model == null)
        {
            currentScaleMultiplier = 1f;
            return;
        }

        Vector3 baseWorldScale = GetInitialWorldScaleForTransform(model);
        float initialMagnitude = baseWorldScale.magnitude;

        if (initialMagnitude <= 0.0001f)
        {
            currentScaleMultiplier = 1f;
            return;
        }

        currentScaleMultiplier = model.lossyScale.magnitude / initialMagnitude;

        float baseScaleValue = Mathf.Abs(baseWorldScale.x) > 0.0001f ? Mathf.Abs(baseWorldScale.x) : 1f;
        float minMult = absoluteMinimumScale / baseScaleValue;
        float maxMult = absoluteMaximumScale / baseScaleValue;

        currentScaleMultiplier = Mathf.Clamp(currentScaleMultiplier, minMult, maxMult);
    }

    // =========================================================
    // SAVE INITIAL TRANSFORMS
    // =========================================================

    // Saves the initial position, rotation, and scale of the target model.
    public void SaveInitialTransform()
    {
        if (targetModel == null) return;

        initialLocalPosition = targetModel.localPosition;
        initialLocalRotation = targetModel.localRotation;
        initialLocalScale = targetModel.localScale;
        initialWorldScale = targetModel.lossyScale;

        currentScaleMultiplier = 1f;
    }

    // Saves the initial transform data for all configured draggables.
    private void SaveInitialDraggableTransforms()
    {
        draggableInitialStates.Clear();
        HashSet<VR3DClickable> uniqueClickables = new HashSet<VR3DClickable>();

        if (autoFindDraggableModels && targetModel != null)
        {
            VR3DClickable[] found = targetModel.GetComponentsInChildren<VR3DClickable>(true);
            foreach (VR3DClickable clickable in found)
            {
                if (clickable != null) uniqueClickables.Add(clickable);
            }
        }

        if (extraDraggableModels != null)
        {
            foreach (VR3DClickable clickable in extraDraggableModels)
            {
                if (clickable != null) uniqueClickables.Add(clickable);
            }
        }

        foreach (VR3DClickable clickable in uniqueClickables)
        {
            Transform draggableTransform = clickable.transform;
            draggableInitialStates.Add(new DraggableInitialState
            {
                clickable = clickable,
                transform = draggableTransform,
                initialParent = draggableTransform.parent,
                initialLocalPosition = draggableTransform.localPosition,
                initialLocalRotation = draggableTransform.localRotation,
                initialLocalScale = draggableTransform.localScale,
                initialWorldScale = draggableTransform.lossyScale
            });
        }
    }

    // Locates the saved initial state for a specified model.
    private DraggableInitialState FindInitialState(Transform model)
    {
        if (model == null) return null;

        for (int i = 0; i < draggableInitialStates.Count; i++)
        {
            DraggableInitialState state = draggableInitialStates[i];
            if (state != null && state.transform == model) return state;
        }

        return null;
    }

    // Exposes resaving functionality to the editor context menu.
    [ContextMenu("Resave Drag Initial Transforms")]
    public void ResaveDragInitialTransforms()
    {
        SaveInitialDraggableTransforms();
    }

    // =========================================================
    // COLLIDERS WHILE MODEL IS ATTACHED
    // =========================================================

    // Disables and caches the states of colliders on a model.
    private void DisableModelColliders(Transform model)
    {
        if (model == null) return;
        if (savedColliderStates.ContainsKey(model)) return;

        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        List<ColliderState> states = new List<ColliderState>();

        foreach (Collider col in colliders)
        {
            if (col == null) continue;

            states.Add(new ColliderState
            {
                collider = col,
                wasEnabled = col.enabled
            });

            col.enabled = false;
        }

        savedColliderStates.Add(model, states);
    }

    // Restores cached collider states to a model.
    private void RestoreModelColliders(Transform model)
    {
        if (model == null) return;

        if (!savedColliderStates.TryGetValue(model, out List<ColliderState> states))
        {
            return;
        }

        foreach (ColliderState state in states)
        {
            if (state == null || state.collider == null) continue;
            state.collider.enabled = state.wasEnabled;
        }

        savedColliderStates.Remove(model);
    }

    // Restores colliders for all models that were previously disabled.
    private void RestoreAllSavedColliders()
    {
        List<Transform> models = new List<Transform>(savedColliderStates.Keys);
        foreach (Transform model in models)
        {
            RestoreModelColliders(model);
        }
    }

    // Interrupts the hand attach animation if currently active.
    private void CancelHandAttach()
    {
        if (handAttachCoroutine != null)
        {
            StopCoroutine(handAttachCoroutine);
            handAttachCoroutine = null;
        }

        isAttachingToHand = false;
    }

    // =========================================================
    // STOP / CLEANUP
    // =========================================================

    // Resets press counts for all inputs.
    private void StopAllActions()
    {
        rotateRightPressCount = 0;
        rotateLeftPressCount = 0;
        scaleUpPressCount = 0;
        scaleDownPressCount = 0;
    }

    // Instantly halts scaling and rotation velocities.
    private void StopMovementImmediately()
    {
        currentRotationSpeed = 0f;
        rotationSmoothVelocity = 0f;

        currentScaleSpeed = 0f;
        scaleSmoothVelocity = 0f;
    }

    // Cleans up states and coroutines when the script is disabled.
    private void OnDisable()
    {
        StopAllActions();
        StopMovementImmediately();
        CancelHandAttach();
        RestoreAllSavedColliders();

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }

        isResetting = false;
    }

    // Removes UI events when the script is destroyed.
    private void OnDestroy()
    {
        RemoveCreatedTriggerEntries();
    }

    // Clears dynamic event triggers from registered bindings.
    private void RemoveCreatedTriggerEntries()
    {
        foreach (TriggerBinding binding in triggerBindings)
        {
            if (binding.eventTrigger == null || binding.eventTrigger.triggers == null) continue;

            foreach (EventTrigger.Entry entry in binding.entries)
            {
                binding.eventTrigger.triggers.Remove(entry);
            }
        }

        triggerBindings.Clear();
    }
}