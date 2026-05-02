using UnityEngine;

public class EnableInputSystem : MonoBehaviour
{
    public CharacterInputs characterInputs;
   
   

    void Awake()
    {
        characterInputs = new CharacterInputs();
    }
     void OnEnable()
    {
        characterInputs.CharacterControls.Enable();
    }

    void OnDisable()
    {
        characterInputs.CharacterControls.Disable();
    }
}
