using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RTLTMPro;

[DefaultExecutionOrder(10000)]
public class ButtonTextController : MonoBehaviour
{
    [Serializable]
    public class FollowButton
    {
        [Header("Button")]
        [Tooltip("Example: 001_Aortaa")]
        public Button button;

        [Header("Line")]
        [Tooltip("Example: 001_Aortaa_Line")]
        public Transform line;

        [Header("Target Model")]
        [Tooltip("The 3D model both the Line and Button will follow.")]
        public Transform targetModel;

        [Header("Drag Source (Optional)")]
        [Tooltip(
            "The VR3DClickable that controls visibility. " +
            "If empty, it is searched on the Target Model or on its parents."
        )]
        public VR3DClickable dragSource;

        [Header("Follow Scale")]
        public bool followScale = true;

        [Header("Button Camera Facing")]
        public bool buttonAlwaysFacesCamera = true;
        public Vector3 buttonCameraRotationOffset = Vector3.zero;

        [HideInInspector] public Vector3 lineLocalPosition;
        [HideInInspector] public Quaternion lineLocalRotation;
        [HideInInspector] public Vector3 lineScaleRatio;

        [HideInInspector] public Vector3 buttonLocalPosition;
        [HideInInspector] public Vector3 buttonScaleRatio;

        [HideInInspector] public Transform initializedTarget;
        [HideInInspector] public Transform initializedLine;
        [HideInInspector] public Transform initializedButton;
        [HideInInspector] public bool initialized;

        [HideInInspector] public VR3DClickable cachedDragSource;
    }

    [Serializable]
    public class ButtonTextItem
    {
        [Header("Buttons")]
        public List<FollowButton> buttons = new List<FollowButton>();

        [Header("Text")]
        [TextArea(3, 10)]
        public string text;
    }

    [Header("Target RTL Text")]
    public RTLTextMeshPro targetText;

    [Header("Auto Height")]
    [Tooltip("Drag BG RectTransform here.")]
    public RectTransform backgroundRect;

    [Tooltip("Extra vertical space around the text, measured in TEXT local units.")]
    public float backgroundVerticalPadding = 1.0f;

    [Tooltip("Very small safety minimum only. Do NOT use values like 40 for your current setup.")]
    public float minimumTextHeight = 0.5f;

    [Tooltip("Keep the top edge fixed and grow/shrink downward.")]
    public bool growDownwardOnly = true;

    [Tooltip("Keep BG in its current place. Its top stays fixed and it grows downward.")]
    public bool keepBackgroundCurrentTop = true;

    [Header("Camera")]
    public Camera targetCamera;

    // =========================================================
    // VISIBILITY
    // =========================================================

    public enum ToggleButtonMode
    {
        // The button turns the Labels ON and OFF.
        EnableOrDisableLabels,

        // The button forces every Label to show, then hides them all.
        ShowAllOrHideAll
    }

    public enum LabelVisibilityMode
    {
        // The Button and the Line show ONLY while the model is being dragged.
        OnlyWhileDragging,

        // The Button and the Line stay visible after the drag,
        // until another model is dragged or a Reset happens.
        StayVisibleUntilOtherModelOrReset
    }

    [Header("Visibility")]
    [Tooltip("Hide every Button and Line when the scene starts.")]
    public bool hideOnStart = true;

    [Tooltip("How the Button and the Line behave after you release the model.")]
    public LabelVisibilityMode visibilityMode =
        LabelVisibilityMode.StayVisibleUntilOtherModelOrReset;

    [Tooltip(
        "Used to read the currently selected model. " +
        "If empty, it will be found automatically."
    )]
    public ModelControlsManager modelControlsManager;

    [Header("Show / Hide Label Button")]
    [Tooltip("Optional: one global Button that controls all the Labels.")]
    public Button toggleAllButton;

    [Tooltip(
        "Enable Or Disable Labels = SHOW works normally with the drag, " +
        "HIDE never shows anything. " +
        "Show All Or Hide All = SHOW forces every Label to appear."
    )]
    public ToggleButtonMode toggleButtonMode = ToggleButtonMode.EnableOrDisableLabels;

    [Tooltip("Master switch. When OFF no Label can appear at all.")]
    public bool labelsEnabled = true;

    [Tooltip("Image that changes its Source Image. If empty, the Button image is used.")]
    public Image toggleButtonImage;

    [Tooltip("Source Image used while the Labels are ON.")]
    public Sprite showStateSprite;

    [Tooltip("Source Image used while the Labels are OFF.")]
    public Sprite hideStateSprite;

    // TRUE = force show everything (even without drag / selection).
    private bool showAllOverride;

    // TRUE = force hide everything, even if a model is selected.
    private bool forceHide;

    // The selection that was active when Force Hide started.
    private Transform forceHideSelection;

    // Local fallback selection when there is no ModelControlsManager.
    private Transform localSelectedModel;

    [Header("Button Groups")]
    public List<ButtonTextItem> items = new List<ButtonTextItem>();

    private RectTransform targetTextRect;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetText != null)
        {
            targetTextRect =
                targetText.GetComponent<RectTransform>();
        }

        if (growDownwardOnly)
        {
            SetTopPivotKeepWorldTop(targetTextRect);
            SetTopPivotKeepWorldTop(backgroundRect);
        }

        InitializeAll();

        ResizeTextAndBackground();

        if (modelControlsManager == null)
        {
            modelControlsManager =
                FindFirstObjectByType<ModelControlsManager>();
        }

        if (toggleAllButton != null)
        {
            toggleAllButton.onClick.AddListener(ToggleAllButtonsAndLines);
        }

        RefreshToggleButtonImage();

        if (hideOnStart)
        {
            showAllOverride = false;
            forceHide = false;
            localSelectedModel = null;

            SetVisibilityForAll(false);
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        UpdateAll();

        UpdateVisibility();
    }

    private void InitializeAll()
    {
        for (int i = 0; i < items.Count; i++)
        {
            int itemIndex = i;

            for (int j = 0; j < items[itemIndex].buttons.Count; j++)
            {
                FollowButton follow = items[itemIndex].buttons[j];

                if (follow == null)
                    continue;

                if (follow.button != null)
                {
                    follow.button.onClick.AddListener(
                        () => ShowText(itemIndex)
                    );
                }

                InitializeFollow(follow);
            }
        }
    }

    private void InitializeFollow(FollowButton follow)
    {
        if (follow == null ||
            follow.targetModel == null ||
            follow.button == null ||
            follow.line == null)
        {
            if (follow != null)
                follow.initialized = false;

            return;
        }

        Transform target = follow.targetModel;
        Transform lineTransform = follow.line;
        Transform buttonTransform = follow.button.transform;

        follow.lineLocalPosition =
            target.InverseTransformPoint(lineTransform.position);

        follow.lineLocalRotation =
            Quaternion.Inverse(target.rotation) *
            lineTransform.rotation;

        follow.lineScaleRatio =
            GetWorldScaleRatio(
                lineTransform.lossyScale,
                target.lossyScale
            );

        follow.buttonLocalPosition =
            target.InverseTransformPoint(buttonTransform.position);

        follow.buttonScaleRatio =
            GetWorldScaleRatio(
                buttonTransform.lossyScale,
                target.lossyScale
            );

        follow.initializedTarget = target;
        follow.initializedLine = lineTransform;
        follow.initializedButton = buttonTransform;
        follow.initialized = true;

        ResolveDragSource(follow);
    }

    private void UpdateAll()
    {
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = 0; j < items[i].buttons.Count; j++)
            {
                FollowButton follow = items[i].buttons[j];

                if (follow == null ||
                    follow.targetModel == null ||
                    follow.button == null ||
                    follow.line == null)
                {
                    if (follow != null)
                        follow.initialized = false;

                    continue;
                }

                Transform target = follow.targetModel;
                Transform lineTransform = follow.line;
                Transform buttonTransform = follow.button.transform;

                if (!follow.initialized ||
                    follow.initializedTarget != target ||
                    follow.initializedLine != lineTransform ||
                    follow.initializedButton != buttonTransform)
                {
                    InitializeFollow(follow);
                }

                if (!follow.initialized)
                    continue;

                // LINE follows model completely.
                lineTransform.position =
                    target.TransformPoint(follow.lineLocalPosition);

                lineTransform.rotation =
                    target.rotation *
                    follow.lineLocalRotation;

                if (follow.followScale)
                {
                    ApplyDesiredWorldScale(
                        lineTransform,
                        new Vector3(
                            target.lossyScale.x * follow.lineScaleRatio.x,
                            target.lossyScale.y * follow.lineScaleRatio.y,
                            target.lossyScale.z * follow.lineScaleRatio.z
                        )
                    );
                }

                // BUTTON follows position/scale, but faces camera.
                buttonTransform.position =
                    target.TransformPoint(follow.buttonLocalPosition);

                if (follow.followScale)
                {
                    ApplyDesiredWorldScale(
                        buttonTransform,
                        new Vector3(
                            target.lossyScale.x * follow.buttonScaleRatio.x,
                            target.lossyScale.y * follow.buttonScaleRatio.y,
                            target.lossyScale.z * follow.buttonScaleRatio.z
                        )
                    );
                }

                if (follow.buttonAlwaysFacesCamera)
                {
                    FaceButtonToCamera(follow);
                }
            }
        }
    }

    // =========================================================
    // SHOW / HIDE
    // =========================================================

    // Finds the VR3DClickable that drives this Button + Line.
    private VR3DClickable ResolveDragSource(FollowButton follow)
    {
        if (follow == null)
            return null;

        if (follow.dragSource != null)
            return follow.dragSource;

        if (follow.cachedDragSource != null)
            return follow.cachedDragSource;

        if (follow.targetModel == null)
            return null;

        follow.cachedDragSource =
            follow.targetModel.GetComponentInParent<VR3DClickable>();

        return follow.cachedDragSource;
    }

    // Returns TRUE while the model of this element is being dragged.
    private bool IsFollowDragging(FollowButton follow)
    {
        VR3DClickable clickable = ResolveDragSource(follow);

        if (clickable == null)
            return false;

        return clickable.IsDragging();
    }

    // Returns the model that is currently selected, or null.
    private Transform GetSelectedModel()
    {
        if (modelControlsManager != null)
        {
            return modelControlsManager.GetSelectedModel();
        }

        return localSelectedModel;
    }

    // Returns TRUE if this element belongs to the selected model.
    private bool IsFollowSelected(FollowButton follow, Transform selectedModel)
    {
        if (follow == null || selectedModel == null)
            return false;

        VR3DClickable clickable = ResolveDragSource(follow);

        if (clickable != null && clickable.transform == selectedModel)
            return true;

        if (follow.targetModel != null)
        {
            if (follow.targetModel == selectedModel)
                return true;

            if (follow.targetModel.IsChildOf(selectedModel))
                return true;
        }

        return false;
    }

    // Decides the visibility of every element, every frame.
    private void UpdateVisibility()
    {
        bool anyDragging = false;

        // Detect a new drag so the local fallback selection stays correct.
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = 0; j < items[i].buttons.Count; j++)
            {
                FollowButton follow = items[i].buttons[j];

                if (follow == null)
                    continue;

                if (!IsFollowDragging(follow))
                    continue;

                anyDragging = true;

                VR3DClickable clickable = ResolveDragSource(follow);

                if (clickable != null)
                {
                    localSelectedModel = clickable.transform;
                }
            }
        }

        // Master switch: nothing can appear while the Labels are OFF.
        if (!labelsEnabled)
        {
            SetVisibilityForAll(false);
            return;
        }

        Transform selectedModel = GetSelectedModel();

        // Force Hide is released when the selection changes,
        // or when the current drag ends.
        if (forceHide)
        {
            if (selectedModel != forceHideSelection || (!anyDragging && selectedModel == null))
            {
                forceHide = false;
                forceHideSelection = null;
            }
        }

        for (int i = 0; i < items.Count; i++)
        {
            for (int j = 0; j < items[i].buttons.Count; j++)
            {
                FollowButton follow = items[i].buttons[j];

                if (follow == null)
                    continue;

                bool visible;

                if (showAllOverride)
                {
                    visible = true;
                }
                else if (forceHide)
                {
                    visible = false;
                }
                else if (visibilityMode == LabelVisibilityMode.OnlyWhileDragging)
                {
                    visible = IsFollowDragging(follow);
                }
                else
                {
                    visible =
                        IsFollowDragging(follow) ||
                        IsFollowSelected(follow, selectedModel);
                }

                ApplyVisibility(follow, visible);
            }
        }
    }

    private void ApplyVisibility(FollowButton follow, bool visible)
    {
        if (follow == null)
            return;

        if (follow.button != null &&
            follow.button.gameObject.activeSelf != visible)
        {
            follow.button.gameObject.SetActive(visible);
        }

        if (follow.line != null &&
            follow.line.gameObject.activeSelf != visible)
        {
            follow.line.gameObject.SetActive(visible);
        }
    }

    private void SetVisibilityForAll(bool visible)
    {
        for (int i = 0; i < items.Count; i++)
        {
            for (int j = 0; j < items[i].buttons.Count; j++)
            {
                ApplyVisibility(items[i].buttons[j], visible);
            }
        }
    }

    // Global button. Its behaviour depends on Toggle Button Mode.
    public void ToggleAllButtonsAndLines()
    {
        if (toggleButtonMode == ToggleButtonMode.EnableOrDisableLabels)
        {
            SetLabelsEnabled(!labelsEnabled);
            return;
        }

        ToggleShowAll();
    }

    // Turns the Labels ON or OFF completely.
    public void SetLabelsEnabled(bool enabled)
    {
        labelsEnabled = enabled;

        if (!labelsEnabled)
        {
            showAllOverride = false;
            forceHide = false;
            forceHideSelection = null;

            SetVisibilityForAll(false);
        }

        RefreshToggleButtonImage();

        UpdateVisibility();
    }

    public void ShowLabels()
    {
        SetLabelsEnabled(true);
    }

    public void HideLabels()
    {
        SetLabelsEnabled(false);
    }

    public bool AreLabelsEnabled()
    {
        return labelsEnabled;
    }

    // Hides the Label of a model, used after a Snap for example.
    public void HideLabelsForModel(Transform model)
    {
        if (model == null)
            return;

        if (localSelectedModel == model)
        {
            localSelectedModel = null;
        }

        if (modelControlsManager != null &&
            modelControlsManager.GetSelectedModel() == model)
        {
            modelControlsManager.ClearSelectedModel();
        }

        UpdateVisibility();
    }

    // Swaps the Source Image of the toggle button.
    private void RefreshToggleButtonImage()
    {
        Image image = toggleButtonImage;

        if (image == null && toggleAllButton != null)
        {
            image = toggleAllButton.image != null
                ? toggleAllButton.image
                : toggleAllButton.GetComponent<Image>();
        }

        if (image == null)
            return;

        bool showingState =
            toggleButtonMode == ToggleButtonMode.EnableOrDisableLabels
                ? labelsEnabled
                : showAllOverride;

        Sprite sprite = showingState ? showStateSprite : hideStateSprite;

        if (sprite != null)
        {
            image.sprite = sprite;
        }
    }

    // First press shows everything, second press hides everything.
    private void ToggleShowAll()
    {
        if (showAllOverride)
        {
            showAllOverride = false;
            forceHide = true;
            forceHideSelection = GetSelectedModel();
        }
        else
        {
            showAllOverride = true;
            forceHide = false;
            forceHideSelection = null;
        }

        RefreshToggleButtonImage();

        UpdateVisibility();
    }

    // Forces everything visible.
    public void ShowAllButtonsAndLines()
    {
        showAllOverride = true;
        forceHide = false;
        forceHideSelection = null;

        UpdateVisibility();
    }

    // Forces everything hidden, even while dragging or selected.
    public void HideAllButtonsAndLines()
    {
        showAllOverride = false;
        forceHide = true;
        forceHideSelection = GetSelectedModel();

        UpdateVisibility();
    }

    // Goes back to the normal drag / selection behaviour.
    public void UseDragVisibility()
    {
        showAllOverride = false;
        forceHide = false;
        forceHideSelection = null;

        UpdateVisibility();
    }

    // Clears the selection so every Label hides again.
    public void ClearSelectionAndHide()
    {
        localSelectedModel = null;

        if (modelControlsManager != null)
        {
            modelControlsManager.ClearSelectedModel();
        }

        showAllOverride = false;
        forceHide = false;
        forceHideSelection = null;

        SetVisibilityForAll(false);
    }

    public bool IsShowingAll()
    {
        return showAllOverride;
    }

    private void FaceButtonToCamera(FollowButton follow)
    {
        if (targetCamera == null ||
            follow.button == null)
            return;

        Transform buttonTransform = follow.button.transform;

        buttonTransform.rotation =
            targetCamera.transform.rotation *
            Quaternion.Euler(follow.buttonCameraRotationOffset);
    }

    private Vector3 GetWorldScaleRatio(
        Vector3 objectWorldScale,
        Vector3 targetWorldScale)
    {
        return new Vector3(
            SafeDivide(objectWorldScale.x, targetWorldScale.x),
            SafeDivide(objectWorldScale.y, targetWorldScale.y),
            SafeDivide(objectWorldScale.z, targetWorldScale.z)
        );
    }

    private void ApplyDesiredWorldScale(
        Transform objectTransform,
        Vector3 desiredWorldScale)
    {
        Transform parent = objectTransform.parent;

        if (parent == null)
        {
            objectTransform.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentWorldScale = parent.lossyScale;

        objectTransform.localScale = new Vector3(
            SafeDivide(desiredWorldScale.x, parentWorldScale.x),
            SafeDivide(desiredWorldScale.y, parentWorldScale.y),
            SafeDivide(desiredWorldScale.z, parentWorldScale.z)
        );
    }

    private float SafeDivide(float value, float divisor)
    {
        if (Mathf.Abs(divisor) < 0.00001f)
            return value;

        return value / divisor;
    }

    private void ShowText(int index)
    {
        if (targetText == null)
            return;

        if (index < 0 || index >= items.Count)
            return;

        targetText.text = items[index].text;

        ResizeTextAndBackground();
    }

    // =========================================================
    // AUTO HEIGHT
    // =========================================================

    private void ResizeTextAndBackground()
    {
        if (targetText == null)
            return;

        if (targetTextRect == null)
        {
            targetTextRect =
                targetText.GetComponent<RectTransform>();
        }

        if (targetTextRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        targetText.ForceMeshUpdate();

        float fixedTextWidth =
            targetTextRect.rect.width;

        if (fixedTextWidth <= 0.001f)
        {
            fixedTextWidth =
                Mathf.Abs(targetTextRect.sizeDelta.x);
        }

        // Calculate preferred height using the CURRENT fixed width.
        Vector2 preferred =
            targetText.GetPreferredValues(
                targetText.text,
                fixedTextWidth,
                Mathf.Infinity
            );

        float desiredTextHeight =
            Mathf.Max(
                minimumTextHeight,
                preferred.y
            );

        // Text grows/shrinks vertically only.
        targetTextRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            desiredTextHeight
        );

        targetText.ForceMeshUpdate();

        if (backgroundRect != null)
        {
            // Convert Text-local height into BG-local height.
            // This fixes the different scale values visible in your Inspector.
            float textWorldScaleY =
                Mathf.Abs(targetTextRect.lossyScale.y);

            float bgWorldScaleY =
                Mathf.Abs(backgroundRect.lossyScale.y);

            if (textWorldScaleY < 0.00001f)
                textWorldScaleY = 1f;

            if (bgWorldScaleY < 0.00001f)
                bgWorldScaleY = 1f;

            float desiredBackgroundWorldHeight =
                (desiredTextHeight + backgroundVerticalPadding) *
                textWorldScaleY;

            float desiredBackgroundLocalHeight =
                desiredBackgroundWorldHeight /
                bgWorldScaleY;

            backgroundRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                desiredBackgroundLocalHeight
            );

        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetTopPivotKeepWorldTop(
        RectTransform rect
    )
    {
        if (rect == null)
            return;

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(corners);

        Vector3 oldTopCenter =
            (corners[1] + corners[2]) * 0.5f;

        Vector2 newPivot =
            rect.pivot;

        newPivot.y = 1f;
        rect.pivot = newPivot;

        rect.GetWorldCorners(corners);

        Vector3 newTopCenter =
            (corners[1] + corners[2]) * 0.5f;

        rect.position +=
            oldTopCenter - newTopCenter;
    }

    private void OnDestroy()
    {
        if (toggleAllButton != null)
        {
            toggleAllButton.onClick.RemoveListener(ToggleAllButtonsAndLines);
        }
    }
}