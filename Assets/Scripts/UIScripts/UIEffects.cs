using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class UIEffects : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image targetImage;
    [SerializeField] private GameObject videoRawImage;

    [Header("Video")]
    [SerializeField] private GameObject videoPlayerObject;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Timings")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;

    private void Start()
    {
        // Start hidden
        SetImageAlpha(0f);
        if (videoPlayerObject != null)
            videoPlayerObject.SetActive(false);
    }

    public void PlaySequence()
    {
        StartCoroutine(SequenceCoroutine());
    }

    private IEnumerator SequenceCoroutine()
    {
        targetImage.gameObject.SetActive(true);

        // Fade in image: 0 -> 1
        yield return FadeImage(0f, 1f, fadeInDuration);
        videoPlayerObject.SetActive(true);
        videoRawImage.SetActive(true);
        // Fade out image: 1 -> 0
        yield return FadeImage(1f, 0f, fadeOutDuration);
        
        

        // Activate video object and play video
        
        bool videoFinished = false;
        void OnVideoFinished(VideoPlayer vp) => videoFinished = true;

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();

        // Wait until the video ends
        yield return new WaitUntil(() => videoFinished);

        videoPlayer.loopPointReached -= OnVideoFinished;

        // Deactivate video object after playback
        videoPlayerObject.SetActive(false);
        targetImage.gameObject.SetActive(false);
        videoRawImage.SetActive(false );
    }

    private IEnumerator FadeImage(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color c = targetImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            c.a = Mathf.Lerp(startAlpha, endAlpha, t);
            targetImage.color = c;
            yield return null;
        }

        c.a = endAlpha;
        targetImage.color = c;
    }

    private void SetImageAlpha(float alpha)
    {
        Color c = targetImage.color;
        c.a = alpha;
        targetImage.color = c;
    }
}
