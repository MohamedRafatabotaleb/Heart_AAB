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
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        UpdateAll();
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


}
