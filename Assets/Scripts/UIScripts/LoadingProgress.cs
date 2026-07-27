using System;

/// <summary>
/// Static, UI-agnostic progress reporter for long-running async flows
/// (create/join lobby, relay allocation, NetworkManager start, etc.).
///
/// This does NOT talk to any UI directly. Any async method (LobbyCanvasFunction,
/// OpenLobbyFunctions, etc.) just calls LoadingProgress.SetStep(...) as it completes
/// each known step. Any UI (LoadingScreenUI) subscribes to OnProgressChanged and
/// decides how to render it (tween a fill bar, update text, etc.).
///
/// IMPORTANT: this reports STEP-WEIGHTED progress, not true byte/sub-task progress —
/// none of the underlying Unity Services / Netcode calls (CreateLobbyAsync,
/// CreateAllocationAsync, StartHost, etc.) expose granular progress callbacks.
/// Each call to SetStep represents "step N of M known steps has just completed".
/// </summary>
public static class LoadingProgress
{
    public struct ProgressInfo
    {
        public int CurrentStep;
        public int TotalSteps;
        public string Label;
        public float Percent01; // 0..1
    }

    public static event Action<ProgressInfo> OnProgressChanged;

    /// <summary>Fired when a flow starts — lets the UI show itself with a fresh bar at 0%.</summary>
    public static event Action<string> OnLoadingStarted;

    /// <summary>Fired when a flow finishes successfully — lets the UI hide itself.</summary>
    public static event Action OnLoadingFinished;

    /// <summary>Fired when a flow fails — lets the UI show an error state / hide itself.</summary>
    public static event Action<string> OnLoadingFailed;

    public static ProgressInfo Current { get; private set; }

    /// <summary>
    /// Call at the very start of a flow (e.g. "Creating lobby...") before the first
    /// await. Resets progress to 0% under the given total step count.
    /// </summary>
    public static void StartFlow(string startLabel, int totalSteps)
    {
        Current = new ProgressInfo
        {
            CurrentStep = 0,
            TotalSteps = Math.Max(1, totalSteps),
            Label = startLabel,
            Percent01 = 0f
        };
        OnLoadingStarted?.Invoke(startLabel);
        OnProgressChanged?.Invoke(Current);
    }

    /// <summary>
    /// Call after each discrete async step completes.
    /// currentStep should be 1-based (e.g. 1 of 6, 2 of 6, ...).
    /// </summary>
    public static void SetStep(int currentStep, int totalSteps, string label)
    {
        totalSteps = Math.Max(1, totalSteps);
        currentStep = Math.Clamp(currentStep, 0, totalSteps);

        Current = new ProgressInfo
        {
            CurrentStep = currentStep,
            TotalSteps = totalSteps,
            Label = label,
            Percent01 = (float)currentStep / totalSteps
        };
        OnProgressChanged?.Invoke(Current);
    }

    /// <summary>Call when the whole flow completes successfully (snaps to 100% then hides).</summary>
    public static void FinishFlow()
    {
        Current = new ProgressInfo
        {
            CurrentStep = Current.TotalSteps,
            TotalSteps = Current.TotalSteps,
            Label = "Done",
            Percent01 = 1f
        };
        OnProgressChanged?.Invoke(Current);
        OnLoadingFinished?.Invoke();
    }

    /// <summary>Call when the flow fails partway through (e.g. relay creation failed).</summary>
    public static void FailFlow(string errorMessage)
    {
        OnLoadingFailed?.Invoke(errorMessage);
    }
}