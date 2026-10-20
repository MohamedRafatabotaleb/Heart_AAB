using System;
using UnityEngine;

/// <summary>
/// Tracks time spent inside the experiment and reports progress to the server
/// every few seconds, following the API's recommended reporting pattern.
/// Put it on the same GameObject as NexoraApiClient.
/// </summary>
[DisallowMultipleComponent]
public class NexoraProgressReporter : MonoBehaviour
{
    [Header("State (read-only at runtime)")]
    [Tooltip("Current progress percentage that will be sent with the next report.")]
    [Range(0f, 100f)]
    [SerializeField] private float _currentProgress;

    [Tooltip("Minutes counted since the last successful report.")]
    [SerializeField] private float _unsentMinutes;

    private float _secondsUntilNextReport;
    private bool _isRunning;
    private bool _isRequestInFlight;
    private bool _isBlocked;
    private bool _hasSucceededWithCurrentToken;

    // Current progress percentage.
    public float CurrentProgress => _currentProgress;

    // True while the learner is inside the experiment and time is being counted.
    public bool IsRunning => _isRunning;

    // True after the server refused the subscription; reporting stays off until the next login.
    public bool IsBlocked => _isBlocked;

    // Raised with the stored totals after every successful report.
    public event Action<ProgressResult> OnReportSucceeded;

    // Raised when the learner must sign in again (expired or rejected token).
    public event Action<string> OnSessionInvalid;

    // Raised when the subscription to this experiment has ended; keep the learner out.
    public event Action<string> OnSubscriptionEnded;

    // Raised for any other failure (bad request, wrong UUID, server or network error).
    public event Action<ApiCallResult<ProgressResult>> OnReportFailed;

    private void OnEnable()
    {
        if (NexoraApiClient.Instance != null)
            NexoraApiClient.Instance.OnLoggedIn += HandleLoggedIn;
    }

    private void Start()
    {
        // Covers the case where this component enabled before the client's Awake ran.
        if (NexoraApiClient.Instance != null)
        {
            NexoraApiClient.Instance.OnLoggedIn -= HandleLoggedIn;
            NexoraApiClient.Instance.OnLoggedIn += HandleLoggedIn;
        }
    }

    private void Update()
    {
        if (!_isRunning)
            return;

        // Unscaled time so pausing the game with timeScale = 0 still counts real minutes.
        float delta = Time.unscaledDeltaTime;
        _unsentMinutes += delta / 60f;
        _secondsUntilNextReport -= delta;

        if (_secondsUntilNextReport <= 0f)
        {
            _secondsUntilNextReport = NexoraApiClient.Instance.Config.ReportIntervalSeconds;
            SendNow();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        // Headset removed or app sent to background: try to flush what we have.
        if (isPaused && _isRunning)
            SendNow();
    }

    private void OnDisable()
    {
        if (NexoraApiClient.Instance != null)
            NexoraApiClient.Instance.OnLoggedIn -= HandleLoggedIn;
    }

    /// <summary>Call when the learner enters the experiment; starts counting time.</summary>
    public void StartExperiment()
    {
        if (_isBlocked)
        {
            Debug.LogWarning("[Nexora] Reporting is blocked: subscription ended. Sign in again first.");
            return;
        }

        if (NexoraApiClient.Instance == null || !NexoraApiClient.Instance.IsLoggedIn)
        {
            OnSessionInvalid?.Invoke("Please sign in first.");
            return;
        }

        _isRunning = true;
        _secondsUntilNextReport = NexoraApiClient.Instance.Config.ReportIntervalSeconds;
    }

    /// <summary>Call when the learner pauses; sends a report and stops counting time.</summary>
    public void PauseExperiment()
    {
        if (!_isRunning)
            return;

        SendNow();
        _isRunning = false;
    }

    /// <summary>Call when the learner finishes or exits; sends the final report and stops counting.</summary>
    public void StopExperiment()
    {
        SendNow();
        _isRunning = false;
    }

    /// <summary>Sets the progress percentage (0-100) from your experiment logic.</summary>
    public void SetProgress(float percent)
    {
        _currentProgress = Mathf.Clamp(percent, 0f, 100f);
    }

    /// <summary>Sends a report immediately (e.g. from a button), using the minutes counted so far.</summary>
    public void SendNow()
    {
        if (_isRequestInFlight || _isBlocked)
            return;

        var api = NexoraApiClient.Instance;
        if (api == null)
        {
            Debug.LogError("[Nexora] NexoraApiClient is missing from the scene.");
            return;
        }

        // Snapshot what we send, so minutes that keep accruing during the request are not lost.
        float minutesToSend = Mathf.Min(_unsentMinutes, 1440f);
        _isRequestInFlight = true;

        api.ReportProgress(_currentProgress, minutesToSend, result =>
        {
            _isRequestInFlight = false;

            if (result.IsSuccess)
            {
                // Subtract only what the server accepted; anything counted meanwhile stays for next time.
                _unsentMinutes = Mathf.Max(0f, _unsentMinutes - minutesToSend);
                _hasSucceededWithCurrentToken = true;
                OnReportSucceeded?.Invoke(result.Data);
                return;
            }

            HandleFailure(result);
        });
    }

    // Reacts to a failed report according to the API's error table.
    private void HandleFailure(ApiCallResult<ProgressResult> result)
    {
        switch (result.StatusCode)
        {
            case 401:
                // No, malformed or expired token: back to sign-in. Unsent minutes are kept.
                _isRunning = false;
                OnSessionInvalid?.Invoke(result.ErrorMessage);
                break;

            case 403:
                if (_hasSucceededWithCurrentToken)
                {
                    // Same token worked before, so the subscription lapsed mid-session.
                    _isRunning = false;
                    _isBlocked = true;
                    _unsentMinutes = 0f;
                    OnSubscriptionEnded?.Invoke(result.ErrorMessage);
                }
                else
                {
                    // Ambiguous on the first report: send to sign-in, which tells the two causes apart.
                    _isRunning = false;
                    NexoraApiClient.Instance.Logout();
                    OnSessionInvalid?.Invoke(result.ErrorMessage);
                }
                break;

            case 400:
                // The app built a bad request; retrying unchanged would fail again.
                _isRunning = false;
                Debug.LogError("[Nexora] Invalid progress request. Fields: " +
                               (result.InvalidFields != null ? string.Join(", ", result.InvalidFields) : "unknown"));
                OnReportFailed?.Invoke(result);
                break;

            case 404:
                // Wrong or missing experiment UUID on the server.
                _isRunning = false;
                Debug.LogError("[Nexora] Experiment UUID not found on the server: " +
                               NexoraApiClient.Instance.Config.ExperimentUuid);
                OnReportFailed?.Invoke(result);
                break;

            default:
                // 500 or network error: keep the minutes and retry on the next interval.
                Debug.LogWarning($"[Nexora] Report failed ({result.StatusCode}), will retry: {result.ErrorMessage}");
                OnReportFailed?.Invoke(result);
                break;
        }
    }

    // A fresh login resets the token-dependent flags.
    private void HandleLoggedIn(ApiUser user)
    {
        _isBlocked = false;
        _hasSucceededWithCurrentToken = false;
    }
}
