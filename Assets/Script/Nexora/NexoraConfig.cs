using UnityEngine;

/// <summary>
/// Language the server uses for errorMessage (sent as the Accept-Language header).
/// </summary>
public enum NexoraLanguage
{
    en,
    ar
}

/// <summary>
/// Shared settings for talking to the Nexora VR API. Create one asset per experiment.
/// </summary>
[CreateAssetMenu(fileName = "NexoraConfig", menuName = "Nexora/API Config")]
public class NexoraConfig : ScriptableObject
{
    [Header("Server")]
    [Tooltip("Server root URL, without a trailing slash.")]
    [SerializeField] private string _baseUrl = "https://nexoralms.duckdns.org";

    [Tooltip("Seconds before a request is abandoned.")]
    [SerializeField] private int _timeoutSeconds = 15;

    [Tooltip("Language of the server's error messages.")]
    [SerializeField] private NexoraLanguage _language = NexoraLanguage.en;

    [Header("Experiment")]
    [Tooltip("Shared UUID of this experiment. Must match the value the admin set on the server.")]
    [SerializeField] private string _experimentUuid = "f8e78af2-4c80-4f02-9a20-aacb0ae8c230";

    [Header("Progress Reporting")]
    [Tooltip("Seconds between automatic progress reports (the API recommends 30-60).")]
    [Range(30f, 60f)]
    [SerializeField] private float _reportIntervalSeconds = 30f;

    // Server root URL with any trailing slash removed.
    public string BaseUrl => _baseUrl.TrimEnd('/');

    // Request timeout in seconds.
    public int TimeoutSeconds => _timeoutSeconds;

    // Value sent in the Accept-Language header ("en" or "ar").
    public string LanguageCode => _language.ToString();

    // Shared experiment UUID, trimmed of accidental spaces.
    public string ExperimentUuid => _experimentUuid.Trim();

    // Seconds between automatic progress reports.
    public float ReportIntervalSeconds => _reportIntervalSeconds;

    // Full URL of the login endpoint.
    public string LoginUrl => BaseUrl + "/api/v1/vr/auth/login";

    // Full URL of the progress endpoint.
    public string ProgressUrl => BaseUrl + "/api/v1/vr/progress/add";
}
