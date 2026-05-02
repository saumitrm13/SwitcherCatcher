using UnityEngine;

public class CatcherUIScript : MonoBehaviour
{
    [SerializeField] CatcherScript catcherScript;
    [SerializeField] RectTransform cursedPoleSelectionPanel;
    bool selectedCursedPole = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!selectedCursedPole)
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                OnCurseBtnClick(6);
                selectedCursedPole = true;
            }
        }
    }

    //For now we know that Catcher is always on server...but that won't be the case 
    //after lobby and relay implementation
    //might need to make on pole cursed using serverrpc later
    public void OnCurseBtnClick(int poleTypeInt)
    {
        PoleType poleType = (PoleType)poleTypeInt;
        catcherScript.ChangeCursedPole(poleType);
        
   
        cursedPoleSelectionPanel.localScale = new Vector3(0, 0, 0);
            
        
    }
}
