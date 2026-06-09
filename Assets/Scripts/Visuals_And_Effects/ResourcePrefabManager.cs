using UnityEngine;

public class ResourcePrefabManager : MonoBehaviour
{
    [SerializeField] GameObject flamesVFX;
    [SerializeField] GameObject dustVFX;

    public void ActivateFlames()
    {
        flamesVFX.SetActive(true);
        dustVFX.SetActive(false);
    }

    public void ActivateDust()
    {
        flamesVFX.SetActive(false);
        dustVFX.SetActive(true);
    }
}
