using TMPro;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Central toast notification system. Attach to a persistent UI object
/// (e.g. a Canvas child that stays loaded across the whole scene / game).
///
/// Usage from anywhere (client-side only — this is local UI):
///     ToastScript.Toast("Great!... You own a Pole");
///
/// If a toast is already showing when a new one comes in, the current
/// animation is killed immediately and the toast routine restarts fresh
/// with the new message (no queueing — newest message always wins).
/// This mirrors how you already fire local notifications from ClientRpc
/// methods (e.g. NotifyClientAboutThePoleClientRpc in SwitcherScript) via
/// debugText.text = message.
/// </summary>
public class ToastScript : MonoBehaviour
{
    public static ToastScript Instance { get; private set; }

    [Header("References")]
    [SerializeField] RectTransform toastPanel;
    [SerializeField] CanvasGroup toastCanvasGroup; // optional, for fade; can be left null
    [SerializeField] TextMeshProUGUI toastText;

    [Header("Timings")]
    [SerializeField] float animateInDuration = 0.35f;
    [SerializeField] float holdDuration = 2f;
    [SerializeField] float animateOutDuration = 0.3f;

    [Header("Scale / Position Animation")]
    [SerializeField] Vector3 shownScale = Vector3.one;
    [SerializeField] Vector3 hiddenScale = Vector3.zero;
    [SerializeField] Ease easeIn = Ease.OutBack;
    [SerializeField] Ease easeOut = Ease.InBack;

    private Sequence _currentSequence;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Start hidden
        if (toastPanel != null)
            toastPanel.localScale = hiddenScale;
    }

    /// <summary>
    /// Static convenience entry point. Call this from anywhere in your
    /// client-side code instead of writing directly to a debugText field.
    /// If a toast is already playing, it's killed immediately and this
    /// new message takes over right away.
    /// </summary>
    public static void Toast(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[ToastScript] No ToastScript instance in scene. Message dropped: {message}");
            return;
        }
        Instance.ShowToast(message);
    }

    private void ShowToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (toastText != null)
            toastText.text = message;

        // Kill whatever's currently animating (if anything) and restart
        // the whole in/hold/out routine fresh for this message.
        _currentSequence?.Kill();

        toastPanel.localScale = hiddenScale;
        if (toastCanvasGroup != null) toastCanvasGroup.alpha = 0f;

        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(toastPanel.DOScale(shownScale, animateInDuration).SetEase(easeIn));
        if (toastCanvasGroup != null)
            _currentSequence.Join(toastCanvasGroup.DOFade(1f, animateInDuration));

        _currentSequence.AppendInterval(holdDuration);

        _currentSequence.Append(toastPanel.DOScale(hiddenScale, animateOutDuration).SetEase(easeOut));
        if (toastCanvasGroup != null)
            _currentSequence.Join(toastCanvasGroup.DOFade(0f, animateOutDuration));
    }

    private void OnDestroy()
    {
        _currentSequence?.Kill();
        if (Instance == this) Instance = null;
    }
}