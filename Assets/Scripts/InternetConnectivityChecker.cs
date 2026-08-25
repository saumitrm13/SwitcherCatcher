using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Checks for real internet connectivity at startup and then repeatedly every
/// few seconds. Application.internetReachability only tells you a network
/// interface is up (e.g. wifi is on), not that the internet is actually
/// reachable, so this does a lightweight HEAD-ish request to a reliable
/// endpoint to confirm real connectivity.
///
/// For now this only Debug.Logs the result (as requested). Hook OnInternetStatusChanged
/// later if you want to react to connectivity changes (e.g. pause matchmaking,
/// show a UI banner, etc).
/// </summary>
public class InternetConnectivityChecker : MonoBehaviour
{
    public static InternetConnectivityChecker Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("How often (seconds) to re-check connectivity after the initial check.")]
    [SerializeField] private float checkInterval = 5f;

    [Tooltip("Max time to wait for the connectivity request before treating it as a failure.")]
    [SerializeField] private float timeoutSeconds = 5f;

    [Tooltip("URL used to verify real internet access. Should be small, fast, and reliable.")]
    [SerializeField] private string pingUrl = "https://clients3.google.com/generate_204";

    [SerializeField] private GameObject InternetConnectionCanvas;

    // Start optimistic-but-cheap: seeded from Application.internetReachability
    // synchronously in Awake() (see below) rather than defaulting blindly to
    // true, so anything that checks IsInternetAvailable on the very first
    // frame (before the real web-request check has had a chance to run)
    // gets a reasonable first guess instead of a stale default.
    public bool IsInternetAvailable { get; private set; } = true;

    /// <summary>Fired whenever the connectivity state changes (true = now online, false = now offline).</summary>
    public static event Action<bool> OnInternetStatusChanged;

    private Coroutine _checkRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Cheap synchronous first guess so other scripts' Awake()/Start() calls
        // (which may run before our first coroutine-based check completes)
        // have something more accurate than a hardcoded "true" to go on.
        // The real web-request check in CheckLoop() will correct this shortly
        // after if the interface is up but the internet isn't actually reachable.
        IsInternetAvailable = Application.internetReachability != NetworkReachability.NotReachable;
    }

    private void OnEnable()
    {
        _checkRoutine = StartCoroutine(CheckLoop());
    }

    private void OnDisable()
    {
        if (_checkRoutine != null)
        {
            StopCoroutine(_checkRoutine);
            _checkRoutine = null;
        }
    }

    private IEnumerator CheckLoop()
    {
        // Initial check right at game start
        yield return StartCoroutine(CheckInternetOnce());

        var wait = new WaitForSecondsRealtime(checkInterval);
        while (true)
        {
            yield return wait;
            yield return StartCoroutine(CheckInternetOnce());
        }
    }

    private IEnumerator CheckInternetOnce()
    {
        // Quick early-out: if there's no network interface at all, don't bother
        // firing a web request.
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            HandleResult(false);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(pingUrl))
        {
            request.timeout = Mathf.CeilToInt(timeoutSeconds);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool success = request.result == UnityWebRequest.Result.Success;
#else
            bool success = !request.isNetworkError && !request.isHttpError;
#endif
            HandleResult(success);
        }
    }

    private void HandleResult(bool isOnline)
    {
        bool changed = isOnline != IsInternetAvailable;
        IsInternetAvailable = isOnline;

        if (isOnline)
        {
            Debug.Log("[InternetConnectivityChecker] Internet is available.");
            InternetConnectionCanvas.SetActive(false);
        }
        else
        {
            Debug.Log("[InternetConnectivityChecker] Internet is NOT available.");
            InternetConnectionCanvas.SetActive(true);
        }

        if (changed)
        {
            OnInternetStatusChanged?.Invoke(isOnline);
        }
    }

    /// <summary>Force an immediate check outside the normal polling interval.</summary>
    public void CheckNow()
    {
        StartCoroutine(CheckInternetOnce());
    }

    // ── Waiting / scheduling helpers ────────────────────────────────────────

    /// <summary>
    /// Coroutine helper: yields until the internet is confirmed available.
    /// Returns immediately if already online. Usage:
    ///   yield return InternetConnectivityChecker.Instance.WaitForInternet();
    /// </summary>
    public IEnumerator WaitForInternet()
    {
        if (IsInternetAvailable) yield break;

        bool online = false;
        Action<bool> handler = null;
        handler = (isOnline) =>
        {
            if (isOnline)
            {
                online = true;
                OnInternetStatusChanged -= handler;
            }
        };
        OnInternetStatusChanged += handler;

        // Also nudge an immediate check rather than waiting for the next poll tick.
        CheckNow();

        while (!online)
            yield return null;
    }

    /// <summary>
    /// Non-coroutine helper: runs the given Action once the internet becomes
    /// available. If already online, invokes immediately (synchronously).
    /// Useful for one-shot "do X once we're back online" scheduling, e.g.
    /// deferring a sign-in call made from Awake()/Start() on some other script.
    /// </summary>
    public void RunWhenOnline(Action action)
    {
        if (action == null) return;

        if (IsInternetAvailable)
        {
            action.Invoke();
            return;
        }

        Action<bool> handler = null;
        handler = (isOnline) =>
        {
            if (isOnline)
            {
                OnInternetStatusChanged -= handler;
                action.Invoke();
            }
        };
        OnInternetStatusChanged += handler;

        // Nudge an immediate check so we don't wait for the next poll interval.
        CheckNow();
    }
}