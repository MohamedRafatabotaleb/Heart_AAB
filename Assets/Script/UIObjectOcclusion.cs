using System.Collections.Generic;
using UnityEngine;

public class UIObjectOcclusion : MonoBehaviour
{
    [System.Serializable]
    public class UIPair
    {
        [Header("UI Object")]
        public GameObject uiObject;

        [Header("3D Occluders")]
        public List<GameObject> occluderObjects = new List<GameObject>();
    }

    [Header("Camera")]
    public Camera targetCamera;

    [Header("UI / Occluder Settings")]
    public List<UIPair> pairs = new List<UIPair>();

    void LateUpdate()
    {
        if (targetCamera == null)
            return;

        foreach (UIPair pair in pairs)
        {
            if (pair == null || pair.uiObject == null)
                continue;

            CheckOcclusion(pair);
        }
    }

    void CheckOcclusion(UIPair pair)
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 uiPosition = pair.uiObject.transform.position;

        Vector3 direction = uiPosition - cameraPosition;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        direction.Normalize();

        RaycastHit[] hits = Physics.RaycastAll(
            cameraPosition,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        bool blocked = false;

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            foreach (GameObject occluder in pair.occluderObjects)
            {
                if (occluder == null)
                    continue;

                Transform occluderTransform = occluder.transform;

                if (hitTransform == occluderTransform ||
                    hitTransform.IsChildOf(occluderTransform))
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                break;
        }

        pair.uiObject.SetActive(!blocked);
    }
}