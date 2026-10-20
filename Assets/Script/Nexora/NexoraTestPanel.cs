using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Simple test screen: sign in, start/stop the experiment, move a slider for progress,
/// and send a report with a button. Put it on the Canvas.
/// </summary>
public class NexoraTestPanel : MonoBehaviour
{
    [Header("Login")]
    [Tooltip("Email input field.")]
    [SerializeField] private InputField _emailInput;

    [Tooltip("Password input field (switched to password mode automatically).")]
    [SerializeField] private InputField _passwordInput;

    [Tooltip("Button that sends the login request.")]
    [SerializeField] private Button _loginButton;

    [Tooltip("Button that clears the local session.")]
    [SerializeField] private Button _logoutButton;

    [Header("Progress")]
    [Tooltip("Reporter on the NexoraManager object.")]
    [SerializeField] private NexoraProgressReporter _reporter;

    [Tooltip("Slider from 0 to 100 that sets the progress percentage.")]
    [SerializeField] private Slider _progressSlider;

    [Tooltip("Starts counting time inside the experiment.")]
    [SerializeField] private Button _startButton;

    [Tooltip("Sends a progress report right now.")]
    [SerializeField] private Button _sendNowButton;

    [Tooltip("Sends the final report and stops counting.")]
    [SerializeField] private Button _stopButton;

    [Header("Output")]
    [Tooltip("Text that shows the last result or error.")]
    [SerializeField] private Text _statusText;

    [Tooltip("Status text color for normal messages.")]
    [SerializeField] private Color _infoColor = Color.white;

    [Tooltip("Status text color for errors (wrong login, server or network failure).")]
    [SerializeField] private Color _errorColor = new Color(1f, 0.35f, 0.35f);

    [Header("After Login")]
    [Tooltip("Scene opened after a successful login. Leave empty to stay on this screen.")]
    [SerializeField] private string _sceneAfterLogin = "Heart_Scene";

    private void Awake()
    {
        if (_passwordInput != null)
        {
            _passwordInput.contentType = InputField.ContentType.Password;
            _passwordInput.ForceLabelUpdate();
        }

        if (_progressSlider != null)
        {
            _progressSlider.minValue = 0f;
            _progressSlider.maxValue = 100f;
        }
    }

    private void OnEnable()
    {
        // Coming back to this scene: the scene's own manager copy is destroyed, so use the surviving one.
        if (NexoraApiClient.Instance != null)
        {
            var survivingReporter = NexoraApiClient.Instance.GetComponent<NexoraProgressReporter>();
            if (survivingReporter != null)
                _reporter = survivingReporter;
        }

        _loginButton.onClick.AddListener(HandleLoginClicked);
        _logoutButton.onClick.AddListener(HandleLogoutClicked);
        _startButton.onClick.AddListener(HandleStartClicked);
        _sendNowButton.onClick.AddListener(HandleSendNowClicked);
        _stopButton.onClick.AddListener(HandleStopClicked);
        _progressSlider.onValueChanged.AddListener(HandleSliderChanged);

        _reporter.OnReportSucceeded += HandleReportSucceeded;
        _reporter.OnSessionInvalid += HandleSessionInvalid;
        _reporter.OnSubscriptionEnded += HandleSubscriptionEnded;
        _reporter.OnReportFailed += HandleReportFailed;
    }

    private void Start()
    {
        RefreshButtons();
        SetStatus("Enter email and password, then press Login.");
    }

    private void OnDisable()
    {
        _loginButton.onClick.RemoveListener(HandleLoginClicked);
        _logoutButton.onClick.RemoveListener(HandleLogoutClicked);
        _startButton.onClick.RemoveListener(HandleStartClicked);
        _sendNowButton.onClick.RemoveListener(HandleSendNowClicked);
        _stopButton.onClick.RemoveListener(HandleStopClicked);
        _progressSlider.onValueChanged.RemoveListener(HandleSliderChanged);

        _reporter.OnReportSucceeded -= HandleReportSucceeded;
        _reporter.OnSessionInvalid -= HandleSessionInvalid;
        _reporter.OnSubscriptionEnded -= HandleSubscriptionEnded;
        _reporter.OnReportFailed -= HandleReportFailed;
    }

    // Sends the login request with the typed credentials.
    private void HandleLoginClicked()
    {
        _loginButton.interactable = false; // Prevents double submits while waiting.
        SetStatus("Signing in...");

        NexoraApiClient.Instance.Login(_emailInput.text, _passwordInput.text, result =>
        {
            _loginButton.interactable = true;
            _passwordInput.text = string.Empty; // Don't keep the password on a shared headset.

            if (!result.IsSuccess)
            {
                // Wrong credentials or no access: stay here and show why.
                SetError($"Login failed ({result.StatusCode}):\n{result.ErrorMessage}");
                RefreshButtons();
                return;
            }

            SetStatus($"Signed in as {result.Data.user.fullName}\nToken expires (UTC): {NexoraApiClient.Instance.TokenExpiresAtUtc:yyyy-MM-dd HH:mm}");
            RefreshButtons();

            if (!string.IsNullOrEmpty(_sceneAfterLogin))
                SceneManager.LoadScene(_sceneAfterLogin);
        });
    }

    // Clears the local session and stops reporting.
    private void HandleLogoutClicked()
    {
        if (_reporter.IsRunning)
            _reporter.StopExperiment();

        NexoraApiClient.Instance.Logout();
        SetStatus("Signed out.");
        RefreshButtons();
    }

    // Starts the time counter.
    private void HandleStartClicked()
    {
        _reporter.SetProgress(_progressSlider.value);
        _reporter.StartExperiment();
        SetStatus(_reporter.IsRunning ? "Experiment running. Reports every few seconds." : "Could not start.");
        RefreshButtons();
    }

    // Sends a report on demand.
    private void HandleSendNowClicked()
    {
        // Status first: a failure can call back synchronously and must not be overwritten.
        SetStatus("Sending progress...");
        _reporter.SetProgress(_progressSlider.value);
        _reporter.SendNow();
    }

    // Sends the final report and stops counting.
    private void HandleStopClicked()
    {
        SetStatus("Experiment stopped. Sending final report...");
        _reporter.SetProgress(_progressSlider.value);
        _reporter.StopExperiment();
        RefreshButtons();
    }

    // Pushes the slider value into the reporter.
    private void HandleSliderChanged(float value)
    {
        _reporter.SetProgress(value);
    }

    // Shows the totals the server stored.
    private void HandleReportSucceeded(ProgressResult stored)
    {
        SetStatus($"Saved on server.\nProgress: {stored.progress}%\nTotal minutes: {stored.timeSpentMinutes}\nUpdated: {stored.updatedAt}");
    }

    // Token rejected or expired: ask to sign in again.
    private void HandleSessionInvalid(string message)
    {
        SetError($"Please sign in again.\n{message}");
        RefreshButtons();
    }

    // Subscription ended mid-session: keep the learner out.
    private void HandleSubscriptionEnded(string message)
    {
        SetError($"Access ended.\n{message}");
        RefreshButtons();
    }

    // Any other failure.
    private void HandleReportFailed(ApiCallResult<ProgressResult> result)
    {
        SetError($"Report failed ({result.StatusCode}):\n{result.ErrorMessage}");
        RefreshButtons();
    }

    // Enables only the buttons that make sense right now.
    private void RefreshButtons()
    {
        bool loggedIn = NexoraApiClient.Instance != null && NexoraApiClient.Instance.IsLoggedIn;

        _logoutButton.interactable = loggedIn;
        _startButton.interactable = loggedIn && !_reporter.IsRunning && !_reporter.IsBlocked;
        _sendNowButton.interactable = loggedIn && !_reporter.IsBlocked;
        _stopButton.interactable = loggedIn && _reporter.IsRunning;
    }

    // Writes to the status text and the Console.
    private void SetStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.color = _infoColor;
            _statusText.text = message;
        }

        Debug.Log("[Nexora UI] " + message);
    }

    // Writes an error in the error color and to the Console.
    private void SetError(string message)
    {
        if (_statusText != null)
        {
            _statusText.color = _errorColor;
            _statusText.text = message;
        }

        Debug.LogWarning("[Nexora UI] " + message);
    }
}
