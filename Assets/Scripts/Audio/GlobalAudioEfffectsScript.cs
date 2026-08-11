using UnityEngine;
using UnityEngine.UI;


public enum SoundType
{
    Click,
    ThrowableMagicLaunch,
    MiniCatcherExplode,
    LobbyJoinOrCreate
}
public class GlobalAudioEfffectsScript : MonoBehaviour
{
    public static GlobalAudioEfffectsScript Instance { get; private set; }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;
    public AudioSource dangerVisualsAudioSource;
    public AudioClip throwableMagicLaunchAudioClip;
    public AudioClip miniCatcherExplodeSoundEffect;
    public AudioClip lobbyJoinOrCreateSoundEffect;
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

    public void PlaySound(SoundType soundType)
    {
        switch (soundType)
        {
            case SoundType.Click:
                PlayClickSound();
                break;
            case SoundType.ThrowableMagicLaunch:
                if (throwableMagicLaunchAudioClip != null)
                    dangerVisualsAudioSource.PlayOneShot(throwableMagicLaunchAudioClip);
                break;
            case SoundType.MiniCatcherExplode:
                if (miniCatcherExplodeSoundEffect != null)
                    dangerVisualsAudioSource.PlayOneShot(miniCatcherExplodeSoundEffect);
                break;
            case SoundType.LobbyJoinOrCreate:
                if (lobbyJoinOrCreateSoundEffect != null)
                    audioSource.PlayOneShot(lobbyJoinOrCreateSoundEffect);
                break;
            default:
                Debug.LogWarning("Unhandled sound type: " + soundType);
                break;
        }
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }
}