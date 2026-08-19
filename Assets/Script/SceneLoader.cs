using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomSceneLoader : MonoBehaviour
{
    // The name of the scene to load, set via the inspector
    public string sceneName;

    // Loads the scene specified in the sceneName variable
    public void LoadTargetScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}