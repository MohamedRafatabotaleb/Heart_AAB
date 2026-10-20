using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends requests to the Nexora VR API and keeps the current learner's session in memory.
/// Put one on a persistent GameObject in the first scene.
/// </summary>
[DisallowMultipleComponent]
public class NexoraApiClient : MonoBehaviour
{
    // Fallback token lifetime used if the server's expiry date cannot be parsed.
    private const double DefaultTokenHours = 12;

    public static NexoraApiClient Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Config asset holding the base URL and experiment UUID.")]
    [SerializeField] private NexoraConfig _config;

    [Header("Debug")]
    [Tooltip("Log every request and response to the Console (never logs the password).")]
    [SerializeField] private bool _logRequests = true;

    // Token is kept in memory only, on purpose: headsets are shared, so nothing is saved to disk.
    private string _accessToken;
    private DateTime _tokenExpiresAtUtc;
    private ApiUser _currentUser;

    // Config asset in use.
    public NexoraConfig Config => _config;

    // Learner signed in right now, or null.
    public ApiUser CurrentUser => _currentUser;

    // Expiry of the current token in UTC.
    public DateTime TokenExpiresAtUtc => _tokenExpiresAtUtc;

    // True while a token exists and has not expired yet.
    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiresAtUtc;

    // Raised after a successful login.
    public event Action<ApiUser> OnLoggedIn;

    // Raised when the session is cleared (logout, expiry, or rejected token).
    public event Action OnLoggedOut;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (_config == null)
            Debug.LogError("[Nexora] NexoraConfig is not assigned on NexoraApiClient.", this);
    }

    /// <summary>Signs the learner in to the experiment set in the config.</summary>
    public void Login(string email, string password, Action<ApiCallResult<LoginResult>> onDone)
    {
        StartCoroutine(LoginRoutine(email, password, onDone));
    }

    /// <summary>Sends one progress report for the experiment set in the config.</summary>
    public void ReportProgress(float progress, float timeSpentMinutes, Action<ApiCallResult<ProgressResult>> onDone)
    {
        StartCoroutine(ReportProgressRoutine(progress, timeSpentMinutes, onDone));
    }

    /// <summary>Forgets the token and the learner (there is no logout endpoint, so this is local only).</summary>
    public void Logout()
    {
        bool wasLoggedIn = !string.IsNullOrEmpty(_accessToken);
        _accessToken = null;
        _currentUser = null;
        _tokenExpiresAtUtc = DateTime.MinValue;

        if (wasLoggedIn)
            OnLoggedOut?.Invoke();
    }

    // Builds the login body, sends it, and stores the token on success.
    private IEnumerator LoginRoutine(string email, string password, Action<ApiCallResult<LoginResult>> onDone)
    {
        var body = new LoginRequest
        {
            email = (email ?? string.Empty).Trim(),
            password = password ?? string.Empty, // Not trimmed: the API keeps spaces in passwords.
            experimentUuid = _config.ExperimentUuid
        };

        ApiCallResult<LoginResult> outcome = null;
        yield return SendPost<LoginResponse>(_config.LoginUrl, JsonUtility.ToJson(body), false, false,
            (status, response, networkError) =>
            {
                outcome = BuildResult(status, response, response != null ? response.result : null, networkError);
            });

        if (outcome.IsSuccess && outcome.Data != null)
            StoreSession(outcome.Data);

        onDone?.Invoke(outcome);
    }

    // Builds the progress body, sends it with the token, and clears the session if the token is rejected.
    private IEnumerator ReportProgressRoutine(float progress, float timeSpentMinutes, Action<ApiCallResult<ProgressResult>> onDone)
    {
        // Skip the network when we already know the token is expired; the server would answer 401 anyway.
        if (!IsLoggedIn)
        {
            Logout();
            onDone?.Invoke(new ApiCallResult<ProgressResult>
            {
                IsSuccess = false,
                StatusCode = 401,
                ErrorMessage = "Session expired. Please sign in again."
            });
            yield break;
        }

        var body = new ProgressRequest
        {
            experimentUuid = _config.ExperimentUuid,
            progress = Round2(Mathf.Clamp(progress, 0f, 100f)),
            timeSpentMinutes = Round2(Mathf.Clamp(timeSpentMinutes, 0f, 1440f))
        };

        ApiCallResult<ProgressResult> outcome = null;
        yield return SendPost<ProgressResponse>(_config.ProgressUrl, JsonUtility.ToJson(body), true, true,
            (status, response, networkError) =>
            {
                outcome = BuildResult(status, response, response != null ? response.result : null, networkError);
            });

        // 401 always means the token is no longer usable.
        if (outcome.StatusCode == 401)
            Logout();

        onDone?.Invoke(outcome);
    }

    // Sends one JSON POST and returns the HTTP status, the parsed envelope, and any network error.
    private IEnumerator SendPost<TResponse>(string url, string json, bool withAuth, bool logBody,
        Action<long, TResponse, string> onDone) where TResponse : ApiEnvelope
    {
        using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = _config.TimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept-Language", _config.LanguageCode);

            if (withAuth)
                request.SetRequestHeader("Authorization", "Bearer " + _accessToken);

            // The login body holds the password, so only the URL is logged for it.
            if (_logRequests)
                Debug.Log($"[Nexora] POST {url}" + (logBody ? $"\n{json}" : string.Empty));

            yield return request.SendWebRequest();

            string text = request.downloadHandler != null ? request.downloadHandler.text : null;
            long status = request.responseCode;

            // ConnectionError means no HTTP answer at all (offline, DNS failure, timeout).
            string networkError = request.result == UnityWebRequest.Result.ConnectionError ? request.error : null;
            if (networkError != null)
                status = 0;

            if (_logRequests)
                Debug.Log($"[Nexora] {status} {url}\n{text}");

            TResponse parsed = null;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    parsed = JsonUtility.FromJson<TResponse>(text);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Nexora] Could not parse response from {url}: {e.Message}");
                }
            }

            onDone?.Invoke(status, parsed, networkError);
        }
    }

    // Turns raw status + envelope into an ApiCallResult the game code can branch on.
    private static ApiCallResult<T> BuildResult<T>(long status, ApiEnvelope envelope, T data, string networkError)
    {
        var result = new ApiCallResult<T>
        {
            StatusCode = status,
            IsSuccess = status == 200 && envelope != null && envelope.success,
            Data = data
        };

        if (result.IsSuccess)
            return result;

        if (status == 0)
            result.ErrorMessage = "Cannot reach the server. Check the internet connection. (" + networkError + ")";
        else if (envelope != null && !string.IsNullOrEmpty(envelope.errorMessage))
            result.ErrorMessage = envelope.errorMessage;
        else
            result.ErrorMessage = "Unexpected server response (HTTP " + status + ").";

        if (envelope != null && envelope.errors != null)
        {
            result.InvalidFields = new string[envelope.errors.Length];
            for (int i = 0; i < envelope.errors.Length; i++)
                result.InvalidFields[i] = envelope.errors[i].property;
        }

        return result;
    }

    // Saves the token, expiry and learner from a successful login.
    private void StoreSession(LoginResult login)
    {
        _accessToken = login.accessToken;
        _currentUser = login.user;

        if (!DateTime.TryParse(login.accessTokenExpiresAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out _tokenExpiresAtUtc))
        {
            _tokenExpiresAtUtc = DateTime.UtcNow.AddHours(DefaultTokenHours);
        }

        OnLoggedIn?.Invoke(_currentUser);
    }

    // Rounds to 2 decimals, matching how the server stores the value.
    private static float Round2(float value)
    {
        return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
