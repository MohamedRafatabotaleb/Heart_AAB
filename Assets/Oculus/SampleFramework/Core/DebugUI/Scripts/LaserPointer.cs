using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class LaserPointer : OVRCursor
{
    public enum LaserBeamBehavior
    {
        On,
        Off,
        OnWhenHitTarget
    }

    [Header("Visual")]
    public GameObject cursorVisual;
    public float maxLength = 10.0f;

    [Header("3D Interaction")]
    public bool enable3DInteraction = true;

    [Tooltip("Layers that can be clicked by the laser")]
    public LayerMask interactionLayers = ~0;

    [Tooltip("Right controller index trigger")]
    public bool useRightController = true;

    [Tooltip("Left controller index trigger")]
    public bool useLeftController = false;

    [Header("Drag")]
    [Tooltip("Enable holding the trigger to move a VR3DClickable object")]
    public bool enableDrag = true;

    [Tooltip("How far the pointer must move before the interaction is treated as a drag instead of a click")]
    public float dragThreshold = 0.01f;

    [Header("Laser")]
    public LaserBeamBehavior laserBeamBehavior = LaserBeamBehavior.On;

    private Vector3 _startPoint;
    private Vector3 _forward;
    private Vector3 _endPoint;

    private bool _hitTarget;

    private LineRenderer lineRenderer;

    private GameObject current3DObject;
    private VR3DClickable currentClickable;

    // Drag state
    private VR3DClickable draggedClickable;
    private Plane dragPlane;
    private Vector3 dragStartPoint;
    private Vector3 lastDragPoint;
    private bool hasDragged;

    private bool m_restoreOnInputAcquired = false;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        if (cursorVisual)
            cursorVisual.SetActive(false);

        OVRManager.InputFocusAcquired += OnInputFocusAcquired;
        OVRManager.InputFocusLost += OnInputFocusLost;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = laserBeamBehavior != LaserBeamBehavior.Off;
        }
    }

    public override void SetCursorStartDest(
        Vector3 start,
        Vector3 dest,
        Vector3 normal)
    {
        _startPoint = start;
        _endPoint = dest;
        _hitTarget = true;
    }

    public override void SetCursorRay(Transform t)
    {
        _startPoint = t.position;
        _forward = t.forward;
        _hitTarget = false;
    }

    private void LateUpdate()
    {
        if (lineRenderer == null)
            return;

        Vector3 laserEnd = _startPoint + maxLength * _forward;

        // ------------------------------------------------
        // Active Drag
        // ------------------------------------------------
        if (draggedClickable != null)
        {
            Vector3 planePoint;

            if (TryGetDragPlanePoint(out planePoint))
            {
                laserEnd = planePoint;
                lastDragPoint = planePoint;

                if (Vector3.Distance(dragStartPoint, planePoint) >= dragThreshold)
                    hasDragged = true;

                if (enableDrag && IsClickHeld())
                {
                    draggedClickable.Drag(planePoint);
                }
            }

            if (IsClickReleased())
            {
                End3DDrag();
            }
        }
        else
        {
            // ------------------------------------------------
            // 3D Physics Raycast
            // ------------------------------------------------
            if (enable3DInteraction)
            {
                RaycastHit hit;

                if (Physics.Raycast(
                    _startPoint,
                    _forward,
                    out hit,
                    maxLength,
                    interactionLayers,
                    QueryTriggerInteraction.Collide))
                {
                    laserEnd = hit.point;

                    Handle3DHover(hit.collider.gameObject);

                    if (IsClickPressed())
                    {
                        VR3DClickable clickable =
                            hit.collider.gameObject.GetComponentInParent<VR3DClickable>();

                        if (clickable != null)
                        {
                            if (enableDrag && clickable.enableDrag)
                            {
                                Begin3DDrag(clickable, hit.point);
                            }
                            else
                            {
                                clickable.Click();
                                Debug.Log("3D CLICK: " + clickable.gameObject.name);
                            }
                        }
                    }
                }
                else
                {
                    Clear3DHover();
                }
            }
        }

        // ------------------------------------------------
        // Original OVR UI Raycast
        // ------------------------------------------------
        if (_hitTarget && draggedClickable == null)
        {
            laserEnd = _endPoint;

            if (cursorVisual)
            {
                cursorVisual.transform.position = _endPoint;
                cursorVisual.SetActive(true);
            }
        }
        else
        {
            if (cursorVisual)
                cursorVisual.SetActive(false);
        }

        // ------------------------------------------------
        // Draw Laser
        // ------------------------------------------------
        lineRenderer.SetPosition(0, _startPoint);
        lineRenderer.SetPosition(1, laserEnd);

        if (laserBeamBehavior == LaserBeamBehavior.Off)
        {
            lineRenderer.enabled = false;
        }
        else if (laserBeamBehavior == LaserBeamBehavior.On)
        {
            lineRenderer.enabled = true;
        }
        else if (laserBeamBehavior == LaserBeamBehavior.OnWhenHitTarget)
        {
            lineRenderer.enabled = _hitTarget || currentClickable != null || draggedClickable != null;
        }
    }

    // ====================================================
    // 3D HOVER
    // ====================================================

    private void Handle3DHover(GameObject hitObject)
    {
        VR3DClickable clickable =
            hitObject.GetComponentInParent<VR3DClickable>();

        if (clickable == null)
        {
            Clear3DHover();
            return;
        }

        if (currentClickable == clickable)
            return;

        Clear3DHover();

        currentClickable = clickable;
        current3DObject = clickable.gameObject;

        currentClickable.PointerEnter();

        Debug.Log("3D HOVER: " + current3DObject.name);
    }

    private void Clear3DHover()
    {
        if (currentClickable != null)
        {
            currentClickable.PointerExit();
        }

        currentClickable = null;
        current3DObject = null;
    }

    // ====================================================
    // 3D DRAG
    // ====================================================

    private void Begin3DDrag(VR3DClickable clickable, Vector3 hitPoint)
    {
        draggedClickable = clickable;
        dragStartPoint = hitPoint;
        lastDragPoint = hitPoint;
        hasDragged = false;

        // Fixed plane through the original hit point.
        // This prevents the raycast point from jumping as the object itself moves.
        dragPlane = new Plane(_forward.normalized, hitPoint);

        draggedClickable.BeginDrag(hitPoint);

        Debug.Log("3D DRAG START: " + draggedClickable.gameObject.name);
    }

    private void End3DDrag()
    {
        if (draggedClickable == null)
            return;

        VR3DClickable releasedClickable = draggedClickable;

        releasedClickable.EndDrag();

        // A press without meaningful movement still behaves like a normal click.
        if (!hasDragged)
        {
            releasedClickable.Click();
            Debug.Log("3D CLICK: " + releasedClickable.gameObject.name);
        }

        Debug.Log("3D DRAG END: " + releasedClickable.gameObject.name);

        draggedClickable = null;
        hasDragged = false;
    }

    private bool TryGetDragPlanePoint(out Vector3 point)
    {
        Ray ray = new Ray(_startPoint, _forward);
        float enter;

        if (dragPlane.Raycast(ray, out enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = lastDragPoint;
        return false;
    }

    // ====================================================
    // CONTROLLER TRIGGER
    // ====================================================

    private bool IsClickPressed()
    {
        bool rightClick = false;
        bool leftClick = false;

        if (useRightController)
        {
            rightClick = OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch);
        }

        if (useLeftController)
        {
            leftClick = OVRInput.GetDown(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch);
        }

        return rightClick || leftClick;
    }

    private bool IsClickHeld()
    {
        bool rightHeld = false;
        bool leftHeld = false;

        if (useRightController)
        {
            rightHeld = OVRInput.Get(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch);
        }

        if (useLeftController)
        {
            leftHeld = OVRInput.Get(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch);
        }

        return rightHeld || leftHeld;
    }

    private bool IsClickReleased()
    {
        bool rightReleased = false;
        bool leftReleased = false;

        if (useRightController)
        {
            rightReleased = OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.RTouch);
        }

        if (useLeftController)
        {
            leftReleased = OVRInput.GetUp(
                OVRInput.Button.PrimaryIndexTrigger,
                OVRInput.Controller.LTouch);
        }

        return rightReleased || leftReleased;
    }

    // ====================================================
    // LASER FOCUS
    // ====================================================

    private void OnDisable()
    {
        if (draggedClickable != null)
        {
            draggedClickable.EndDrag();
            draggedClickable = null;
        }

        Clear3DHover();

        if (cursorVisual)
            cursorVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        OVRManager.InputFocusAcquired -= OnInputFocusAcquired;
        OVRManager.InputFocusLost -= OnInputFocusLost;
    }

    public void OnInputFocusLost()
    {
        if (gameObject && gameObject.activeInHierarchy)
        {
            m_restoreOnInputAcquired = true;
            gameObject.SetActive(false);
        }
    }

    public void OnInputFocusAcquired()
    {
        if (m_restoreOnInputAcquired && gameObject)
        {
            m_restoreOnInputAcquired = false;
            gameObject.SetActive(true);
        }
    }
}
