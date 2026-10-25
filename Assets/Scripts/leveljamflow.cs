using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// GAME JAM BÖLÜM BAŞLANGIÇ & BİTİŞ YÖNETİCİSİ:
/// Hiç Canvas veya karmaşık UI objeleri kurmana gerek kalmadan:
/// 1) Bölüm başında sinematik bir "BÖLÜM 1" başlangıç ekranı ve tuşa basarak başlama (İnteraksiyon) sunar.
/// 2) Bölüm sonuna koyduğun bitiş objesine (veya bu scriptin takılı olduğu bitiş noktasına) değdiğinde
///    şık bir "BÖLÜMÜ BİTİRDİN!" ekranı, bitiriş süresi (Timer) ve Tekrar Oyna / Sonraki Bölüm interaksiyonu çıkarır!
/// </summary>
public class LevelJamFlow : MonoBehaviour
{
    [Header("1. Bölüm Başlangıç İnteraksiyonu Ayarları")]
    [Tooltip("Oyun açıldığında 'BÖLÜM 1 - Başlamak için SPACE / SOL TIK' interaksiyon ekranı gelsin mi?")]
    public bool showStartScreenOnLoad = true;

    [Tooltip("Başlangıç ekranındaki üst başlık.")]
    public string levelTitle = "BÖLÜM 1";

    [Tooltip("Başlangıç ekranındaki alt açıklama / hikaye veya mekanik ipucu.")]
    public string levelSubtitle = "Yerçekimini Değiştir • Düşerek İlerle";

    [Tooltip("Başlamak için oyuncuya gösterilecek interaksiyon mesajı.")]
    public string startPromptText = "[ BAŞLAMAK İÇİN SPACE VEYA SOL TIK BAS ]";

    [Header("2. Bölüm Sonu (Bitiş) Ayarları")]
    [Tooltip("Bölüm bittiğinde ekranda yazacak ana tebrik başlığı.")]
    public string victoryTitle = "BÖLÜMÜ BİTİRDİN!";

    [Tooltip("Bölüm bittiğinde alt kısımda yazacak interaksiyon mesajı.")]
    public string victoryPromptText = "[ TEKRAR OYNAMAK İÇİN 'R' VEYA 'SPACE' BAS ]";

    [Tooltip("Eğer 2. bir bölüm sahnen varsa adını buraya yazabilirsin (Boş bırakırsan mevcut bölümü yeniden başlatır).")]
    public string OptionalNextSceneName = "";

    [Tooltip("Bitiş tetikleyicisinin (Hitbox) görselin yüzde kaçını kaplayacağı (Erken tetiklenmemesi için 0.7 idealdir).")]
    [Range(0.2f, 1.2f)]
    public float finishHitboxTightness = 0.7f;

    // Durum değişkenleri
    private enum StageState
    {
        WaitingForStartInteraction,
        Playing,
        Completed
    }

    // Oyuncu öldüğünde (Sahne yeniden yüklenmiyorsa zaten sorun yok, ama sahne yeniden yüklenirse bile)
    // sadece ilk açılışta başlangıç ekranı sorması veya her seferinde sorması için kontrol:
    private StageState currentState = StageState.Playing;
    private float introFadeAlpha = 1f;
    private float outroFadeAlpha = 0f;
    private float playTimer = 0f;
    private float stateTimer = 0f;

    private Texture2D darkOverlayTex;
    private Texture2D accentLineTex;
    private GravityPlayerController cachedPlayer;

    private void Awake()
    {
        CreateUITextures();
        SetupFinishTriggerColliderIfPresent();
    }

    private void Start()
    {
        cachedPlayer = FindFirstObjectByType<GravityPlayerController>();

        if (showStartScreenOnLoad)
        {
            EnterStartScreenState();
        }
        else
        {
            currentState = StageState.Playing;
            introFadeAlpha = 0f;
        }
    }

    private void CreateUITextures()
    {
        darkOverlayTex = new Texture2D(1, 1);
        darkOverlayTex.SetPixel(0, 0, Color.white);
        darkOverlayTex.Apply();

        accentLineTex = new Texture2D(1, 1);
        accentLineTex.SetPixel(0, 0, Color.white);
        accentLineTex.Apply();
    }

    /// <summary>
    /// Bu scripti bölüm sonundaki bitiş objesine (örn. BitisNoktasi / Kare / Ağaç / Kapı) eklediğinde,
    /// devasa CircleCollider2D hatası olmaması için otomatik olarak görsele tam oturan bir BoxCollider2D Trigger kurar.
    /// </summary>
    private void SetupFinishTriggerColliderIfPresent()
    {
        CircleCollider2D oldCircle = GetComponent<CircleCollider2D>();
        if (oldCircle != null)
        {
            Destroy(oldCircle);
        }

        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr) && sr.sprite != null)
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider2D>();
            }
            box.isTrigger = true;
            box.size = sr.sprite.bounds.size * finishHitboxTightness;
            box.offset = Vector2.zero;
        }
        else if (TryGetComponent<Collider2D>(out Collider2D col))
        {
            col.isTrigger = true;
        }
    }

    private void EnterStartScreenState()
    {
        currentState = StageState.WaitingForStartInteraction;
        introFadeAlpha = 1f;
        stateTimer = 0f;

        // Oyuncu başlama tuşuna basana kadar zamanı ve karakteri beklet
        Time.timeScale = 0f;
        if (cachedPlayer != null)
        {
            cachedPlayer.enabled = false;
        }
    }

    private void StartPlayingLevel()
    {
        currentState = StageState.Playing;
        stateTimer = 0f;
        playTimer = 0f;

        Time.timeScale = 1f;
        if (cachedPlayer != null)
        {
            cachedPlayer.enabled = true;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        stateTimer += dt;

        if (currentState == StageState.WaitingForStartInteraction)
        {
            // İlk 0.15 saniye yanlışlıkla tıklamaları yok say, sonra Space / Sol Tık / Enter / W-A-S-D ile bölümü başlat!
            if (stateTimer > 0.15f && WasStartInteractionPressed())
            {
                StartPlayingLevel();
            }
        }
        else if (currentState == StageState.Playing)
        {
            playTimer += Time.deltaTime;

            // Başlangıç ekranı yumuşakça açılsın (Fade out)
            if (introFadeAlpha > 0f)
            {
                introFadeAlpha = Mathf.MoveTowards(introFadeAlpha, 0f, dt * 3.5f);
            }
        }
        else if (currentState == StageState.Completed)
        {
            // Bitiş ekranı yumuşakça kararsın (Fade in)
            outroFadeAlpha = Mathf.MoveTowards(outroFadeAlpha, 1f, dt * 3f);

            // Bölüm bittikten 0.4 saniye sonra R, Space veya Sol Tık basılırsa yeniden başlat / sonraki bölüme geç
            if (stateTimer > 0.4f && WasRestartOrNextPressed())
            {
                RestartOrLoadNextLevel();
            }
        }
    }

    private bool WasStartInteractionPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyPressed = Keyboard.current != null && (
            Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.aKey.wasPressedThisFrame ||
            Keyboard.current.dKey.wasPressedThisFrame);

        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return keyPressed || mousePressed;
#else
        return Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Return) ||
               Input.GetMouseButtonDown(0);
#endif
    }

    private bool WasRestartOrNextPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool keyPressed = Keyboard.current != null && (
            Keyboard.current.rKey.wasPressedThisFrame ||
            Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame);

        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        return keyPressed || mousePressed;
#else
        return Input.GetKeyDown(KeyCode.R) ||
               Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Return) ||
               Input.GetMouseButtonDown(0);
#endif
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentState != StageState.Playing) return;

        if (other.TryGetComponent<GravityPlayerController>(out GravityPlayerController player))
        {
            TriggerLevelComplete(player);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != StageState.Playing) return;

        if (collision.gameObject.TryGetComponent<GravityPlayerController>(out GravityPlayerController player))
        {
            TriggerLevelComplete(player);
        }
    }

    /// <summary>
    /// Oyuncu bitiş noktasına ulaştığında çağrılır.
    /// </summary>
    public void TriggerLevelComplete(GravityPlayerController player)
    {
        if (currentState == StageState.Completed) return;

        currentState = StageState.Completed;
        stateTimer = 0f;
        outroFadeAlpha = 0f;

        if (player != null)
        {
            player.SetFocusAbilityState(false);
            if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
            }
            player.enabled = false;
        }

        // Kamera varsa hafif bir zafer sarsıntısı ver
        if (CameraFollow2D.Instance != null)
        {
            CameraFollow2D.Instance.TriggerScreenShake(0.2f, 0.35f);
        }
    }

    private void RestartOrLoadNextLevel()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(OptionalNextSceneName))
        {
            SceneManager.LoadScene(OptionalNextSceneName);
        }
        else
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }

    private void OnGUI()
    {
        if (darkOverlayTex == null) CreateUITextures();

        // 1. BÖLÜM BAŞLANGIÇ İNTERAKSİYON EKRANI (WaitingForStartInteraction veya Fade-Out aşaması)
        if (currentState == StageState.WaitingForStartInteraction || introFadeAlpha > 0.01f)
        {
            DrawStartScreenOverlay(introFadeAlpha);
        }

        // 2. BÖLÜM BİTİŞ EKRANI ("BÖLÜMÜ BİTİRDİN!")
        if (currentState == StageState.Completed)
        {
            DrawVictoryScreenOverlay(outroFadeAlpha);
        }
    }

    private void DrawStartScreenOverlay(float alpha)
    {
        float w = Screen.width;
        float h = Screen.height;

        // Yarı saydam sinematik koyu arka plan
        GUI.color = new Color(0.03f, 0.05f, 0.09f, 0.82f * alpha);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), darkOverlayTex);

        // Orta sinematik bant
        float bandHeight = 230f;
        float bandY = (h - bandHeight) * 0.5f;
        GUI.color = new Color(0.06f, 0.1f, 0.16f, 0.94f * alpha);
        GUI.DrawTexture(new Rect(0f, bandY, w, bandHeight), darkOverlayTex);

        // Üst ve alt neon turkuaz çizgiler
        GUI.color = new Color(0.15f, 0.95f, 0.8f, 0.9f * alpha);
        GUI.DrawTexture(new Rect(w * 0.2f, bandY, w * 0.6f, 3f), accentLineTex);
        GUI.DrawTexture(new Rect(w * 0.2f, bandY + bandHeight - 3f, w * 0.6f, 3f), accentLineTex);

        // Bölüm Başlığı ("BÖLÜM 1")
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.25f, 1f, 0.85f, alpha);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, bandY + 28f, w, 65f), levelTitle, titleStyle);

        // Alt Başlık / Mekanik Özeti
        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Normal
        };
        subStyle.normal.textColor = new Color(0.85f, 0.92f, 1f, 0.9f * alpha);
        GUI.Label(new Rect(0f, bandY + 95f, w, 35f), levelSubtitle, subStyle);

        // Yanıp sönen (Pulse) İnteraksiyon Yazısı ("[ BAŞLAMAK İÇİN SPACE VEYA SOL TIK BAS ]")
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        promptStyle.normal.textColor = new Color(1f, 0.88f, 0.25f, pulse * alpha);
        GUI.Label(new Rect(0f, bandY + 155f, w, 40f), startPromptText, promptStyle);
    }

    private void DrawVictoryScreenOverlay(float alpha)
    {
        float w = Screen.width;
        float h = Screen.height;

        // Yarı saydam koyu arka plan
        GUI.color = new Color(0.02f, 0.06f, 0.08f, 0.85f * alpha);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), darkOverlayTex);

        // Orta zafer kutusu
        float boxW = 640f;
        float boxH = 260f;
        float boxX = (w - boxW) * 0.5f;
        float boxY = (h - boxH) * 0.5f;

        GUI.color = new Color(0.06f, 0.12f, 0.16f, 0.96f * alpha);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), darkOverlayTex);

        // Altın sarısı / Neon yeşil çerçeve çizgileri
        GUI.color = new Color(0.2f, 1f, 0.6f, 0.95f * alpha);
        GUI.DrawTexture(new Rect(boxX, boxY, boxW, 4f), accentLineTex);
        GUI.DrawTexture(new Rect(boxX, boxY + boxH - 4f, boxW, 4f), accentLineTex);

        // Ana Başlık: "BÖLÜMÜ BİTİRDİN!"
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 44,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.25f, 1f, 0.65f, alpha);
        GUI.color = Color.white;
        GUI.Label(new Rect(boxX, boxY + 32f, boxW, 60f), victoryTitle, titleStyle);

        // Bitirme Süresi (Jam sunumlarında oyuncuları çok motive eder!)
        GUIStyle timeStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        timeStyle.normal.textColor = new Color(0.9f, 0.95f, 1f, 0.95f * alpha);
        string timeFormatted = $"Tamamlama Süresi: {playTimer:F2} sn";
        GUI.Label(new Rect(boxX, boxY + 105f, boxW, 40f), timeFormatted, timeStyle);

        // Yanıp sönen Tekrar Oyna / Devam Et interaksiyon yazısı
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        promptStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, pulse * alpha);
        GUI.Label(new Rect(boxX, boxY + 185f, boxW, 40f), victoryPromptText, promptStyle);
    }
}