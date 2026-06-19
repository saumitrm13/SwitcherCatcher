using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;
public class SizeManagerPracScript : MonoBehaviour
{
    [SerializeField] float scaleDownTime;
    [SerializeField] float scaleUpTime;
    [SerializeField] float scaleDownValue;
    bool transitionig = false;
    Vector3 scaleVector;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        scaleVector = new Vector3(scaleDownValue, scaleDownValue, scaleDownValue);
    }

    // Update is called once per frame
    void Update()
    {
        if (!transitionig)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                transitionig = true;
                ScaleRoutine();
            }
        }
        
    }

    void ScaleRoutine()
    {
        transform.DOScale(scaleDownValue, scaleDownTime).OnComplete(() =>
        {
            transform.DOScale(new Vector3(1, 1, 1), scaleUpTime).OnComplete(() => { transitionig = false; });
            
        });
    }
}
