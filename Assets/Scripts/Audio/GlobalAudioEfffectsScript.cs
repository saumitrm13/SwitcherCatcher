using UnityEngine;
using UnityEngine.UI;

public class GlobalAudioEfffectsScript : MonoBehaviour
{
    public static GlobalAudioEfffectsScript Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;
    public AudioSource dangerVisualsAudioSource;
    public AudioClip throwableMagicLaunchAudioClip;
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // Hook every button currently in the scene
        HookAllButtons();
    }

    public void HookAllButtons()
    {
        foreach (var btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            btn.onClick.RemoveListener(PlayClickSound); // avoid double-hook
            btn.onClick.AddListener(PlayClickSound);
        }
    }

    public void PlayClickSound()
    {
        if (clickClip != null)
            audioSource.PlayOneShot(clickClip);
    }
}