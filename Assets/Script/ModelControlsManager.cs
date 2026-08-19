using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ModelControlsManager : MonoBehaviour
{
    public enum ControlAction
    {
        RotateRight,
        RotateLeft,
        ScaleUp,
        ScaleDown,
        AttachToHand,
        ReleaseFromHand,
        Reset
    }

    [Serializable]
    public class ButtonControlGroup
    {
        public ControlAction controlAction;
        public List<Button> buttons = new List<Button>();
    }

    private class TriggerBinding
    {
        public EventTrigger eventTrigger;

        public List<EventTrigger.Entry> entries =
            new List<EventTrigger.Entry>();
    }

    private class DraggableInitialState
    {
        public VR3DClickable clickable;
        public Transform transform;

        public Transform initialParent;

        public Vector3 initialLocalPosition;
        public Quaternion initialLocalRotation;
        public Vector3 initialLocalScale;
        public Vector3 initialWorldScale;
    }

    private class ColliderState
    {
        public Collider collider;
        public bool wasEnabled;
    }

    [Header("Target Model")]
    [SerializeField] private Transform targetModel;

    [Header("Selected Model")]
    [Tooltip(
        "The last model dragged by VR3DClickable. " +
        "Rotate / Scale / Attach To Hand will use this model."
    )]
    [SerializeField] private Transform selectedModel;

    [Header("Hand Attach")]
    [Tooltip(
        "Create an Empty GameObject under the hand/controller " +
        "and place it where you want the selected model to appear."
    )]
    [SerializeField] private Transform handAttachPoint;

    [Tooltip("Position of the model relative to Hand Attach Point.")]
    [SerializeField] private Vector3 handLocalPosition = Vector3.zero;

    [Tooltip("Rotation of the model relative to Hand Attach Point.")]
    [SerializeField] private Vector3 handLocalEulerAngles = Vector3.zero;

    [Header("Smooth Hand Attach")]
    [Tooltip("How long the selected model takes to move into the hand.")]
    [SerializeField] private float handAttachDuration = 0.5f;

    [SerializeField]
    private AnimationCurve handAttachEaseCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Blocks Rotate / Scale while the model is moving into the hand.")]
    [SerializeField] private bool blockControlsDuringHandAttach = true;

    [Header("Button Control Groups")]
    [SerializeField]
    private List<ButtonControlGroup> buttonControlGroups =
        new List<ButtonControlGroup>();

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private bool invertRotationDirection = false;

    [Header("Rotation Ease Settings")]
    [SerializeField] private float rotationEaseInTime = 0.2f;
    [SerializeField] private float rotationEaseOutTime = 0.4f;

    [Header("Scale Settings")]
    [SerializeField] private float scaleSpeed = 1f;
    [SerializeField] private float minimumScaleMultiplier = 0.5f;
    [SerializeField] private float maximumScaleMultiplier = 2f;

    [Header("Scale Ease Settings")]
    [SerializeField] private float scaleEaseInTime = 0.2f;
    [SerializeField] private float scaleEaseOutTime = 0.4f;

    [Header("Smooth Reset Settings")]
    [SerializeField] private float resetDuration = 0.8f;

    [SerializeField]
    private AnimationCurve resetEaseCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField]
    private bool blockControlsDuringReset = true;

    [Header("Drag Reset")]
    [Tooltip("Automatically finds VR3DClickable components under Target Model.")]
    [SerializeField] private bool autoFindDraggableModels = true;

    [Tooltip("Optional draggable objects outside Target Model.")]
    [SerializeField]
    private List<VR3DClickable> extraDraggableModels =
        new List<VR3DClickable>();

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Vector3 initialWorldScale;

    private float currentScaleMultiplier = 1f;

    private int rotateRightPressCount;
    private int rotateLeftPressCount;
    private int scaleUpPressCount;
    private int scaleDownPressCount;

    private float currentRotationSpeed;
    private float rotationSmoothVelocity;

    private float currentScaleSpeed;
    private float scaleSmoothVelocity;

    private Coroutine resetCoroutine;
    private bool isResetting;

    private Coroutine handAttachCoroutine;
    private bool isAttachingToHand;

    private readonly List<TriggerBinding> triggerBindings =
        new List<TriggerBinding>();

    private readonly List<DraggableInitialState> draggableInitialStates =
        new List<DraggableInitialState>();

    private readonly Dictionary<Transform, List<ColliderState>> savedColliderStates =
        new Dictionary<Transform, List<ColliderState>>();

    private void Awake()
    {
        if (targetModel == null)
        {
            Debug.LogError(
                "Target Model is not assigned.",
                gameObject
            );

            enabled = false;
            return;
        }

        SaveInitialTransform();
        SaveInitialDraggableTransforms();
        SetupButtons();
    }

    private void Update()
    {
        if (isResetting && blockControlsDuringReset)
        {
            return;
        }

        if (isAttachingToHand && blockControlsDuringHandAttach)
        {
            return;
        }

        HandleRotation();
        HandleScale();
    }

    // =========================================================
    // SELECTED MODEL
    // =========================================================

    public void SetSelectedModel(Transform model)
    {
        if (model == null)
            return;

        selectedModel = model;

        CalculateCurrentScaleMultiplierForTarget(
            GetScaleTarget()
        );

        Debug.Log(
            "Selected Model: " + selectedModel.name,
            selectedModel.gameObject
        );
    }

    public void ClearSelectedModel()
    {
        selectedModel = null;

        CalculateCurrentScaleMultiplierForTarget(
            targetModel
        );
    }

    public Transform GetSelectedModel()
    {
        return selectedModel;
    }

    private Transform GetRotationTarget()
    {
        if (selectedModel != null)
            return selectedModel;

        return targetModel;
    }

    private Transform GetScaleTarget()
    {
        if (selectedModel != null)
            return selectedModel;

        return targetModel;
    }

    // =========================================================
    // HAND ATTACH
    // =========================================================

    public void AttachSelectedModelToHand()
    {
        if (selectedModel == null)
        {
            Debug.LogWarning(
                "No Selected Model. Drag a model first.",
                gameObject
            );
            return;
        }

        if (handAttachPoint == null)
        {
            Debug.LogWarning(
                "Hand Attach Point is not assigned.",
                gameObject
            );
            return;
        }

        if (handAttachCoroutine != null)
        {
            StopCoroutine(handAttachCoroutine);
            handAttachCoroutine = null;
        }

        handAttachCoroutine =
            StartCoroutine(
                SmoothAttachSelectedModelToHandRoutine()
            );
    }

    private IEnumerator SmoothAttachSelectedModelToHandRoutine()
    {
        if (selectedModel == null ||
            handAttachPoint == null)
        {
            handAttachCoroutine = null;
            yield break;
        }

        isAttachingToHand = true;

        StopAllActions();
        StopMovementImmediately();

        Transform model = selectedModel;

        VR3DClickable clickable =
            model.GetComponent<VR3DClickable>();

        if (clickable != null)
        {
            clickable.EndDrag();
        }

        DisableModelColliders(model);

        Vector3 startWorldPosition =
            model.position;

        Quaternion startWorldRotation =
            model.rotation;

        Vector3 startWorldScale =
            model.lossyScale;

        if (handAttachDuration <= 0f)
        {
            FinishAttachToHand(
                model,
                startWorldScale
            );

            isAttachingToHand = false;
            handAttachCoroutine = null;

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < handAttachDuration)
        {
            // If selection changed while attaching, cancel this move.
            if (model == null ||
                selectedModel != model ||
                handAttachPoint == null)
            {
                isAttachingToHand = false;
                handAttachCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / handAttachDuration
                );

            float easedTime =
                handAttachEaseCurve.Evaluate(
                    normalizedTime
                );

            // Recalculate every frame so the target follows the moving hand.
            Vector3 targetWorldPosition =
                handAttachPoint.TransformPoint(
                    handLocalPosition
                );

            Quaternion targetWorldRotation =
                handAttachPoint.rotation *
                Quaternion.Euler(
                    handLocalEulerAngles
                );

            model.position =
                Vector3.LerpUnclamped(
                    startWorldPosition,
                    targetWorldPosition,
                    easedTime
                );

            model.rotation =
                Quaternion.SlerpUnclamped(
                    startWorldRotation,
                    targetWorldRotation,
                    easedTime
                );

            // Preserve its visible world scale while moving.
            SetWorldScale(
                model,
                startWorldScale
            );

            yield return null;
        }

        if (model != null &&
            selectedModel == model &&
            handAttachPoint != null)
        {
            FinishAttachToHand(
                model,
                startWorldScale
            );
        }

        isAttachingToHand = false;
        handAttachCoroutine = null;
    }

    private void FinishAttachToHand(
        Transform model,
        Vector3 preservedWorldScale
    )
    {
        if (model == null ||
            handAttachPoint == null)
        {
            return;
        }

        model.SetParent(
            handAttachPoint,
            true
        );

        model.localPosition =
            handLocalPosition;

        model.localRotation =
            Quaternion.Euler(
                handLocalEulerAngles
            );

        SetWorldScale(
            model,
            preservedWorldScale
        );

        // Recalculate after parenting so the next Scale press
        // starts exactly from the current visible size.
        CalculateCurrentScaleMultiplierForTarget(
            model
        );

        Debug.Log(
            "Smoothly Attached To Hand: " +
            model.name,
            model.gameObject
        );
    }

    private void SetWorldScale(
        Transform model,
        Vector3 desiredWorldScale
    )
    {
        if (model == null)
            return;

        Transform parent =
            model.parent;

        if (parent == null)
        {
            model.localScale =
                desiredWorldScale;
            return;
        }

        Vector3 parentScale =
            parent.lossyScale;

        model.localScale =
            new Vector3(
                SafeDivide(
                    desiredWorldScale.x,
                    parentScale.x
                ),
                SafeDivide(
                    desiredWorldScale.y,
                    parentScale.y
                ),
                SafeDivide(
                    desiredWorldScale.z,
                    parentScale.z
                )
            );
    }

    private float SafeDivide(
        float value,
        float divisor
    )
    {
        if (Mathf.Abs(divisor) < 0.00001f)
        {
            return value;
        }

        return value / divisor;
    }

    public void ReleaseSelectedModelFromHand()
    {
        CancelHandAttach();

        if (selectedModel == null)
        {
            Debug.LogWarning(
                "No Selected Model.",
                gameObject
            );
            return;
        }

        DraggableInitialState state =
            FindInitialState(selectedModel);

        if (state == null)
        {
            Debug.LogWarning(
                "Initial state was not found for: " +
                selectedModel.name,
                selectedModel.gameObject
            );
            return;
        }

        // Return to original parent but keep current world position/rotation/scale.
        selectedModel.SetParent(
            state.initialParent,
            true
        );

        RestoreModelColliders(selectedModel);

        CalculateCurrentScaleMultiplierForTarget(
            selectedModel
        );

        Debug.Log(
            "Released From Hand: " + selectedModel.name,
            selectedModel.gameObject
        );
    }

    private bool IsAttachedToHand(Transform model)
    {
        if (model == null ||
            handAttachPoint == null)
        {
            return false;
        }

        return model.parent == handAttachPoint;
    }

    // =========================================================
    // BUTTON SETUP
    // =========================================================

    private void SetupButtons()
    {
        RemoveCreatedTriggerEntries();

        foreach (ButtonControlGroup controlGroup in buttonControlGroups)
        {
            if (controlGroup == null ||
                controlGroup.buttons == null)
            {
                continue;
            }

            ControlAction selectedAction =
                controlGroup.controlAction;

            foreach (Button button in controlGroup.buttons)
            {
                if (button == null)
                    continue;

                SetupButton(
                    button,
                    selectedAction
                );
            }
        }
    }

    private void SetupButton(
        Button button,
        ControlAction action
    )
    {
        EventTrigger eventTrigger =
            button.GetComponent<EventTrigger>();

        if (eventTrigger == null)
        {
            eventTrigger =
                button.gameObject.AddComponent<EventTrigger>();
        }

        if (eventTrigger.triggers == null)
        {
            eventTrigger.triggers =
                new List<EventTrigger.Entry>();
        }

        TriggerBinding binding =
            new TriggerBinding
            {
                eventTrigger = eventTrigger
            };

        AddTriggerEntry(
            binding,
            EventTriggerType.PointerDown,
            delegate
            {
                StartAction(action);
            }
        );

        AddTriggerEntry(
            binding,
            EventTriggerType.PointerUp,
            delegate
            {
                StopAction(action);
            }
        );

        AddTriggerEntry(
            binding,
            EventTriggerType.PointerExit,
            delegate
            {
                StopAction(action);
            }
        );

        AddTriggerEntry(
            binding,
            EventTriggerType.Cancel,
            delegate
            {
                StopAction(action);
            }
        );

        triggerBindings.Add(binding);
    }

    private void AddTriggerEntry(
        TriggerBinding binding,
        EventTriggerType eventType,
        Action<BaseEventData> callback
    )
    {
        EventTrigger.Entry entry =
            new EventTrigger.Entry();

        entry.eventID = eventType;

        entry.callback.AddListener(
            delegate(BaseEventData eventData)
            {
                callback(eventData);
            }
        );

        binding.eventTrigger.triggers.Add(entry);
        binding.entries.Add(entry);
    }

    // =========================================================
    // ACTIONS
    // =========================================================

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
            if (blockControlsDuringReset)
                return;

            CancelReset();
        }

        switch (action)
        {
            case ControlAction.RotateRight:
                rotateRightPressCount++;
                break;

            case ControlAction.RotateLeft:
                rotateLeftPressCount++;
                break;

            case ControlAction.ScaleUp:
                scaleUpPressCount++;
                break;

            case ControlAction.ScaleDown:
                scaleDownPressCount++;
                break;
        }
    }

    private void StopAction(ControlAction action)
    {
        switch (action)
        {
            case ControlAction.RotateRight:
                rotateRightPressCount =
                    Mathf.Max(0, rotateRightPressCount - 1);
                break;

            case ControlAction.RotateLeft:
                rotateLeftPressCount =
                    Mathf.Max(0, rotateLeftPressCount - 1);
                break;

            case ControlAction.ScaleUp:
                scaleUpPressCount =
                    Mathf.Max(0, scaleUpPressCount - 1);
                break;

            case ControlAction.ScaleDown:
                scaleDownPressCount =
                    Mathf.Max(0, scaleDownPressCount - 1);
                break;
        }
    }

    // =========================================================
    // ROTATION
    // =========================================================

    private void HandleRotation()
    {
        float direction = 0f;

        if (rotateRightPressCount > 0)
            direction -= 1f;

        if (rotateLeftPressCount > 0)
            direction += 1f;

        if (invertRotationDirection)
            direction *= -1f;

        float targetRotationSpeed =
            direction * rotationSpeed;

        float smoothTime =
            Mathf.Abs(targetRotationSpeed) > 0.001f
                ? rotationEaseInTime
                : rotationEaseOutTime;

        if (smoothTime <= 0f)
        {
            currentRotationSpeed =
                targetRotationSpeed;

            rotationSmoothVelocity = 0f;
        }
        else
        {
            currentRotationSpeed =
                Mathf.SmoothDamp(
                    currentRotationSpeed,
                    targetRotationSpeed,
                    ref rotationSmoothVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
        }

        if (Mathf.Abs(currentRotationSpeed) < 0.01f &&
            Mathf.Abs(targetRotationSpeed) < 0.01f)
        {
            currentRotationSpeed = 0f;
            rotationSmoothVelocity = 0f;
            return;
        }

        Transform rotationTarget =
            GetRotationTarget();

        if (rotationTarget == null)
            return;

        Space rotationSpace =
            useLocalRotation
                ? Space.Self
                : Space.World;

        rotationTarget.Rotate(
            rotationAxis.normalized,
            currentRotationSpeed * Time.deltaTime,
            rotationSpace
        );
    }

    // =========================================================
    // SCALE
    // =========================================================

    private void HandleScale()
    {
        float direction = 0f;

        if (scaleUpPressCount > 0)
            direction += 1f;

        if (scaleDownPressCount > 0)
            direction -= 1f;

        float targetScaleSpeed =
            direction * scaleSpeed;

        float smoothTime =
            Mathf.Abs(targetScaleSpeed) > 0.001f
                ? scaleEaseInTime
                : scaleEaseOutTime;

        if (smoothTime <= 0f)
        {
            currentScaleSpeed =
                targetScaleSpeed;

            scaleSmoothVelocity = 0f;
        }
        else
        {
            currentScaleSpeed =
                Mathf.SmoothDamp(
                    currentScaleSpeed,
                    targetScaleSpeed,
                    ref scaleSmoothVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
        }

        if (Mathf.Abs(currentScaleSpeed) < 0.001f &&
            Mathf.Abs(targetScaleSpeed) < 0.001f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
            return;
        }

        currentScaleMultiplier +=
            currentScaleSpeed * Time.deltaTime;

        currentScaleMultiplier =
            Mathf.Clamp(
                currentScaleMultiplier,
                minimumScaleMultiplier,
                maximumScaleMultiplier
            );

        if (currentScaleMultiplier >=
                maximumScaleMultiplier &&
            currentScaleSpeed > 0f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
        }

        if (currentScaleMultiplier <=
                minimumScaleMultiplier &&
            currentScaleSpeed < 0f)
        {
            currentScaleSpeed = 0f;
            scaleSmoothVelocity = 0f;
        }

        Transform scaleTarget =
            GetScaleTarget();

        if (scaleTarget == null)
            return;

        Vector3 baseWorldScale =
            GetInitialWorldScaleForTransform(
                scaleTarget
            );

        Vector3 desiredWorldScale =
            baseWorldScale * currentScaleMultiplier;

        SetWorldScale(
            scaleTarget,
            desiredWorldScale
        );
    }

    private Vector3 GetInitialWorldScaleForTransform(
        Transform model
    )
    {
        if (model == null)
            return Vector3.one;

        if (model == targetModel)
            return initialWorldScale;

        DraggableInitialState state =
            FindInitialState(model);

        if (state != null)
            return state.initialWorldScale;

        return model.lossyScale;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void SmoothResetModel()
    {
        if (targetModel == null)
            return;

        CancelHandAttach();

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
        }

        resetCoroutine =
            StartCoroutine(
                SmoothResetRoutine()
            );
    }

    private IEnumerator SmoothResetRoutine()
    {
        isResetting = true;

        StopAllActions();
        StopMovementImmediately();

        // IMPORTANT:
        // Restore all draggable objects to their original parents FIRST,
        // while keeping their current world transforms.
        RestoreOriginalParentsKeepingWorldTransform();

        Vector3 startPosition =
            targetModel.localPosition;

        Quaternion startRotation =
            targetModel.localRotation;

        Vector3 startScale =
            targetModel.localScale;

        List<Vector3> draggableStartPositions =
            new List<Vector3>(
                draggableInitialStates.Count
            );

        List<Quaternion> draggableStartRotations =
            new List<Quaternion>(
                draggableInitialStates.Count
            );

        List<Vector3> draggableStartScales =
            new List<Vector3>(
                draggableInitialStates.Count
            );

        for (int i = 0;
             i < draggableInitialStates.Count;
             i++)
        {
            DraggableInitialState state =
                draggableInitialStates[i];

            if (state == null ||
                state.transform == null)
            {
                draggableStartPositions.Add(Vector3.zero);
                draggableStartRotations.Add(Quaternion.identity);
                draggableStartScales.Add(Vector3.one);
                continue;
            }

            if (state.clickable != null)
            {
                state.clickable.EndDrag();
            }

            draggableStartPositions.Add(
                state.transform.localPosition
            );

            draggableStartRotations.Add(
                state.transform.localRotation
            );

            draggableStartScales.Add(
                state.transform.localScale
            );
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

            float normalizedTime =
                Mathf.Clamp01(
                    elapsedTime / resetDuration
                );

            float easedTime =
                resetEaseCurve.Evaluate(
                    normalizedTime
                );

            targetModel.localPosition =
                Vector3.LerpUnclamped(
                    startPosition,
                    initialLocalPosition,
                    easedTime
                );

            targetModel.localRotation =
                Quaternion.SlerpUnclamped(
                    startRotation,
                    initialLocalRotation,
                    easedTime
                );

            targetModel.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    initialLocalScale,
                    easedTime
                );

            for (int i = 0;
                 i < draggableInitialStates.Count;
                 i++)
            {
                DraggableInitialState state =
                    draggableInitialStates[i];

                if (state == null ||
                    state.transform == null)
                {
                    continue;
                }

                if (state.transform == targetModel)
                    continue;

                state.transform.localPosition =
                    Vector3.LerpUnclamped(
                        draggableStartPositions[i],
                        state.initialLocalPosition,
                        easedTime
                    );

                state.transform.localRotation =
                    Quaternion.SlerpUnclamped(
                        draggableStartRotations[i],
                        state.initialLocalRotation,
                        easedTime
                    );

                state.transform.localScale =
                    Vector3.LerpUnclamped(
                        draggableStartScales[i],
                        state.initialLocalScale,
                        easedTime
                    );
            }

            yield return null;
        }

        ApplyInitialTransform();
        ApplyInitialDraggableTransforms();

        selectedModel = null;

        isResetting = false;
        resetCoroutine = null;
    }

    private void RestoreOriginalParentsKeepingWorldTransform()
    {
        for (int i = 0;
             i < draggableInitialStates.Count;
             i++)
        {
            DraggableInitialState state =
                draggableInitialStates[i];

            if (state == null ||
                state.transform == null)
            {
                continue;
            }

            if (state.clickable != null)
            {
                state.clickable.EndDrag();
            }

            if (state.transform.parent !=
                state.initialParent)
            {
                state.transform.SetParent(
                    state.initialParent,
                    true
                );
            }

            RestoreModelColliders(state.transform);
        }
    }

    private void ApplyInitialTransform()
    {
        targetModel.localPosition =
            initialLocalPosition;

        targetModel.localRotation =
            initialLocalRotation;

        targetModel.localScale =
            initialLocalScale;

        currentScaleMultiplier = 1f;
    }

    private void ApplyInitialDraggableTransforms()
    {
        for (int i = 0;
             i < draggableInitialStates.Count;
             i++)
        {
            DraggableInitialState state =
                draggableInitialStates[i];

            if (state == null ||
                state.transform == null)
            {
                continue;
            }

            if (state.clickable != null)
            {
                state.clickable.EndDrag();
            }

            if (state.transform.parent !=
                state.initialParent)
            {
                state.transform.SetParent(
                    state.initialParent,
                    false
                );
            }

            RestoreModelColliders(state.transform);

            if (state.transform == targetModel)
                continue;

            state.transform.localPosition =
                state.initialLocalPosition;

            state.transform.localRotation =
                state.initialLocalRotation;

            state.transform.localScale =
                state.initialLocalScale;
        }
    }

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

    private void CalculateCurrentScaleMultiplier()
    {
        CalculateCurrentScaleMultiplierForTarget(
            GetScaleTarget()
        );
    }

    private void CalculateCurrentScaleMultiplierForTarget(
        Transform model
    )
    {
        if (model == null)
        {
            currentScaleMultiplier = 1f;
            return;
        }

        Vector3 baseWorldScale =
            GetInitialWorldScaleForTransform(model);

        float initialMagnitude =
            baseWorldScale.magnitude;

        if (initialMagnitude <= 0.0001f)
        {
            currentScaleMultiplier = 1f;
            return;
        }

        currentScaleMultiplier =
            model.lossyScale.magnitude /
            initialMagnitude;

        currentScaleMultiplier =
            Mathf.Clamp(
                currentScaleMultiplier,
                minimumScaleMultiplier,
                maximumScaleMultiplier
            );
    }

    // =========================================================
    // SAVE INITIAL TRANSFORMS
    // =========================================================

    public void SaveInitialTransform()
    {
        if (targetModel == null)
            return;

        initialLocalPosition =
            targetModel.localPosition;

        initialLocalRotation =
            targetModel.localRotation;

        initialLocalScale =
            targetModel.localScale;

        initialWorldScale =
            targetModel.lossyScale;

        currentScaleMultiplier = 1f;
    }

    private void SaveInitialDraggableTransforms()
    {
        draggableInitialStates.Clear();

        HashSet<VR3DClickable> uniqueClickables =
            new HashSet<VR3DClickable>();

        if (autoFindDraggableModels &&
            targetModel != null)
        {
            VR3DClickable[] found =
                targetModel.GetComponentsInChildren<VR3DClickable>(true);

            foreach (VR3DClickable clickable in found)
            {
                if (clickable != null)
                {
                    uniqueClickables.Add(
                        clickable
                    );
                }
            }
        }

        if (extraDraggableModels != null)
        {
            foreach (VR3DClickable clickable
                     in extraDraggableModels)
            {
                if (clickable != null)
                {
                    uniqueClickables.Add(
                        clickable
                    );
                }
            }
        }

        foreach (VR3DClickable clickable
                 in uniqueClickables)
        {
            Transform draggableTransform =
                clickable.transform;

            draggableInitialStates.Add(
                new DraggableInitialState
                {
                    clickable = clickable,

                    transform =
                        draggableTransform,

                    initialParent =
                        draggableTransform.parent,

                    initialLocalPosition =
                        draggableTransform.localPosition,

                    initialLocalRotation =
                        draggableTransform.localRotation,

                    initialLocalScale =
                        draggableTransform.localScale,

                    initialWorldScale =
                        draggableTransform.lossyScale
                }
            );
        }
    }

    private DraggableInitialState FindInitialState(
        Transform model
    )
    {
        if (model == null)
            return null;

        for (int i = 0;
             i < draggableInitialStates.Count;
             i++)
        {
            DraggableInitialState state =
                draggableInitialStates[i];

            if (state != null &&
                state.transform == model)
            {
                return state;
            }
        }

        return null;
    }

    [ContextMenu("Resave Drag Initial Transforms")]
    public void ResaveDragInitialTransforms()
    {
        SaveInitialDraggableTransforms();
    }


    // =========================================================
    // COLLIDERS WHILE MODEL IS ATTACHED
    // =========================================================

    private void DisableModelColliders(Transform model)
    {
        if (model == null)
            return;

        if (savedColliderStates.ContainsKey(model))
            return;

        Collider[] colliders =
            model.GetComponentsInChildren<Collider>(true);

        List<ColliderState> states =
            new List<ColliderState>();

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            states.Add(
                new ColliderState
                {
                    collider = col,
                    wasEnabled = col.enabled
                }
            );

            col.enabled = false;
        }

        savedColliderStates.Add(model, states);
    }

    private void RestoreModelColliders(Transform model)
    {
        if (model == null)
            return;

        if (!savedColliderStates.TryGetValue(
                model,
                out List<ColliderState> states))
        {
            return;
        }

        foreach (ColliderState state in states)
        {
            if (state == null ||
                state.collider == null)
            {
                continue;
            }

            state.collider.enabled =
                state.wasEnabled;
        }

        savedColliderStates.Remove(model);
    }

    private void RestoreAllSavedColliders()
    {
        List<Transform> models =
            new List<Transform>(
                savedColliderStates.Keys
            );

        foreach (Transform model in models)
        {
            RestoreModelColliders(model);
        }
    }

    private void CancelHandAttach()
    {
        if (handAttachCoroutine != null)
        {
            StopCoroutine(
                handAttachCoroutine
            );

            handAttachCoroutine = null;
        }

        isAttachingToHand = false;
    }

    // =========================================================
    // STOP / CLEANUP
    // =========================================================

    private void StopAllActions()
    {
        rotateRightPressCount = 0;
        rotateLeftPressCount = 0;
        scaleUpPressCount = 0;
        scaleDownPressCount = 0;
    }

    private void StopMovementImmediately()
    {
        currentRotationSpeed = 0f;
        rotationSmoothVelocity = 0f;

        currentScaleSpeed = 0f;
        scaleSmoothVelocity = 0f;
    }

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

    private void OnDestroy()
    {
        RemoveCreatedTriggerEntries();
    }

    private void RemoveCreatedTriggerEntries()
    {
        foreach (TriggerBinding binding
                 in triggerBindings)
        {
            if (binding.eventTrigger == null ||
                binding.eventTrigger.triggers == null)
            {
                continue;
            }

            foreach (EventTrigger.Entry entry
                     in binding.entries)
            {
                binding.eventTrigger.triggers.Remove(
                    entry
                );
            }
        }

        triggerBindings.Clear();
    }
}
