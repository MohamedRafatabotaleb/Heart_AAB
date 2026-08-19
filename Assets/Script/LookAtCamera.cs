using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (targetCamera == null)
            return;

        // يخلي الـ UI مواجه للكاميرا
        transform.LookAt(
            transform.position + targetCamera.transform.rotation * Vector3.forward,
            targetCamera.transform.rotation * Vector3.up
        );
    }
}