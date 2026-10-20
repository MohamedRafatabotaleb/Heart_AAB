using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps a protected scene (the heart) closed to anyone who has not signed in.
/// Sends the learner back to the login scene when there is no valid session,
/// and starts/stops progress reporting while the scene is open.
/// Put one on any GameObject in the protected scene.
/// </summary>
[DisallowMultipleComponent]
public class NexoraSceneGate : MonoBehaviour
{
    [Tooltip("Scene that holds the login screen.")]
    [SerializeField] private string _loginSceneName = "Nexora_Login";

    [Tooltip("Start counting time and reporting progress when the scene opens.")]
    [SerializeField] private bool _reportProgress = true;

    private NexoraProgressReporter _reporter;

    private void Awake()
    {
        var api = NexoraApiClient.Instance;
        if (api == null || !api.IsLoggedIn)
        {
            Debug.LogWarning("[Nexora] No valid login, returning to the login scene.");
            SceneManager.LoadScene(_loginSceneName);
            return;
        }

        _reporter = api.GetComponent<NexoraProgressReporter>();
    }

    private void Start()
    {
        if (_reporter == null)
            return;

        _reporter.OnSessionInvalid += HandleAccessLost;
        _reporter.OnSubscriptionEnded += HandleAccessLost;

        if (_reportProgress)
            _reporter.StartExperiment();
    }

    private void OnDestroy()
    {
        if (_reporter == null)
            return;

        _reporter.OnSessionInvalid -= HandleAccessLost;
        _reporter.OnSubscriptionEnded -= HandleAccessLost;

        // Leaving the scene: send the final report and stop counting.
        if (_reporter.IsRunning)
            _reporter.StopExperiment();
    }

    /// <summary>Sets the progress percentage (0-100); call it from the heart scene's own logic.</summary>
    public void SetProgress(float percent)
    {
        if (_reporter != null)
            _reporter.SetProgress(percent);
    }

    // Token expired or subscription ended mid-session: back to the login screen.
    private void HandleAccessLost(string message)
    {
        Debug.LogWarning("[Nexora] Access lost: " + message);
        SceneManager.LoadScene(_loginSceneName);
    }
}
