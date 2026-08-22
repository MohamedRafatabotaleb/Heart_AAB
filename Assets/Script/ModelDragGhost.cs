using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Creates a transparent copy of the model at its home place.
// The ghost appears when the model is dragged away,
// and the model snaps back when it comes close to the ghost.
[RequireComponent(typeof(VR3DClickable))]
public class ModelDragGhost : MonoBehaviour
{
    // What makes the model snap back home.
    public enum SnapTriggerMode
    {
        // Snaps as soon as the model mesh touches the ghost mesh.
        MeshTouch,

        // Snaps when the model gets closer than Snap Distance.
        Distance
    }

    [Header("Ghost")]
    [Tooltip("Creates the transparent copy automatically on Start.")]
    public bool createGhostAutomatically = true;

    [Tooltip("Optional: use an existing ghost object instead of creating one.")]
    public Transform customGhost;

    [Tooltip("Puts the ghost on the Ignore Raycast layer so the pointer never hits it.")]
    public bool setGhostToIgnoreRaycastLayer = true;

    [Tooltip("Optional: material used for the ghost. If empty, one is created.")]
    public Material ghostMaterial;

    [Tooltip("Transparency of the auto created ghost material.")]
    [Range(0f, 1f)]
    public float ghostAlpha = 0.25f;

    [Tooltip("Color tint of the auto created ghost material.")]
    public Color ghostTint = Color.white;

    [Header("Visibility")]
    [Tooltip("The ghost only appears while the model is being dragged.")]
    public bool showOnlyWhileDragging = true;

    [Tooltip("Distance mode only: the ghost appears after the model moves this far from home.")]
    public float showDistance = 0.05f;

    [Tooltip("The ghost copies the current scale of the model.")]
    public bool matchModelScale = true;

    [Tooltip("The ghost copies the current rotation of the model.")]
    public bool matchModelRotation = false;

    [Header("Snap")]
    [Tooltip("Snaps the model back home when it reaches the ghost.")]
    public bool enableSnap = true;

    [Tooltip("Mesh Touch = snap the moment the two meshes overlap.")]
    public SnapTriggerMode snapTrigger = SnapTriggerMode.MeshTouch;

    [Tooltip(
        "Mesh Touch only. Extra space around the meshes. " +
        "Positive = snaps a bit earlier, negative = needs a deeper overlap."
    )]
    public float meshTouchPadding = 0f;

    [Tooltip("Distance mode only: distance from home that triggers the snap.")]
    public float snapDistance = 0.08f;

    [Tooltip(
        "Distance mode only: the snap becomes active AFTER the model moves this far from home."
    )]
    public float snapArmDistance = 0.2f;

    [Tooltip("Snap only after you release the model, not while dragging.")]
    public bool snapOnlyOnRelease = false;

    [Tooltip("Returns the model to its ORIGINAL rotation when it snaps.")]
    public bool snapRotation = true;

    [Tooltip("Also restores the original scale.")]
    public bool snapScale = false;

    [Tooltip("Duration of the snap animation. 0 = instant.")]
    public float snapDuration = 0.25f;

    [Tooltip("Easing curve of the snap animation.")]
    public AnimationCurve snapEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Releases the drag as soon as the snap starts.")]
    public bool endDragOnSnap = true;

    [Tooltip("Hides the Button and the Line of this model after the snap.")]
    public bool hideLabelOnSnap = true;

    [Header("Events")]
    public UnityEvent OnGhostShown;
    public UnityEvent OnGhostHidden;
    public UnityEvent OnSnapped;

    private VR3DClickable clickable;

    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;

    private Transform ghostTransform;
    private readonly List<Renderer> ghostRenderers = new List<Renderer>();
    private readonly List<Renderer> modelRenderers = new List<Renderer>();

    private Material createdGhostMaterial;

    private bool ghostVisible;
    private bool ghostVisibilityApplied;
    private bool wasDragging;
    private bool isSnapping;
    private bool snapArmed;

    private Coroutine snapCoroutine;

    private void Awake()
    {
        clickable = GetComponent<VR3DClickable>();
    }

    private void Start()
    {
        SaveHomeTransform();
        CollectModelRenderers();

        if (customGhost != null)
        {
            SetupExistingGhost(customGhost);
        }
        else if (createGhostAutomatically)
        {
            CreateGhost();
        }

        SetGhostVisible(false);
    }

    // =========================================================
    // HOME
    // =========================================================

    // Saves the place the model should return to.
    [ContextMenu("Save Home Transform")]
    public void SaveHomeTransform()
    {
        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
    }

    public Vector3 GetHomeWorldPosition()
    {
        if (homeParent == null) return homeLocalPosition;

        return homeParent.TransformPoint(homeLocalPosition);
    }

    public Quaternion GetHomeWorldRotation()
    {
        if (homeParent == null) return homeLocalRotation;

        return homeParent.rotation * homeLocalRotation;
    }

    // =========================================================
    // GHOST CREATION
    // =========================================================

    // Clones the model, strips everything, and makes it transparent.
    private void CreateGhost()
    {
        GameObject clone = Instantiate(gameObject, homeParent);
        clone.name = gameObject.name + "_Ghost";

        StripGhostComponents(clone);

        ghostTransform = clone.transform;

        ghostTransform.localPosition = homeLocalPosition;
        ghostTransform.localRotation = homeLocalRotation;
        ghostTransform.localScale = homeLocalScale;

        CollectGhostRenderers();
        ApplyGhostMaterial();
        ApplyGhostLayer();
    }

    // Uses a ghost object that already exists in the scene.
    private void SetupExistingGhost(Transform existingGhost)
    {
        ghostTransform = existingGhost;

        CollectGhostRenderers();

        if (ghostMaterial != null)
        {
            ApplyGhostMaterial();
        }

        ApplyGhostLayer();
    }

    // Moves the ghost to the Ignore Raycast layer.
    private void ApplyGhostLayer()
    {
        if (!setGhostToIgnoreRaycastLayer) return;
        if (ghostTransform == null) return;

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");

        if (ignoreRaycastLayer < 0) return;

        Transform[] allChildren = ghostTransform.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allChildren)
        {
            if (child != null) child.gameObject.layer = ignoreRaycastLayer;
        }
    }

    // Removes scripts, colliders, animations, and physics from the ghost.
    private void StripGhostComponents(GameObject clone)
    {
        // The ghost script must go first, it requires VR3DClickable.
        ModelDragGhost[] ghostScripts = clone.GetComponentsInChildren<ModelDragGhost>(true);

        foreach (ModelDragGhost ghostScript in ghostScripts)
        {
            if (ghostScript != null) DestroyImmediate(ghostScript);
        }

        Component[] components = clone.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null) continue;
            if (component is Transform) continue;
            if (component is MeshFilter) continue;
            if (component is Renderer) continue;

            DestroyImmediate(component);
        }
    }

    private void CollectGhostRenderers()
    {
        ghostRenderers.Clear();

        if (ghostTransform == null) return;

        Renderer[] renderers = ghostTransform.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null) ghostRenderers.Add(renderer);
        }
    }

    private void ApplyGhostMaterial()
    {
        Material materialToUse = ghostMaterial;

        if (materialToUse == null)
        {
            if (createdGhostMaterial == null)
            {
                createdGhostMaterial = BuildTransparentMaterial();
            }

            materialToUse = createdGhostMaterial;
        }

        if (materialToUse == null) return;

        foreach (Renderer renderer in ghostRenderers)
        {
            if (renderer == null) continue;

            Material[] materials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = materialToUse;
            }

            renderer.sharedMaterials = materials;
        }
    }

    // Builds a simple transparent material that works with URP.
    private Material BuildTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        Material material = new Material(shader);
        material.name = "GhostMaterial_Runtime";

        Color color = ghostTint;
        color.a = ghostAlpha;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }

    // =========================================================
    // MESH TOUCH
    // =========================================================

    private void CollectModelRenderers()
    {
        modelRenderers.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null) modelRenderers.Add(renderer);
        }
    }

    // Combines the world bounds of a list of renderers.
    private bool TryGetWorldBounds(List<Renderer> renderers, out Bounds bounds)
    {
        bounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    // Returns TRUE when the model mesh overlaps the ghost mesh.
    private bool AreMeshesTouching()
    {
        if (!TryGetWorldBounds(modelRenderers, out Bounds modelBounds)) return false;
        if (!TryGetWorldBounds(ghostRenderers, out Bounds ghostBounds)) return false;

        if (Mathf.Abs(meshTouchPadding) > 0.00001f)
        {
            Vector3 padding = Vector3.one * meshTouchPadding * 2f;

            modelBounds.size += padding;
        }

        return modelBounds.Intersects(ghostBounds);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (ghostTransform == null) return;

        UpdateGhostTransform();

        bool dragging = clickable != null && clickable.IsDragging();

        float distanceToHome =
            Vector3.Distance(transform.position, GetHomeWorldPosition());

        bool meshesTouching =
            snapTrigger == SnapTriggerMode.MeshTouch && AreMeshesTouching();

        // The snap is armed ONLY after the model leaves its home place,
        // otherwise it would snap back the moment you grab it.
        if (snapTrigger == SnapTriggerMode.MeshTouch)
        {
            if (!meshesTouching) snapArmed = true;
        }
        else
        {
            float armDistance = Mathf.Max(snapArmDistance, snapDistance * 1.5f);

            if (distanceToHome >= armDistance) snapArmed = true;
        }

        UpdateGhostVisibility(dragging, distanceToHome, meshesTouching);

        if (enableSnap && snapArmed && !isSnapping)
        {
            bool readyToSnap =
                snapTrigger == SnapTriggerMode.MeshTouch
                    ? meshesTouching
                    : distanceToHome <= snapDistance;

            bool releasedNow = wasDragging && !dragging;

            if (snapOnlyOnRelease)
            {
                if (releasedNow && readyToSnap) StartSnap();
            }
            else
            {
                if (dragging && readyToSnap) StartSnap();
            }
        }

        wasDragging = dragging;
    }

    // Keeps the ghost at the home place with the current look of the model.
    private void UpdateGhostTransform()
    {
        if (ghostTransform.parent != homeParent)
        {
            ghostTransform.SetParent(homeParent, false);
        }

        ghostTransform.localPosition = homeLocalPosition;

        ghostTransform.localRotation =
            matchModelRotation ? transform.localRotation : homeLocalRotation;

        ghostTransform.localScale =
            matchModelScale ? transform.localScale : homeLocalScale;
    }

    private void UpdateGhostVisibility(bool dragging, float distanceToHome, bool meshesTouching)
    {
        bool visible;

        if (snapTrigger == SnapTriggerMode.MeshTouch)
        {
            // The ghost appears once the model no longer covers it.
            visible = !meshesTouching;
        }
        else
        {
            visible = distanceToHome >= showDistance;
        }

        if (showOnlyWhileDragging && !dragging) visible = false;
        if (isSnapping) visible = false;

        SetGhostVisible(visible);
    }

    private void SetGhostVisible(bool visible)
    {
        // The first call must always be applied,
        // otherwise the ghost stays visible from the start.
        if (ghostVisibilityApplied && ghostVisible == visible) return;

        ghostVisibilityApplied = true;
        ghostVisible = visible;

        foreach (Renderer renderer in ghostRenderers)
        {
            if (renderer != null) renderer.enabled = visible;
        }

        if (visible) OnGhostShown?.Invoke();
        else OnGhostHidden?.Invoke();
    }

    // =========================================================
    // SNAP
    // =========================================================

    // Starts the return animation to the home place.
    [ContextMenu("Snap Now")]
    public void StartSnap()
    {
        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }

        snapCoroutine = StartCoroutine(SnapRoutine());
    }

    private IEnumerator SnapRoutine()
    {
        isSnapping = true;

        if (endDragOnSnap && clickable != null)
        {
            clickable.EndDrag();
        }

        SetGhostVisible(false);

        if (transform.parent != homeParent)
        {
            transform.SetParent(homeParent, true);
        }

        Vector3 startLocalPosition = transform.localPosition;
        Quaternion startLocalRotation = transform.localRotation;
        Vector3 startLocalScale = transform.localScale;

        Vector3 endLocalPosition = homeLocalPosition;
        Quaternion endLocalRotation = snapRotation ? homeLocalRotation : startLocalRotation;
        Vector3 endLocalScale = snapScale ? homeLocalScale : startLocalScale;

        if (snapDuration > 0f)
        {
            float elapsedTime = 0f;

            while (elapsedTime < snapDuration)
            {
                elapsedTime += Time.deltaTime;

                float normalizedTime = Mathf.Clamp01(elapsedTime / snapDuration);
                float easedTime = snapEaseCurve.Evaluate(normalizedTime);

                transform.localPosition =
                    Vector3.LerpUnclamped(startLocalPosition, endLocalPosition, easedTime);

                transform.localRotation =
                    Quaternion.SlerpUnclamped(startLocalRotation, endLocalRotation, easedTime);

                transform.localScale =
                    Vector3.LerpUnclamped(startLocalScale, endLocalScale, easedTime);

                yield return null;
            }
        }

        transform.localPosition = endLocalPosition;
        transform.localRotation = endLocalRotation;
        transform.localScale = endLocalScale;

        isSnapping = false;
        snapCoroutine = null;
        snapArmed = false;

        if (hideLabelOnSnap)
        {
            HideLabelForThisModel();
        }

        Debug.Log("SNAPPED HOME: " + gameObject.name, gameObject);

        OnSnapped?.Invoke();
    }

    // Clears the selection so the Label of this model disappears.
    private void HideLabelForThisModel()
    {
        ButtonTextController[] controllers =
            FindObjectsByType<ButtonTextController>(FindObjectsSortMode.None);

        bool handled = false;

        foreach (ButtonTextController controller in controllers)
        {
            if (controller == null) continue;

            controller.HideLabelsForModel(transform);
            handled = true;
        }

        if (handled) return;

        ModelControlsManager manager =
            FindFirstObjectByType<ModelControlsManager>();

        if (manager != null && manager.GetSelectedModel() == transform)
        {
            manager.ClearSelectedModel();
        }
    }

    public bool IsSnapping()
    {
        return isSnapping;
    }

    private void OnDisable()
    {
        if (snapCoroutine != null)
        {
            StopCoroutine(snapCoroutine);
            snapCoroutine = null;
        }

        isSnapping = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        if (snapTrigger == SnapTriggerMode.MeshTouch)
        {
            if (TryGetWorldBounds(ghostRenderers, out Bounds ghostBounds))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(ghostBounds.center, ghostBounds.size);
            }

            if (TryGetWorldBounds(modelRenderers, out Bounds modelBounds))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(modelBounds.center, modelBounds.size);
            }

            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetHomeWorldPosition(), snapDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(GetHomeWorldPosition(), Mathf.Max(snapArmDistance, snapDistance * 1.5f));
    }
}