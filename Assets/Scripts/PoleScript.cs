using UnityEngine;

public class PoleScript : MonoBehaviour
{   
    [SerializeField]PoleType poleType;  
    public Pole thisPole;
    public bool isCursed = false;
    void Awake()
    {   
        thisPole = new Pole(poleType);   
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

   
}
