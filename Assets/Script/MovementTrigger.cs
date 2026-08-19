using UnityEngine;
using System.Collections.Generic;

public class MovementTrigger : MonoBehaviour
{
    [System.Serializable]
    public class TargetSettings
    {
        [Header("Target Object")]
        public Transform targetObject;

        [Header("Movement Distance")]
        public float movementDistance = 10f;

        [Header("Which Axis?")]
        public bool checkX = true;
        public bool checkY = true;
        public bool checkZ = true;

        [Header("Objects To Hide")]
        public List<GameObject> objectsToHide = new List<GameObject>();

        [Header("Objects To Show")]
        public List<GameObject> objectsToShow = new List<GameObject>();

        [HideInInspector]
        public Vector3 startPosition;

        [HideInInspector]
        public bool isTriggered;
    }

    [Header("Targets")]
    public List<TargetSettings> targets = new List<TargetSettings>();

    void Start()
    {
        foreach (TargetSettings target in targets)
        {
            if (target.targetObject != null)
            {
                target.startPosition = target.targetObject.position;
                target.isTriggered = false;

                SetObjectsState(target, false);
            }
        }
    }

    void Update()
    {
        foreach (TargetSettings target in targets)
        {
            if (target.targetObject == null)
                continue;

            Vector3 movement =
                target.targetObject.position - target.startPosition;

            bool reachedDistance = false;

            // Check X axis
            if (target.checkX &&
                Mathf.Abs(movement.x) >= target.movementDistance)
            {
                reachedDistance = true;
            }

            // Check Y axis
            if (target.checkY &&
                Mathf.Abs(movement.y) >= target.movementDistance)
            {
                reachedDistance = true;
            }

            // Check Z axis
            if (target.checkZ &&
                Mathf.Abs(movement.z) >= target.movementDistance)
            {
                reachedDistance = true;
            }

            // Trigger when the target reaches the required distance
            if (reachedDistance && !target.isTriggered)
            {
                target.isTriggered = true;
                SetObjectsState(target, true);
            }

            // Reset when the target returns below the required distance
            else if (!reachedDistance && target.isTriggered)
            {
                target.isTriggered = false;
                SetObjectsState(target, false);
            }
        }
    }

    void SetObjectsState(TargetSettings target, bool triggered)
    {
        if (triggered)
        {
            // Hide the first group
            foreach (GameObject obj in target.objectsToHide)
            {
                if (obj != null)
                    obj.SetActive(false);
            }

            // Show the second group
            foreach (GameObject obj in target.objectsToShow)
            {
                if (obj != null)
                    obj.SetActive(true);
            }
        }
        else
        {
            // Show the first group
            foreach (GameObject obj in target.objectsToHide)
            {
                if (obj != null)
                    obj.SetActive(true);
            }

            // Hide the second group
            foreach (GameObject obj in target.objectsToShow)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
    }
}