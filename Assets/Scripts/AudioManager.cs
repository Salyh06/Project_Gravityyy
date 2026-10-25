using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources (Boş bırakılırsa otomatik oluşturulur)")]
    public AudioSource musicSource;
    public AudioSource ambianceSource;
    public AudioSource sfxSource;

    [Header("Genel Ses Seviyeleri (0 = Sessiz, 1 = Tam Ses)")]
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    [Range(0f, 1f)] public float ambianceVolume = 0.25f;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    [Header("Müzik & Ambiyans")]
    public AudioClip mainMenuMusic;
    public AudioClip backgroundMusic;    // Oyun içi arka plan müziği
    public AudioClip ambianceSound;      // Sonbahar rüzgar / ambiyans sesi

    [Header("Oyun İçi SFX (Birden Fazla Alternatif Ekleyebilirsin)")]
    [Tooltip("Mermi / küre fırlatma ses alternatifleri")]
    public AudioClip[] throwSounds;

    [Tooltip("Yer çekimi değişim / mermi çarpma ses alternatifleri")]
    public AudioClip[] gravityFlipSounds;

    [Tooltip("Zıplama ses alternatifleri")]
    public AudioClip[] jumpSounds;

    [Tooltip("Zemine iniş ses alternatifleri")]
    public AudioClip[] landSounds;

    [Tooltip("Drop (Düşme / Damlama / Bırakma) ses alternatifleri")]
    public AudioClip[] dropSounds;

    [Tooltip("Slow Motion (Yavaşlatma moduna girerken) ses alternatifleri")]
    public AudioClip[] slowMoEnterSounds;

    [Tooltip("Slow Motion (Normal zamana dönerken) ses alternatifleri")]
    public AudioClip[] slowMoExitSounds;

    [Tooltip("Ölüm / Respawn ses alternatifleri")]
    public AudioClip[] deathSounds;

    [Tooltip("Menü buton tıklama ses alternatifleri")]
    public AudioClip[] buttonClickSounds;

    [Header("SFX Özel Şiddet Çarpanları (Tek tek ses kısma)")]
    [Range(0f, 1f)] public float throwVolumeScale = 0.8f;
    [Range(0f, 1f)] public float gravityFlipVolumeScale = 0.85f;
    [Range(0f, 1f)] public float jumpVolumeScale = 0.6f;
    [Range(0f, 1f)] public float landVolumeScale = 0.5f;
    [Range(0f, 1f)] public float dropVolumeScale = 0.75f;
    [Range(0f, 1f)] public float slowMoVolumeScale = 0.7f;
    [Range(0f, 1f)] public float deathVolumeScale = 0.9f;
    [Range(0f, 1f)] public float buttonClickVolumeScale = 0.8f;

    private bool isCrossFading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (backgroundMusic != null && musicSource != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            musicSource.Play();
        }

        if (ambianceSound != null && ambianceSource != null)
        {
            ambianceSource.clip = ambianceSound;
            ambianceSource.loop = true;
            ambianceSource.Play();
        }
    }

    private void Update()
    {
        if (!isCrossFading && musicSource != null) musicSource.volume = musicVolume;
        if (ambianceSource != null) ambianceSource.volume = ambianceVolume;
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        SetupSource2D(musicSource, true, musicVolume);

        if (ambianceSource == null) ambianceSource = gameObject.AddComponent<AudioSource>();
        SetupSource2D(ambianceSource, true, ambianceVolume);

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        SetupSource2D(sfxSource, false, sfxVolume);

        // Slow motion (Time.timeScale) değişimlerinden SFX seslerinin etkilenmemesini sağlar
        sfxSource.ignoreListenerPause = true;
    }

    private void SetupSource2D(AudioSource source, bool loop, float volume)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 0f; // 2D ses: Kamera mesafesinden dolayı kısılmayı engeller
    }

    // ================= MÜZİK VE AMBİYANS YÖNETİMİ =================

    public void PlayMainMenuMusic()
    {
        SwitchMusic(mainMenuMusic);
        StopAmbiance();
    }

    public void PlayGameplayAudio()
    {
        SwitchMusic(backgroundMusic);
        PlayAmbiance(ambianceSound);
    }

    public void SwitchMusic(AudioClip newMusic, float fadeDuration = 0.8f)
    {
        if (newMusic == null || musicSource == null) return;
        if (musicSource.clip == newMusic && musicSource.isPlaying) return;
        StartCoroutine(CrossFadeMusicRoutine(newMusic, fadeDuration));
    }

    private IEnumerator CrossFadeMusicRoutine(AudioClip nextClip, float duration)
    {
        isCrossFading = true;
        float startVol = musicVolume;
        float halfDuration = duration * 0.5f;

        for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / halfDuration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = nextClip;
        musicSource.Play();

        for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / halfDuration);
            yield return null;
        }

        musicSource.volume = musicVolume;
        isCrossFading = false;
    }

    public void PlayAmbiance(AudioClip clip)
    {
        if (clip == null || ambianceSource == null) return;
        if (ambianceSource.clip == clip && ambianceSource.isPlaying) return;

        ambianceSource.clip = clip;
        ambianceSource.loop = true;
        ambianceSource.Play();
    }

    public void StopAmbiance()
    {
        if (ambianceSource != null && ambianceSource.isPlaying)
        {
            ambianceSource.Stop();
        }
    }

    // ================= SFX TETİKLEYİCİLERİ =================

    public void PlayThrowSound()
    {
        PlayRandomClip(throwSounds, 0.92f, 1.08f, throwVolumeScale);
    }

    public void PlayGravityFlip()
    {
        PlayRandomClip(gravityFlipSounds, 0.88f, 1.12f, gravityFlipVolumeScale);
    }

    public void PlayJump()
    {
        PlayRandomClip(jumpSounds, 0.95f, 1.05f, jumpVolumeScale);
    }

    public void PlayLanding()
    {
        PlayRandomClip(landSounds, 0.92f, 1.08f, landVolumeScale);
    }

    public void PlayDrop()
    {
        PlayRandomClip(dropSounds, 0.90f, 1.10f, dropVolumeScale);
    }

    public void PlaySlowMoEnter()
    {
        PlayRandomClip(slowMoEnterSounds, 0.9f, 1.1f, slowMoVolumeScale);
    }

    public void PlaySlowMoExit()
    {
        PlayRandomClip(slowMoExitSounds, 0.9f, 1.1f, slowMoVolumeScale);
    }

    public void PlayDeath()
    {
        // Ölüm sesinin karakter yok olsa bile arkada çalmaya devam etmesi için PlayClipAtPoint kullanabiliriz 
        // Veya sfxSource üzerinden çalabiliriz:
        PlayRandomClip(deathSounds, 0.98f, 1.02f, deathVolumeScale);
    }

    public void PlayButtonClick()
    {
        PlayRandomClip(buttonClickSounds, 1f, 1f, buttonClickVolumeScale);
    }

    private void PlayRandomClip(AudioClip[] clips, float minPitch, float maxPitch, float volumeScale = 1f)
    {
        if (sfxSource == null || clips == null || clips.Length == 0) return;

        int randomIndex = Random.Range(0, clips.Length);
        AudioClip selectedClip = clips[randomIndex];

        if (selectedClip == null) return;

        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(selectedClip, volumeScale);
    }
}