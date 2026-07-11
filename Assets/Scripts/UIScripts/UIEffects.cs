using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DG.Tweening;

public class UIEffects : MonoBehaviour
{
    [Header("UI")]
    
    [SerializeField] private GameObject videoRawImage;

    [Header("Video")]
    [SerializeField] private GameObject videoPlayerObject;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Timings")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float holdTimeBetweenFade = 0.3f;

    private void Start()
    {
        // Start hidden
       
    }

    public void PlaySequence()
    {
        StartCoroutine(SequenceCoroutine());
    }

    private IEnumerator SequenceCoroutine()
    {
       

        DOTween.Kill(videoRawImage);
        bool videoFinished = false;
        

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();
        videoRawImage.transform.DOScale(1, fadeInDuration)
            .OnComplete(() =>
            {




                }
            );
           
       
        void OnVideoFinished(VideoPlayer vp) => videoFinished = true;
        yield return new WaitUntil(() => videoFinished);

        videoPlayer.loopPointReached -= OnVideoFinished;
        // Deactivate video object after playback
        videoPlayer.Pause();
        videoRawImage.transform.DOScale(0, fadeOutDuration);
    }

   
 

    private void ClearRenderTexture(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = prev;
    }
}
