using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// BÖLÜM SONU BİTİŞ & ZAFER EKRANI (LevelJamFlow):
/// Bölümün sonuna koyduğun herhangi bir objeye (kapı, ağaç, tabela, kare vb.) eklenir.
/// Oyuncu bu objeye değdiği anda:
/// 1) Ana menüdeki pahlı (kesik köşeli) Pixel Art çerçeve tasarımıyla birebir aynı şıklıkta
///    "BÖLÜM TAMAMLANDI!" ekranı açılır,
/// 2) Bölümü kaç saniyede tamamladığını (ve kaç kez öldüğünü) gösterir,
/// 3) Üzerine gelince yumuşakça büyüyen (Smooth Hover) "TEKRAR OYNA" ve "ANA MENÜ" butonları sunar!
/// </summary>
public class LevelJamFlow : MonoBehaviour
{
    [Header("1. Bölüm Sonu (Bitiş) Yazı ve Font Ayarları")]
    [Tooltip("İndirdiğin Pixelify Sans (.ttf) fontunu buraya sürükle (Boş bırakırsan Player üzerindeki HUD fontunu otomatik alır!).")]
    public Font customPixelFont;

    [Tooltip("Bölüm bittiğinde ekranda yazacak ana tebrik başlığı.")]
    public string victoryTitle = "BÖLÜM TAMAMLANDI!";

    [Tooltip("Ana başlığın hemen altındaki alt başlık.")]
    public string victorySubtitle = "Harika Düşüştü Baba!";

    [Tooltip("Ana Menüye Dön butonuna basıldığında yüklenecek menü sahnesinin adı (Örn: AnaMenu veya AnaMenu_1).")]
    public string mainMenuSceneName = "AnaMenu";

    [Tooltip("Eğer 2. bir bölüm sahnen varsa ve doğrudan oraya geçsin istersen adını buraya yazabilirsin.")]
    public string optionalNextLevelSceneName = "";

    [Header("2. Hitbox (Çarpışma Alanı) Ayarı")]
    [Tooltip("Bitiş objesinin hitbox'ı görselin yüzde kaçını kaplasın? (0.75 = Oyuncu objenin tam içine değdiğinde tetiklenir).")]
    [Range(0.2f, 1.2f)]
    public float finishHitboxTightness = 0.75f;

    [Header("3. İsteğe Bağlı: Bölüm Başlangıç Ekranı")]
    [Tooltip("Sadece bölüm sonu ekranı istiyorsan bunu KAPALI (False) bırak. Açarsan bölüm başında da başlangıç ekranı çıkar.")]
    public bool showStartScreenOnLoad = false;
    public string levelTitle = "BÖLÜM 1";
    public string levelSubtitle = "Yerçekimini Değiştir • Düşerek İlerle";

    private enum StageState
    {
        WaitingForStartInteraction,
        Playing,
        Completed
    }

    private StageState currentState = StageState.Playing;
    private float introFadeAlpha = 0f;
    private float outroFadeAlpha = 0f;
    private float playTimer = 0f;
    private float finalCompletionTime = 0f;
    private int finalDeathCount = 0;
    private float stateTimer = 0f;

    // Butonlar için smooth hover animasyonu
    private readonly float[] buttonHoverProgress = new float[2];
    private float cornerCutSize = 8f;

    private Texture2D whiteTex;
    private Texture2D vignetteTex;
    private GravityPlayerController cachedPlayer;

    private void Awake()
    {
        CreateUITextures();
        SetupFinishTriggerCollider();
    }

    private void Start()
    {
        cachedPlayer = FindFirstObjectByType<GravityPlayerController>();

        // Eğer Inspector'dan font atanmadıysa Player üzerindeki HUD fontunu otomatik çek
        if (customPixelFont == null && cachedPlayer != null && cachedPlayer.hudPixelFont != null)
        {
            customPixelFont = cachedPlayer.hudPixelFont;
        }

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
        whiteTex = new Texture2D(1, 1);
        whiteTex.filterMode = FilterMode.Point;
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        int size = 128;
        vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        vignetteTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.SmoothStep(0.35f, 0.92f, Mathf.InverseLerp(0.15f, 1.05f, dist));
                vignetteTex.SetPixel(x, y, new Color(0.02f, 0.04f, 0.09f, alpha));
            }
        }
        vignetteTex.Apply();
    }

    /// <summary>
    /// Bitiş objesinde devasa CircleCollider2D hatası olmaması için otomatik olarak
    /// görselin boyutuna tam oturan bir BoxCollider2D (IsTrigger = true) kurar.
    /// </summary>
    private void SetupFinishTriggerCollider()
    {
        CircleCollider2D oldCircle = GetComponent<CircleCollider2D>();
        if (oldCircle != null)
        {
            Destroy(oldCircle);
        }

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider2D>();
        }
        box.isTrigger = true;

        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr) && sr.sprite != null)
        {
            box.size = sr.sprite.bounds.size * finishHitboxTightness;
            box.offset = Vector2.zero;
        }
    }

    private void EnterStartScreenState()
    {
        currentState = StageState.WaitingForStartInteraction;
        introFadeAlpha = 1f;
        stateTimer = 0f;

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
            if (stateTimer > 0.15f && WasStartInteractionPressed())
            {
                StartPlayingLevel();
            }
        }
        else if (currentState == StageState.Playing)
        {
            playTimer += Time.deltaTime;

            if (introFadeAlpha > 0f)
            {
                introFadeAlpha = Mathf.MoveTowards(introFadeAlpha, 0f, dt * 3.5f);
            }
        }
        else if (currentState == StageState.Completed)
        {
            outroFadeAlpha = Mathf.MoveTowards(outroFadeAlpha, 1f, dt * 3.2f);

            // Klavyeden R veya Space basılırsa da hızlıca tekrar başlat
            if (stateTimer > 0.4f && WasQuickRestartPressed())
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

    private bool WasQuickRestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
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
    /// Oyuncu bitiş objesine değdiği an çağrılır, süreyi dondurur ve Zafer Ekranını açar!
    /// </summary>
    public void TriggerLevelComplete(GravityPlayerController player)
    {
        if (currentState == StageState.Completed) return;

        currentState = StageState.Completed;
        stateTimer = 0f;
        outroFadeAlpha = 0f;
        finalCompletionTime = playTimer;

        if (player != null)
        {
            finalDeathCount = player.DeathCount;
            if (customPixelFont == null && player.hudPixelFont != null)
            {
                customPixelFont = player.hudPixelFont;
            }

            player.SetFocusAbilityState(false);
            player.showFocusBarUI = false; // Bitiş ekranında üstteki HUD kalabalık yapmasın

            if (player.TryGetComponent<Rigidbody2D>(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
            }
            player.enabled = false;
        }

        if (CameraFollow2D.Instance != null)
        {
            CameraFollow2D.Instance.TriggerScreenShake(0.18f, 0.35f);
        }
    }

    public void RestartOrLoadNextLevel()
    {
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(optionalNextLevelSceneName))
        {
            SceneManager.LoadScene(optionalNextLevelSceneName);
        }
        else
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }

    private void OnGUI()
    {
        if (whiteTex == null) CreateUITextures();

        if (customPixelFont != null)
        {
            GUI.skin.font = customPixelFont;
        }

        if (currentState == StageState.WaitingForStartInteraction || introFadeAlpha > 0.01f)
        {
            DrawStartScreenOverlay(introFadeAlpha);
        }

        if (currentState == StageState.Completed)
        {
            DrawOrnateVictoryScreen(outroFadeAlpha);
        }
    }

    private void DrawOrnateVictoryScreen(float alpha)
    {
        float w = Screen.width;
        float h = Screen.height;

        // 1. Arka Plan Sinematik Vignette Karartması
        if (vignetteTex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), vignetteTex, ScaleMode.StretchToFill, true);
        }

        // 2. Orta Pahlı (Chamfered) Pixel Art Zafer Paneli (Hafifçe süzülen animasyonlu)
        float floatOffset = Mathf.Sin(Time.unscaledTime * 2.2f) * 4f;
        float panelW = Mathf.Min(640f, w * 0.88f);
        float panelH = 350f;
        float panelX = (w - panelW) * 0.5f;
        float panelY = ((h - panelH) * 0.45f) + floatOffset;

        Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

        DrawOrnateBeveledPanel(
            panelRect,
            new Color(0.05f, 0.08f, 0.15f, 0.95f * alpha),
            new Color(0.25f, 1f, 0.78f, alpha),
            new Color(0.12f, 0.48f, 0.45f, 0.85f * alpha),
            cornerCutSize + 2f,
            true);

        // 3. Ana Başlık: "BÖLÜM TAMAMLANDI!"
        DrawShadowedText(
            new Rect(panelX, panelY + 22f, panelW, 56f),
            victoryTitle,
            42,
            new Color(0.3f, 1f, 0.82f, alpha),
            new Color(0.02f, 0.14f, 0.18f, 0.95f * alpha),
            3f);

        // Alt Başlık
        DrawShadowedText(
            new Rect(panelX, panelY + 76f, panelW, 30f),
            victorySubtitle,
            18,
            new Color(1f, 0.86f, 0.35f, 0.95f * alpha),
            new Color(0.15f, 0.08f, 0.02f, 0.9f * alpha),
            2f);

        // Dekoratif Elmaslı Ayırıcı Çizgi
        float divW = panelW * 0.55f;
        float divX = panelX + (panelW - divW) * 0.5f;
        float divY = panelY + 114f;
        DrawSolidRect(new Rect(divX, divY, divW, 2f), new Color(0.28f, 0.95f, 0.82f, 0.4f * alpha));
        DrawSolidRect(new Rect(panelX + panelW * 0.5f - 4f, divY - 3f, 8f, 8f), new Color(1f, 0.85f, 0.3f, 0.95f * alpha));

        // 4. SÜRE VE ÖLÜM İSTATİSTİK KUTUSU (İç Pahlı Kutu)
        float statW = panelW - 110f;
        float statH = 82f;
        float statX = panelX + 55f;
        float statY = panelY + 132f;

        DrawOrnateBeveledPanel(
            new Rect(statX, statY, statW, statH),
            new Color(0.03f, 0.05f, 0.1f, 0.9f * alpha),
            new Color(1f, 0.85f, 0.3f, 0.85f * alpha),
            new Color(0.4f, 0.32f, 0.1f, 0.4f * alpha),
            6f,
            false);

        int minutes = Mathf.FloorToInt(finalCompletionTime / 60f);
        float seconds = finalCompletionTime % 60f;
        string timeText = minutes > 0
            ? $"TAMAMLAMA SÜRESİ :  {minutes:00}:{seconds:00.00}"
            : $"TAMAMLAMA SÜRESİ :  {finalCompletionTime:F2} SANİYE";

        DrawShadowedText(
            new Rect(statX, statY + 12f, statW, 32f),
            timeText,
            22,
            new Color(1f, 0.92f, 0.35f, alpha),
            new Color(0f, 0f, 0f, 0.9f * alpha),
            2f);

        DrawShadowedText(
            new Rect(statX, statY + 44f, statW, 26f),
            $"TOPLAM ÖLÜM :  {finalDeathCount}",
            16,
            new Color(0.85f, 0.92f, 1f, 0.9f * alpha),
            new Color(0f, 0f, 0f, 0.85f * alpha),
            1.5f);

        // 5. SMOOTH HOVER BUTONLARI ("TEKRAR OYNA [R]" ve "ANA MENÜ")
        float btnW = 240f;
        float btnH = 50f;
        float btnGap = 24f;
        float totalBtnsW = btnW * 2f + btnGap;
        float startBtnX = panelX + (panelW - totalBtnsW) * 0.5f;
        float btnY = panelY + panelH - 76f;

        string playAgainLabel = string.IsNullOrEmpty(optionalNextLevelSceneName) ? "↻  TEKRAR OYNA [R]" : "▶  SONRAKİ BÖLÜM";

        if (DrawFancySmoothButton(0, new Rect(startBtnX, btnY, btnW, btnH), playAgainLabel,
            new Color(0.22f, 0.95f, 0.68f, alpha), new Color(0.06f, 0.14f, 0.15f, 0.94f * alpha)))
        {
            RestartOrLoadNextLevel();
        }

        if (DrawFancySmoothButton(1, new Rect(startBtnX + btnW + btnGap, btnY, btnW, btnH), "⌂  ANA MENÜ",
            new Color(0.35f, 0.85f, 1f, alpha), new Color(0.06f, 0.11f, 0.18f, 0.94f * alpha)))
        {
            LoadMainMenu();
        }
    }

    private void DrawStartScreenOverlay(float alpha)
    {
        float w = Screen.width;
        float h = Screen.height;

        float bandW = Mathf.Min(640f, w * 0.85f);
        float bandH = 210f;
        float bandX = (w - bandW) * 0.5f;
        float bandY = (h - bandH) * 0.5f;

        DrawOrnateBeveledPanel(
            new Rect(bandX, bandY, bandW, bandH),
            new Color(0.05f, 0.08f, 0.15f, 0.94f * alpha),
            new Color(0.28f, 0.95f, 0.82f, alpha),
            new Color(0.12f, 0.45f, 0.52f, 0.8f * alpha),
            cornerCutSize,
            true);

        DrawShadowedText(new Rect(bandX, bandY + 24f, bandW, 56f), levelTitle, 44, new Color(0.3f, 1f, 0.85f, alpha), new Color(0f, 0f, 0f, 0.9f * alpha), 3f);
        DrawShadowedText(new Rect(bandX, bandY + 86f, bandW, 32f), levelSubtitle, 18, new Color(0.9f, 0.95f, 1f, 0.9f * alpha), new Color(0f, 0f, 0f, 0.8f * alpha), 2f);

        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
        DrawShadowedText(new Rect(bandX, bandY + 142f, bandW, 36f), "[ BAŞLAMAK İÇİN SPACE VEYA SOL TIK BAS ]", 16, new Color(1f, 0.88f, 0.25f, pulse * alpha), new Color(0f, 0f, 0f, 0.8f * alpha), 2f);
    }

    private bool DrawFancySmoothButton(int index, Rect baseRect, string label, Color accentColor, Color darkBgColor)
    {
        Vector2 mousePos = Event.current.mousePosition;
        bool isHovered = baseRect.Contains(mousePos);

        if (Event.current.type == EventType.Repaint)
        {
            float target = isHovered ? 1f : 0f;
            buttonHoverProgress[index] = Mathf.MoveTowards(buttonHoverProgress[index], target, Time.unscaledDeltaTime * 12f);
        }

        float hoverT = Mathf.SmoothStep(0f, 1f, buttonHoverProgress[index]);
        float extraW = hoverT * 16f;
        float extraH = hoverT * 4f;
        float liftY = hoverT * -2f;

        Rect animRect = new Rect(
            baseRect.x - extraW * 0.5f,
            baseRect.y + liftY - extraH * 0.5f,
            baseRect.width + extraW,
            baseRect.height + extraH);

        Color currentBg = Color.Lerp(darkBgColor, Color.Lerp(darkBgColor, accentColor, 0.28f), hoverT);
        Color currentOuter = Color.Lerp(accentColor * 0.85f, Color.white, hoverT * 0.8f);
        currentOuter.a = accentColor.a;
        Color currentInner = Color.Lerp(new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f * accentColor.a), accentColor, hoverT);

        DrawOrnateBeveledPanel(animRect, currentBg, currentOuter, currentInner, 6f, false, hoverT);

        string displayLabel = hoverT > 0.35f ? $"» {label} «" : label;
        Color textColor = Color.Lerp(accentColor, Color.white, hoverT * 0.9f);
        int fontSize = Mathf.RoundToInt(Mathf.Lerp(16f, 18f, hoverT));

        DrawShadowedText(animRect, displayLabel, fontSize, textColor, new Color(0f, 0f, 0f, 0.85f * accentColor.a), 2f);

        return GUI.Button(animRect, GUIContent.none, GUIStyle.none);
    }

    private void DrawOrnateBeveledPanel(Rect rect, Color bgColor, Color outerBorderColor, Color innerRimColor, float cut, bool drawCornerPins, float glowBoost = 0f)
    {
        float c = Mathf.Floor(cut);

        Rect shadowRect = new Rect(rect.x + 4f, rect.y + 6f, rect.width, rect.height);
        DrawChamferedRect(shadowRect, new Color(0f, 0f, 0f, 0.55f * outerBorderColor.a), c);

        if (glowBoost > 0.05f)
        {
            Rect glowRect = new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f);
            Color glowCol = outerBorderColor;
            glowCol.a = 0.22f * glowBoost;
            DrawChamferedRect(glowRect, glowCol, c + 2f);
        }

        DrawChamferedRect(rect, outerBorderColor, c);

        float border = 3f;
        Rect innerRect = new Rect(rect.x + border, rect.y + border, rect.width - border * 2f, rect.height - border * 2f);
        DrawChamferedRect(innerRect, bgColor, Mathf.Max(2f, c - border));

        float highlightCut = Mathf.Max(2f, c - border);
        DrawSolidRect(new Rect(innerRect.x + highlightCut, innerRect.y + 2f, innerRect.width - highlightCut * 2f, 3f), new Color(1f, 1f, 1f, (0.12f + glowBoost * 0.12f) * outerBorderColor.a));
        DrawSolidRect(new Rect(innerRect.x + highlightCut, innerRect.yMax - 4f, innerRect.width - highlightCut * 2f, 3f), new Color(0f, 0f, 0f, 0.35f * outerBorderColor.a));

        float rimOffset = 6f;
        Rect rimRect = new Rect(rect.x + rimOffset, rect.y + rimOffset, rect.width - rimOffset * 2f, rect.height - rimOffset * 2f);
        DrawChamferedOutlineOnly(rimRect, innerRimColor, Mathf.Max(2f, c - 4f), 1.5f);

        if (drawCornerPins)
        {
            float pinSize = 4f;
            float pinInset = c + 3f;
            Color pinCol = Color.Lerp(outerBorderColor, Color.white, 0.35f);
            DrawSolidRect(new Rect(rect.x + pinInset, rect.y + pinInset, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.xMax - pinInset - pinSize, rect.y + pinInset, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.x + pinInset, rect.yMax - pinInset - pinSize, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.xMax - pinInset - pinSize, rect.yMax - pinInset - pinSize, pinSize, pinSize), pinCol);
        }

        GUI.color = Color.white;
    }

    private void DrawChamferedRect(Rect r, Color col, float cut)
    {
        float c1 = Mathf.Floor(cut);
        float c2 = Mathf.Floor(cut * 0.5f);

        GUI.color = col;
        GUI.DrawTexture(new Rect(r.x, r.y + c1, r.width, r.height - c1 * 2f), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c2, r.y + c2, r.width - c2 * 2f, c1 - c2), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c2, r.yMax - c1, r.width - c2 * 2f, c1 - c2), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c1, r.y, r.width - c1 * 2f, c2), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c1, r.yMax - c2, r.width - c1 * 2f, c2), whiteTex);
    }

    private void DrawChamferedOutlineOnly(Rect r, Color col, float cut, float thickness)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(r.x + cut, r.y, r.width - cut * 2f, thickness), whiteTex);
        GUI.DrawTexture(new Rect(r.x + cut, r.yMax - thickness, r.width - cut * 2f, thickness), whiteTex);
        GUI.DrawTexture(new Rect(r.x, r.y + cut, thickness, r.height - cut * 2f), whiteTex);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y + cut, thickness, r.height - cut * 2f), whiteTex);
    }

    private void DrawSolidRect(Rect r, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(r, whiteTex);
    }

    private void DrawShadowedText(Rect rect, string text, int fontSize, Color mainColor, Color shadowColor, float shadowOffset)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        style.normal.textColor = shadowColor;
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height), text, style);

        style.normal.textColor = mainColor;
        GUI.Label(rect, text, style);
    }
}