using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders LoadingProgress events as a smoothly-tweened fill bar + status text + %.
///
/// Setup:
///  - Put this on your loading screen root panel (the panel you show/hide).
///  - Assign fillImage (an Image with Image Type = Filled, Fill Method = Horizontal)
///    OR assign a Slider — whichever you're using. Both are optional; wire up what you have.
///  - Assign statusText (e.g. "Creating relay...") and percentText (e.g. "42%").
///
/// This script only reacts to LoadingProgress static events — it has no knowledge of
/// lobby/relay/netcode code, so it can be reused for host flow, join flow, or any
/// other multi-step async flow you wire up later.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root object to show while loading and hide when finished/failed.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Progress Bar (use ONE of these)")]
    [Tooltip("Image with Image Type = Filled (Horizontal/Radial). Leave empty if using a Slider instead.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Slider (0-1 range). Leave empty if using a filled Image instead.")]
    [SerializeField] private Slider fillSlider;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Tweening")]
    [Tooltip("How long it takes the bar to smoothly animate toward each new progress value.")]
    [SerializeField] private float tweenDuration = 0.35f;
    [SerializeField] private Ease tweenEase = Ease.OutCubic;
    [SerializeField] private float timeTakenPerDotForStatusText = 0.2f;
    

    [Header("Behaviour")]
    [Tooltip("Seconds to hold the panel at 100% before hiding, so the bar doesn't just vanish.")]
    [SerializeField] private float holdAtCompleteBeforeHide = 0.25f;
    [Tooltip("Seconds to show an error message before hiding (0 = don't auto-hide on error).")]
    [SerializeField] private float errorDisplayDuration = 2.5f;

    private float _displayedPercent01 = 0f;
    private Tween _fillTween;
    private Coroutine _progressDotsCoroutine;
    private void OnEnable()
    {
        LoadingProgress.OnLoadingStarted += HandleLoadingStarted;
        LoadingProgress.OnProgressChanged += HandleProgressChanged;
        LoadingProgress.OnLoadingFinished += HandleLoadingFinished;
        LoadingProgress.OnLoadingFailed += HandleLoadingFailed;
    }

    private void OnDisable()
    {
        LoadingProgress.OnLoadingStarted -= HandleLoadingStarted;
        LoadingProgress.OnProgressChanged -= HandleProgressChanged;
        LoadingProgress.OnLoadingFinished -= HandleLoadingFinished;
        LoadingProgress.OnLoadingFailed -= HandleLoadingFailed;

        _fillTween?.Kill();
        if(_progressDotsCoroutine!= null) StopCoroutine(_progressDotsCoroutine);
    }

    private void OnDestroy()
    {
        LoadingProgress.OnLoadingStarted -= HandleLoadingStarted;
        LoadingProgress.OnProgressChanged -= HandleProgressChanged;
        LoadingProgress.OnLoadingFinished -= HandleLoadingFinished;
        LoadingProgress.OnLoadingFailed -= HandleLoadingFailed;

        _fillTween?.Kill();
        if (_progressDotsCoroutine != null) StopCoroutine(_progressDotsCoroutine);
    }

    private void HandleLoadingStarted(string startLabel)
    {
        if (errorText != null) errorText.gameObject.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(true);

        _displayedPercent01 = 0f;
        ApplyFillImmediate(0f);

        if (statusText != null) statusText.text = startLabel;
        if (percentText != null) percentText.text = "0%";
        if(_progressDotsCoroutine != null) StopCoroutine(_progressDotsCoroutine);
        _progressDotsCoroutine = StartCoroutine(progressDotsRoutine());  
    }

    private void HandleProgressChanged(LoadingProgress.ProgressInfo info)
    {
        if (statusText != null) statusText.text = info.Label;

        _fillTween?.Kill();
        _fillTween = DOTween.To(
            () => _displayedPercent01,
            SetDisplayedPercent,
            info.Percent01,
            tweenDuration
        ).SetEase(tweenEase);
    }

    private void HandleLoadingFinished()
    {
        // Let the in-flight tween reach 100%, then hold briefly, reset, and hide.
        _fillTween?.Kill();
        _fillTween = DOTween.To(
            () => _displayedPercent01,
            SetDisplayedPercent,
            1f,
            tweenDuration
        ).SetEase(tweenEase).OnComplete(() =>
        {
            if (holdAtCompleteBeforeHide > 0f)
                DOVirtual.DelayedCall(holdAtCompleteBeforeHide, ResetAndHidePanel);
            else
                ResetAndHidePanel();
        });
        if (_progressDotsCoroutine != null) StopCoroutine(_progressDotsCoroutine);
    }

    private void HandleLoadingFailed(string errorMessage)
    {
        _fillTween?.Kill();

        if (errorText != null)
        {
            errorText.gameObject.SetActive(true);
            errorText.text = errorMessage;
        }
        if (statusText != null) statusText.text = "";

        if (errorDisplayDuration > 0f)
            DOVirtual.DelayedCall(errorDisplayDuration, ResetAndHidePanel);
        if (_progressDotsCoroutine != null) StopCoroutine(_progressDotsCoroutine);
    }

    /// <summary>
    /// Resets fill, percent text, status text, and error state back to a clean
    /// slate, then deactivates the panel. Runs after both success and failure so
    /// the panel never re-appears next time showing last run's leftover state.
    /// </summary>
    private void ResetAndHidePanel()
    {
        _fillTween?.Kill();

        _displayedPercent01 = 0f;
        ApplyFillImmediate(0f);

        if (statusText != null) statusText.text = string.Empty;
        if (errorText != null)
        {
            errorText.text = string.Empty;
            errorText.gameObject.SetActive(false);
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (_progressDotsCoroutine != null) StopCoroutine(_progressDotsCoroutine);
    }

    private void SetDisplayedPercent(float value)
    {
        _displayedPercent01 = value;
        ApplyFillImmediate(value);
    }

    private void ApplyFillImmediate(float value01)
    {
        if (fillImage != null) fillImage.fillAmount = value01;
        if (fillSlider != null) fillSlider.value = value01;
        if (percentText != null) percentText.text = $"{Mathf.RoundToInt(value01 * 100f)}%";
    }

    IEnumerator progressDotsRoutine()
    {
        yield return null;
        while (true)
        {
            if (statusText != null)
            {
                string baseText = LoadingProgress.Current.Label;
                int dotCount = Mathf.FloorToInt(Time.time % 3) + 1; // 1 to 3 dots
                statusText.text = baseText + new string('.', dotCount);
            }
            yield return new WaitForSeconds(timeTakenPerDotForStatusText);
        }
    }
}