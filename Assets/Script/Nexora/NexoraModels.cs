using System;

// ---------------------------------------------------------------------------
// Request bodies. Field names must match the API exactly, and no extra fields
// are allowed, because the server rejects unknown fields with 400.
// ---------------------------------------------------------------------------

/// <summary>Body of POST /api/v1/vr/auth/login.</summary>
[Serializable]
public class LoginRequest
{
    public string email;          // Learner's website email.
    public string password;       // Learner's website password, sent exactly as typed.
    public string experimentUuid; // Shared UUID of the experiment being opened.
}

/// <summary>Body of POST /api/v1/vr/progress/add.</summary>
[Serializable]
public class ProgressRequest
{
    public string experimentUuid;  // Shared UUID of the experiment.
    public float progress;         // Percentage reached, 0 to 100.
    public float timeSpentMinutes; // Minutes since the last SUCCESSFUL report, 0 to 1440.
}

// ---------------------------------------------------------------------------
// Response payloads.
// ---------------------------------------------------------------------------

/// <summary>Signed-in learner as returned by login.</summary>
[Serializable]
public class ApiUser
{
    public int id;           // Learner's database id.
    public string email;     // Learner's email.
    public string fullName;  // Learner's display name.
    public string avatarUrl; // Picture URL, empty when the learner has none.
}

/// <summary>"result" object of a successful login.</summary>
[Serializable]
public class LoginResult
{
    public string accessToken;          // Token for every other VR request.
    public string tokenType;            // Always "Bearer".
    public string accessTokenExpiresAt; // ISO 8601 UTC expiry time.
    public ApiUser user;                // The signed-in learner.
}

/// <summary>"result" object of a successful progress report (stored totals, not sent values).</summary>
[Serializable]
public class ProgressResult
{
    public string experimentUuid;  // Experiment the totals belong to.
    public float progress;         // Highest percentage reached so far.
    public float timeSpentMinutes; // Total minutes across every report.
    public string createdAt;       // Time of the first report.
    public string updatedAt;       // Time of the latest report.
}

/// <summary>One invalid field in a 400 response (constraint details are skipped, JsonUtility cannot read dictionaries).</summary>
[Serializable]
public class ValidationError
{
    public string property; // Name of the field that failed validation.
}

/// <summary>Outer envelope shared by every response.</summary>
[Serializable]
public class ApiEnvelope
{
    public bool success;              // True on success.
    public int code;                  // Always equals the HTTP status.
    public string errorMessage;       // Learner-facing message on failure.
    public ValidationError[] errors;  // Present only on 400 validation failures.
}

/// <summary>Full login response.</summary>
[Serializable]
public class LoginResponse : ApiEnvelope
{
    public LoginResult result; // Present only on success.
}

/// <summary>Full progress response.</summary>
[Serializable]
public class ProgressResponse : ApiEnvelope
{
    public ProgressResult result; // Present only on success.
}

// ---------------------------------------------------------------------------
// Result handed back to game code after any call.
// ---------------------------------------------------------------------------

/// <summary>Outcome of one API call, already unpacked for game code.</summary>
public class ApiCallResult<T>
{
    public bool IsSuccess;         // True only for HTTP 200 with success = true.
    public long StatusCode;        // HTTP status; 0 when the server could not be reached.
    public string ErrorMessage;    // Server message, or a local message for network errors.
    public string[] InvalidFields; // Field names from a 400 response.
    public T Data;                 // Parsed "result" on success.

    // True when the request never got an HTTP answer (offline, DNS, timeout).
    public bool IsNetworkError => StatusCode == 0;
}
