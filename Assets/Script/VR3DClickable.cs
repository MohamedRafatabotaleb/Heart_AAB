using UnityEngine;
using UnityEngine.Events;

public class VR3DClickable : MonoBehaviour
{
    [Header("Click")]
    public UnityEvent OnClick;

    [Header("Hover")]
    public UnityEvent OnHoverEnter;
    public UnityEvent OnHoverExit;

    [Header("Drag")]
    public bool enableDrag = true;
    public bool moveX = true;
    public bool moveY = true;
    public bool keepZ = true;

    [Header("Model Controls")]
    [Tooltip(
        "Manager used to select this model after Drag. " +
        "If empty, it will be found automatically."
    )]
    [SerializeField]
    private ModelControlsManager modelControlsManager;

    private Vector3 dragStartPosition;
    private Vector3 dragStartHitPoint;

    private bool dragging;

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

    public void BeginDrag(Vector3 hitPoint)
    {
        if (!enableDrag)
            return;

        dragging = true;

        dragStartPosition =
            transform.position;

        dragStartHitPoint =
            hitPoint;

        FindControlsManagerIfNeeded();

        if (modelControlsManager != null)
        {
            modelControlsManager.SetSelectedModel(
                transform
            );
        }

        Debug.Log(
            "BEGIN DRAG + SELECT MODEL: " +
            gameObject.name,
            gameObject
        );
    }

    public void Drag(Vector3 currentHitPoint)
    {
        if (!dragging ||
            !enableDrag)
        {
            return;
        }

        Vector3 delta =
            currentHitPoint -
            dragStartHitPoint;

        Vector3 newPosition =
            dragStartPosition;

        if (moveX)
        {
            newPosition.x += delta.x;
        }

        if (moveY)
        {
            newPosition.y += delta.y;
        }

        if (keepZ)
        {
            newPosition.z =
                dragStartPosition.z;
        }
        else
        {
            newPosition.z += delta.z;
        }

        transform.position =
            newPosition;
    }

    public void EndDrag()
    {
        dragging = false;
    }

    public bool IsDragging()
    {
        return dragging;
    }
}
