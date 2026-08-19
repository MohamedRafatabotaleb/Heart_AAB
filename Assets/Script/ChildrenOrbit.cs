using UnityEngine;

public class ChildrenOrbit : MonoBehaviour
{
    [Header("Orbit Settings")]
    public Transform orbitCenter;
    public float orbitRadius = 5f;
    public float orbitSpeed = 30f;

    [Header("Start Movement")]
    public float moveOutDuration = 2f;

    [Header("Rotation")]
    public bool lookAtCenter = true;

    [Header("Direction")]
    public bool clockwise = true;

    private Transform[] objects;
    private Vector3[] startPositions;
    private float[] orbitAngles;

    private bool isOrbiting = false;
    private float moveTimer = 0f;

    void Start()
    {
        int childCount = transform.childCount;

        objects = new Transform[childCount];
        startPositions = new Vector3[childCount];
        orbitAngles = new float[childCount];

        for (int i = 0; i < childCount; i++)
        {
            objects[i] = transform.GetChild(i);

            // Store the initial position of each object
            startPositions[i] = objects[i].position;

            // Give each object a different starting angle
            orbitAngles[i] = (360f / childCount) * i;
        }
    }

    void Update()
    {
        if (orbitCenter == null || objects.Length == 0)
            return;

        if (!isOrbiting)
        {
            MoveObjectsToOrbit();

            moveTimer += Time.deltaTime;

            if (moveTimer >= moveOutDuration)
            {
                isOrbiting = true;
            }
        }
        else
        {
            OrbitObjects();
        }
    }

    void MoveObjectsToOrbit()
    {
        float t = Mathf.Clamp01(moveTimer / moveOutDuration);

        // Smooth movement
        t = Mathf.SmoothStep(0f, 1f, t);

        for (int i = 0; i < objects.Length; i++)
        {
            float angle = orbitAngles[i] * Mathf.Deg2Rad;

            Vector3 targetPosition =
                orbitCenter.position +
                new Vector3(
                    Mathf.Cos(angle) * orbitRadius,
                    0f,
                    Mathf.Sin(angle) * orbitRadius
                );

            objects[i].position = Vector3.Lerp(
                startPositions[i],
                targetPosition,
                t
            );

            if (lookAtCenter)
            {
                LookAtCenter(objects[i]);
            }
        }
    }

    void OrbitObjects()
    {
        float direction = clockwise ? -1f : 1f;

        for (int i = 0; i < objects.Length; i++)
        {
            orbitAngles[i] += orbitSpeed * direction * Time.deltaTime;

            float angle = orbitAngles[i] * Mathf.Deg2Rad;

            Vector3 newPosition =
                orbitCenter.position +
                new Vector3(
                    Mathf.Cos(angle) * orbitRadius,
                    0f,
                    Mathf.Sin(angle) * orbitRadius
                );

            objects[i].position = newPosition;

            if (lookAtCenter)
            {
                LookAtCenter(objects[i]);
            }
        }
    }

    void LookAtCenter(Transform obj)
    {
        Vector3 direction = orbitCenter.position - obj.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            obj.rotation = Quaternion.LookRotation(
                direction,
                Vector3.up
            );
        }
    }

    public void RestartOrbit()
    {
        isOrbiting = false;
        moveTimer = 0f;

        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].position = startPositions[i];
        }
    }
}