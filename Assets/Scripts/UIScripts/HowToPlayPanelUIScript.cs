using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class HowToPlayPanelUIScript : MonoBehaviour
{
    [SerializeField] List<VideoPlayer> howToPlayPages;
    [SerializeField] float initialX;
    [SerializeField] float nextX;
    [SerializeField] float prevX;
    [SerializeField] float moveDuration = 0.5f;

    int currentPageIndex = 0;   

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void NextPage()
    {
        if (currentPageIndex < howToPlayPages.Count - 1)
        {
            howToPlayPages[currentPageIndex].transform.DOLocalMoveX(prevX,moveDuration);
            howToPlayPages[currentPageIndex + 1].transform.DOLocalMoveX(initialX, moveDuration);
            howToPlayPages[currentPageIndex + 1].Play();
            howToPlayPages[currentPageIndex].Stop();
           
            currentPageIndex++;
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            howToPlayPages[currentPageIndex].transform.DOLocalMoveX(nextX, moveDuration);
            howToPlayPages[currentPageIndex - 1].transform.DOLocalMoveX(initialX, moveDuration);
            howToPlayPages[currentPageIndex - 1].Play();
            howToPlayPages[currentPageIndex].Stop();
            currentPageIndex--;
        }       
    }

}
