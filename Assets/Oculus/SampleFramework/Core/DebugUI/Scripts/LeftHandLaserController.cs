using UnityEngine;
using System.Collections;

public class LeftHandLaserOnly : MonoBehaviour
{
    [Tooltip("مرجع لعنصر الليزر")]
    public GameObject laserPointer;

    private Transform leftHandAnchor;
    private Transform rightHandAnchor;
    private bool isInitialized = false;

    void Start()
    {
        // البحث عن مراجع اليدين والليزر
        FindReferences();

        // تطبيق الحل بعد إطار واحد للتأكد من تهيئة جميع المكونات
        StartCoroutine(ApplyFixAfterDelay());
    }

    void FindReferences()
    {
        // العثور على OVRCameraRig
        OVRCameraRig cameraRig = FindObjectOfType<OVRCameraRig>();
        if (cameraRig != null)
        {
            leftHandAnchor = cameraRig.transform.Find("TrackingSpace/LeftHandAnchor");
            rightHandAnchor = cameraRig.transform.Find("TrackingSpace/RightHandAnchor");
            Debug.Log("تم العثور على مراجع اليدين: " +
                      (leftHandAnchor != null ? "اليسرى ✓" : "اليسرى ✗") + ", " +
                      (rightHandAnchor != null ? "اليمنى ✓" : "اليمنى ✗"));
        }

        // العثور على الليزر إذا لم يتم تعيينه
        if (laserPointer == null)
        {
            Transform uiHelpers = GameObject.Find("UIHelpers")?.transform;
            if (uiHelpers != null)
            {
                laserPointer = uiHelpers.Find("LaserPointer")?.gameObject;
            }

            if (laserPointer == null)
            {
                laserPointer = GameObject.Find("LaserPointer");
            }
        }

        isInitialized = (leftHandAnchor != null && rightHandAnchor != null);
    }

    IEnumerator ApplyFixAfterDelay()
    {
        // انتظار إطار واحد
        yield return new WaitForEndOfFrame();

        // تعطيل أي ليزر في اليد اليمنى
        DisableRightHandLaser();

        // تكوين OVRInputModule للعمل مع اليد اليسرى فقط
        ConfigureInputModules();

        // التحقق مرة أخرى بعد قليل
        yield return new WaitForSeconds(0.5f);
        DisableRightHandLaser();

        // استمر في التحقق كل ثانية
        StartCoroutine(ContinuousCheck());
    }

    IEnumerator ContinuousCheck()
    {
        while (true)
        {
            DisableRightHandLaser();
            yield return new WaitForSeconds(0.2f);
        }
    }

    void DisableRightHandLaser()
    {
        if (!isInitialized || rightHandAnchor == null) return;

        // تعطيل أي LineRenderer في اليد اليمنى
        LineRenderer[] rightHandLines = rightHandAnchor.GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer line in rightHandLines)
        {
            if (line != null && line.enabled)
            {
                line.enabled = false;
                Debug.Log("تم تعطيل LineRenderer في اليد اليمنى");
            }
        }

        // تعطيل أي Renderer في اليد اليمنى يحتوي اسمه على Laser أو Pointer
        Renderer[] rightHandRenderers = rightHandAnchor.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in rightHandRenderers)
        {
            if (renderer != null && renderer.enabled &&
                (renderer.gameObject.name.Contains("Laser") ||
                 renderer.gameObject.name.Contains("Pointer")))
            {
                renderer.enabled = false;
                Debug.Log("تم تعطيل Renderer في اليد اليمنى: " + renderer.gameObject.name);
            }
        }

        // البحث عن أي ليزر مرتبط باليد اليمنى وتعطيله
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Laser") || obj.name.Contains("Pointer"))
            {
                // تحقق مما إذا كان هذا الكائن مرتبطًا باليد اليمنى
                Transform parent = obj.transform.parent;
                bool isRightHandChild = false;

                while (parent != null)
                {
                    if (parent == rightHandAnchor)
                    {
                        isRightHandChild = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (isRightHandChild)
                {
                    // تعطيل جميع Renderers في هذا الكائن
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer r in renderers)
                    {
                        if (r.enabled)
                        {
                            r.enabled = false;
                        }
                    }

                    // تعطيل جميع LineRenderers في هذا الكائن
                    LineRenderer[] lines = obj.GetComponentsInChildren<LineRenderer>(true);
                    foreach (LineRenderer l in lines)
                    {
                        if (l.enabled)
                        {
                            l.enabled = false;
                        }
                    }
                }
            }
        }
    }

    void ConfigureInputModules()
    {
        if (!isInitialized) return;

        // العثور على جميع وحدات OVRInputModule
        UnityEngine.EventSystems.OVRInputModule[] inputModules =
            FindObjectsOfType<UnityEngine.EventSystems.OVRInputModule>();

        foreach (var module in inputModules)
        {
            if (module != null && leftHandAnchor != null)
            {
                // ضبط Ray Transform على اليد اليسرى
                module.rayTransform = leftHandAnchor;
                Debug.Log("تم تكوين OVRInputModule للعمل مع اليد اليسرى فقط");
            }
        }

        // العثور على EventSystem وتكوينه
        UnityEngine.EventSystems.EventSystem eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem != null)
        {
            // تأكد من أن OVRInputModule هو المستخدم
            UnityEngine.EventSystems.BaseInputModule[] modules = eventSystem.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
            foreach (var module in modules)
            {
                if (!(module is UnityEngine.EventSystems.OVRInputModule))
                {
                    module.enabled = false;
                    Debug.Log("تم تعطيل وحدة إدخال غير OVR: " + module.GetType().Name);
                }
            }
        }
    }
}
