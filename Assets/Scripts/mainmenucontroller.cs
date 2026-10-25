using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// GELİŞMİŞ RETRO / PIXEL ART ANA MENÜ YÖNETİCİSİ:
/// Düz dikdörtgen kutular yerine:
/// - Pahlı/Kesik Köşeli (Chamfered / Beveled) Pixel Art Çerçeveler,
/// - 3B Derinlik Gölgeleri (Drop Shadow) ve İç Parlama (Inner Bevel Highlight),
/// - Köşe Süsleme İğneleri (Corner Ornaments),
/// - Fareyle üzerine gelince (Hover) yumuşakça genişleyen (Smooth Lerp Scale), parlayan ve
///   yanlarında hareketli oklar (`» ... «`) beliren interaktif butonlar,
/// - Havada süzülen (Floating Bob) başlık animasyonu içerir!
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("1. Sahne Geçiş Ayarları")]
    [Tooltip("Oyuna Başla'ya basıldığında yüklenecek ilk bölüm sahnesinin tam adı (Örn: Bolum_1_Eren_Kaplama).")]
    public string firstLevelSceneName = "Bolum_1_Eren_Kaplama";

    [Tooltip("Eğer sahne adı yerine Build Settings'teki sıra numarasını (Index 1) kullanmak istersen bunu aç.")]
    public bool loadByBuildIndex = false;
    public int firstLevelBuildIndex = 1;

    [Header("2. Menü Görsel & Font Ayarları")]
    [Tooltip("AÇIK OLURSA: Ekrana gelişmiş çerçeveli ve smooth animasyonlu Pixel Ana Menüyü çizer.")]
    public bool showBuiltInPixelMenu = true;

    [Tooltip("İndirdiğin Pixel Font (.ttf) dosyasını buraya sürükle (Örn: Pixelify Sans).")]
    public Font customPixelFont;

    [Tooltip("Oyunun Ana Başlığı.")]
    public string gameTitle = "Bok Böceğinin Boku";

    [Tooltip("Oyunun Alt Başlığı.")]
    public string gameSubtitle = "Düşmeye Devam Baba";

    [Header("3. Arka Plan Canlandırma (Kamera Salınımı)")]
    [Tooltip("Ana menüde arka plandaki ağaçların/dünyanın hafifçe sağa sola süzülmesini sağlar.")]
    public bool animateCameraGently = true;
    public float cameraSwayAmount = 1.2f;
    public float cameraSwaySpeed = 0.6f;

    [Header("4. Buton Smooth Animasyon & Çerçeve Detayları")]
    [Tooltip("Fareyle butonun üzerine gelindiğinde (Hover) yumuşak büyüme ve parlama hızı.")]
    public float buttonHoverSmoothSpeed = 12f;

    [Tooltip("Butonların köşe kesim/pah miktarı (Pixel Art kademeli köşe).")]
    [Range(4f, 14f)]
    public float cornerCutSize = 8f;

    private Vector3 initialCamPos;
    private Camera mainCam;
    private bool showHowToPlayModal = false;

    // 4 adet buton için smooth hover animasyon değerleri (0 = normal, 1 = tam hover)
    private readonly float[] buttonHoverProgress = new float[4];

    private Texture2D whiteTex;
    private Texture2D vignetteTex;

    private void Awake()
    {
        Time.timeScale = 1f;
        mainCam = Camera.main;
        if (mainCam != null)
        {
            initialCamPos = mainCam.transform.position;
        }

        whiteTex = new Texture2D(1, 1);
        whiteTex.filterMode = FilterMode.Point;
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();

        GenerateMenuVignetteTexture();
    }

    private void GenerateMenuVignetteTexture()
    {
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
                float alpha = Mathf.SmoothStep(0.15f, 0.78f, Mathf.InverseLerp(0.2f, 1.05f, dist));
                vignetteTex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.08f, alpha));
            }
        }
        vignetteTex.Apply();
    }

    private void Update()
    {
        if (animateCameraGently && mainCam != null)
        {
            float offsetX = Mathf.Sin(Time.unscaledTime * cameraSwaySpeed) * cameraSwayAmount;
            float offsetY = Mathf.Cos(Time.unscaledTime * cameraSwaySpeed * 0.7f) * (cameraSwayAmount * 0.4f);
            mainCam.transform.position = initialCamPos + new Vector3(offsetX, offsetY, 0f);
        }

        if (!showHowToPlayModal && WasQuickStartPressed())
        {
            StartGameButton();
        }
    }

    private bool WasQuickStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    public void StartGameButton()
    {
        Time.timeScale = 1f;

        if (loadByBuildIndex)
        {
            SceneManager.LoadScene(firstLevelBuildIndex);
        }
        else if (!string.IsNullOrEmpty(firstLevelSceneName))
        {
            SceneManager.LoadScene(firstLevelSceneName);
        }
    }

    public void ToggleHowToPlayButton()
    {
        showHowToPlayModal = !showHowToPlayModal;
    }

    public void QuitGameButton()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnGUI()
    {
        if (!showBuiltInPixelMenu) return;

        if (customPixelFont != null)
        {
            GUI.skin.font = customPixelFont;
        }

        float w = Screen.width;
        float h = Screen.height;
        float time = Time.unscaledTime;

        // 1. Sinematik Kenar Karartma (Vignette) - Ortadaki ağaçları açık bırakır, kenarları odak-lar
        if (vignetteTex != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0f, 0f, w, h), vignetteTex, ScaleMode.StretchToFill, true);
        }

        // 2. ÜST BAŞLIK PANELİ (Hafifçe yukarı-aşağı süzülen Floating Animasyon + Pahlı Pixel Çerçeve)
        float floatOffset = Mathf.Sin(time * 2.2f) * 5f;
        float headerW = Mathf.Min(680f, w * 0.85f);
        float headerH = 152f;
        float headerX = (w - headerW) * 0.5f;
        float headerY = (h * 0.12f) + floatOffset;

        Rect headerRect = new Rect(headerX, headerY, headerW, headerH);

        DrawOrnateBeveledPanel(
            headerRect,
            new Color(0.06f, 0.09f, 0.16f, 0.92f),      // Koyu Gece Laciverti İç Dolgu
            new Color(0.28f, 0.95f, 0.82f, 1f),         // Parlak Turkuaz Ana Çerçeve
            new Color(0.12f, 0.45f, 0.52f, 0.85f),      // İç İnce Çerçeve (Inner Rim)
            cornerCutSize + 2f,
            true);

        // Başlık Yazısı (Gölgeli + Parlak)
        DrawShadowedText(
            new Rect(headerX, headerY + 20f, headerW, 68f),
            gameTitle,
            50,
            new Color(0.35f, 1f, 0.86f, 1f),
            new Color(0.02f, 0.15f, 0.2f, 0.95f),
            3f);

        // Başlık ile Alt Başlık Arası Dekoratif Ayırıcı Çizgi ve Elmas Nokta
        float dividerW = headerW * 0.45f;
        float dividerX = headerX + (headerW - dividerW) * 0.5f;
        float dividerY = headerY + 90f;
        DrawSolidRect(new Rect(dividerX, dividerY, dividerW, 2f), new Color(1f, 0.82f, 0.3f, 0.45f));
        DrawSolidRect(new Rect(headerX + headerW * 0.5f - 4f, dividerY - 3f, 8f, 8f), new Color(1f, 0.85f, 0.32f, 0.95f));

        // Alt Başlık Yazısı
        DrawShadowedText(
            new Rect(headerX, headerY + 100f, headerW, 36f),
            gameSubtitle,
            20,
            new Color(1f, 0.86f, 0.35f, 0.98f),
            new Color(0.15f, 0.08f, 0.02f, 0.9f),
            2f);

        // 3. EĞER "NASIL OYNANIR" PENCERESİ AÇIKSA ONU ÇİZ
        if (showHowToPlayModal)
        {
            DrawHowToPlayWindow(w, h);
            return;
        }

        // 4. SMOOTH ANİMASYONLU & DETAYLI ÇERÇEVELİ MENÜ BUTONLARI
        float btnW = 340f;
        float btnH = 58f;
        float btnX = (w - btnW) * 0.5f;
        float startY = h * 0.47f;
        float spacing = 78f;

        if (DrawFancySmoothButton(0, new Rect(btnX, startY, btnW, btnH), "▶  OYUNA BAŞLA",
            new Color(0.22f, 0.95f, 0.68f, 1f), new Color(0.06f, 0.14f, 0.15f, 0.92f)))
        {
            StartGameButton();
        }

        if (DrawFancySmoothButton(1, new Rect(btnX, startY + spacing, btnW, btnH), "?  NASIL OYNANIR",
            new Color(0.32f, 0.85f, 1f, 1f), new Color(0.06f, 0.11f, 0.18f, 0.92f)))
        {
            ToggleHowToPlayButton();
        }

        if (DrawFancySmoothButton(2, new Rect(btnX, startY + spacing * 2f, btnW, btnH), "✕  OYUNDAN ÇIK",
            new Color(1f, 0.42f, 0.48f, 1f), new Color(0.16f, 0.07f, 0.11f, 0.92f)))
        {
            QuitGameButton();
        }
    }

    private void DrawHowToPlayWindow(float w, float h)
    {
        float modalW = Mathf.Min(680f, w * 0.88f);
        float modalH = 340f;
        float modalX = (w - modalW) * 0.5f;
        float modalY = h * 0.41f;

        Rect modalRect = new Rect(modalX, modalY, modalW, modalH);

        DrawOrnateBeveledPanel(
            modalRect,
            new Color(0.05f, 0.08f, 0.14f, 0.96f),
            new Color(1f, 0.85f, 0.28f, 1f),
            new Color(0.45f, 0.35f, 0.12f, 0.8f),
            cornerCutSize + 2f,
            true);

        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        textStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);
        GUI.color = Color.white;

        string controlsText =
            "• [ SOL TIK ] : 360° Yerçekimi Küresi Fırlat (Çarptığı an dünya döner!)\n\n" +
            "• [ W, A, S, D ] : Ekrana Göre Yüzeyde Yürü ve Havada Süzül\n\n" +
            "• [ SPACE ] : Mevcut Yerçekiminin Tersine Zıpla\n\n" +
            "• [ R ] veya [ SAĞ TIK ] : Üzerinde Bulunduğun Yüzeye Yerçekimini Hizala\n\n" +
            "• [ SHIFT (Havada) ] : Odak & Keskin Hava Manevra Modu (Yere değince dolar)";

        GUI.Label(new Rect(modalX + 42f, modalY + 26f, modalW - 84f, 225f), controlsText, textStyle);

        if (DrawFancySmoothButton(3, new Rect((w - 260f) * 0.5f, modalY + modalH - 74f, 260f, 52f), "←  GERİ DÖN",
            new Color(1f, 0.85f, 0.28f, 1f), new Color(0.14f, 0.11f, 0.05f, 0.94f)))
        {
            showHowToPlayModal = false;
        }
    }

    /// <summary>
    /// Fareyle üzerine gelindiğinde (Hover) yumuşakça genişleyen, içten parlayan,
    /// pahlı (kesik köşeli) çerçeveye, 3B alt gölgeye ve hareketli yan oklara sahip buton çizer.
    /// </summary>
    private bool DrawFancySmoothButton(int index, Rect baseRect, string label, Color accentColor, Color darkBgColor)
    {
        Vector2 mousePos = Event.current.mousePosition;
        bool isHovered = baseRect.Contains(mousePos);

        // Smooth Hover geçişi (Time.unscaledDeltaTime ile yumuşak büyüme/küçülme)
        if (Event.current.type == EventType.Repaint)
        {
            float target = isHovered ? 1f : 0f;
            buttonHoverProgress[index] = Mathf.MoveTowards(
                buttonHoverProgress[index],
                target,
                Time.unscaledDeltaTime * buttonHoverSmoothSpeed);
        }

        float hoverT = Mathf.SmoothStep(0f, 1f, buttonHoverProgress[index]);

        // Hover oldukça buton yatayda hafifçe genişler ve 2 piksel yukarı kalkar!
        float extraWidth = hoverT * 24f;
        float extraHeight = hoverT * 4f;
        float liftY = hoverT * -2f;

        Rect animRect = new Rect(
            baseRect.x - extraWidth * 0.5f,
            baseRect.y + liftY - extraHeight * 0.5f,
            baseRect.width + extraWidth,
            baseRect.height + extraHeight);

        // Renk geçişleri
        Color currentBg = Color.Lerp(darkBgColor, Color.Lerp(darkBgColor, accentColor, 0.28f), hoverT);
        Color currentOuterBorder = Color.Lerp(accentColor * 0.85f, Color.white, hoverT * 0.8f);
        currentOuterBorder.a = 1f;
        Color currentInnerRim = Color.Lerp(new Color(accentColor.r, accentColor.g, accentColor.b, 0.25f), accentColor, hoverT);

        // Pahlı (Kesik Köşeli) ve 3B Gölgeli Panel Çizimi
        DrawOrnateBeveledPanel(animRect, currentBg, currentOuterBorder, currentInnerRim, cornerCutSize, true, hoverT);

        // Butonun Sol ve Sağ İç Kenarındaki Küçük Dekoratif Elmas/Çentikler
        float diamondSize = 6f + hoverT * 2f;
        Color diamondColor = Color.Lerp(accentColor * 0.7f, Color.white, hoverT);
        diamondColor.a = 0.85f;
        DrawSolidRect(new Rect(animRect.x + 14f, animRect.center.y - diamondSize * 0.5f, diamondSize, diamondSize), diamondColor);
        DrawSolidRect(new Rect(animRect.xMax - 14f - diamondSize, animRect.center.y - diamondSize * 0.5f, diamondSize, diamondSize), diamondColor);

        // Üzerine gelindiğinde yazının iki yanında beliren "» ... «" işaretleri
        string displayLabel = hoverT > 0.35f ? $"»  {label}  «" : label;
        Color textColor = Color.Lerp(accentColor, Color.white, hoverT * 0.9f);
        int fontSize = Mathf.RoundToInt(Mathf.Lerp(20f, 22f, hoverT));

        DrawShadowedText(
            animRect,
            displayLabel,
            fontSize,
            textColor,
            new Color(0f, 0f, 0f, 0.85f),
            2f);

        return GUI.Button(animRect, GUIContent.none, GUIStyle.none);
    }

    /// <summary>
    /// Sivri köşeli sıradan dikdörtgen yerine; köşeleri kademeli kesik (Chamfered Pixel Art),
    /// altında 3B derinlik gölgesi olan, çift katmanlı çerçeveye ve köşe iğnelerine sahip şık bir kutu çizer.
    /// </summary>
    private void DrawOrnateBeveledPanel(
        Rect rect,
        Color bgColor,
        Color outerBorderColor,
        Color innerRimColor,
        float cut,
        bool drawCornerPins,
        float glowBoost = 0f)
    {
        float c = Mathf.Floor(cut);

        // 1. 3B Alt Derinlik Gölgesi (Drop Shadow)
        Rect shadowRect = new Rect(rect.x + 4f, rect.y + 6f, rect.width, rect.height);
        DrawChamferedRect(shadowRect, new Color(0f, 0f, 0f, 0.55f), c);

        // 2. Eğer Hover/Glow varsa dışta yumuşak bir neon hale (Outer Glow)
        if (glowBoost > 0.05f)
        {
            Rect glowRect = new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f);
            Color glowCol = outerBorderColor;
            glowCol.a = 0.22f * glowBoost;
            DrawChamferedRect(glowRect, glowCol, c + 2f);
        }

        // 3. Dış Ana Çerçeve (Katman 1)
        DrawChamferedRect(rect, outerBorderColor, c);

        // 4. Ana İç Dolgu (Dış çerçeveden 3px içeride)
        float border = 3f;
        Rect innerRect = new Rect(rect.x + border, rect.y + border, rect.width - border * 2f, rect.height - border * 2f);
        DrawChamferedRect(innerRect, bgColor, Mathf.Max(2f, c - border));

        // 5. Üst İç Parlama Şeridi (Top Bevel Highlight - Kutuya 3B cam/metalik derinlik verir)
        float highlightCut = Mathf.Max(2f, c - border);
        DrawSolidRect(
            new Rect(innerRect.x + highlightCut, innerRect.y + 2f, innerRect.width - highlightCut * 2f, 3f),
            new Color(1f, 1f, 1f, 0.12f + glowBoost * 0.12f));

        // 6. Alt İç Gölge Şeridi (Bottom Bevel Shade)
        DrawSolidRect(
            new Rect(innerRect.x + highlightCut, innerRect.yMax - 4f, innerRect.width - highlightCut * 2f, 3f),
            new Color(0f, 0f, 0f, 0.35f));

        // 7. İnce İç Çerçeve (Inner Rim - 6px içeride dekoratif ikinci çerçeve)
        float rimOffset = 6f;
        Rect rimRect = new Rect(rect.x + rimOffset, rect.y + rimOffset, rect.width - rimOffset * 2f, rect.height - rimOffset * 2f);
        DrawChamferedOutlineOnly(rimRect, innerRimColor, Mathf.Max(2f, c - 4f), 1.5f);

        // 8. Köşe Süsleme İğneleri (Corner Ornaments - 4 köşede küçük parlak piksel kareler)
        if (drawCornerPins)
        {
            float pinSize = 4f;
            float pinInset = c + 3f;
            Color pinCol = Color.Lerp(outerBorderColor, Color.white, 0.35f);

            // Sol-Üst, Sağ-Üst, Sol-Alt, Sağ-Alt köşe noktaları
            DrawSolidRect(new Rect(rect.x + pinInset, rect.y + pinInset, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.xMax - pinInset - pinSize, rect.y + pinInset, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.x + pinInset, rect.yMax - pinInset - pinSize, pinSize, pinSize), pinCol);
            DrawSolidRect(new Rect(rect.xMax - pinInset - pinSize, rect.yMax - pinInset - pinSize, pinSize, pinSize), pinCol);
        }

        GUI.color = Color.white;
    }

    /// <summary>
    /// Köşeleri 2 kademeli piksel merdiveni şeklinde kesilmiş (Chamfered / Octagonal) dolu kutu çizer.
    /// </summary>
    private void DrawChamferedRect(Rect r, Color col, float cut)
    {
        float c1 = Mathf.Floor(cut);
        float c2 = Mathf.Floor(cut * 0.5f);

        GUI.color = col;

        // Orta tam genişlikteki gövde
        GUI.DrawTexture(new Rect(r.x, r.y + c1, r.width, r.height - c1 * 2f), whiteTex);

        // Üst ve alt ara kademe (Merdiven basamağı 1)
        GUI.DrawTexture(new Rect(r.x + c2, r.y + c2, r.width - c2 * 2f, c1 - c2), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c2, r.yMax - c1, r.width - c2 * 2f, c1 - c2), whiteTex);

        // En üst ve en alt dar şerit (Merdiven basamağı 2)
        GUI.DrawTexture(new Rect(r.x + c1, r.y, r.width - c1 * 2f, c2), whiteTex);
        GUI.DrawTexture(new Rect(r.x + c1, r.yMax - c2, r.width - c1 * 2f, c2), whiteTex);
    }

    private void DrawChamferedOutlineOnly(Rect r, Color col, float cut, float thickness)
    {
        GUI.color = col;
        // Üst ve Alt yatay çizgiler
        GUI.DrawTexture(new Rect(r.x + cut, r.y, r.width - cut * 2f, thickness), whiteTex);
        GUI.DrawTexture(new Rect(r.x + cut, r.yMax - thickness, r.width - cut * 2f, thickness), whiteTex);

        // Sol ve Sağ dikey çizgiler
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

        // Alt Gölge
        style.normal.textColor = shadowColor;
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + shadowOffset, rect.y + shadowOffset, rect.width, rect.height), text, style);

        // Ana Metin
        style.normal.textColor = mainColor;
        GUI.Label(rect, text, style);
    }
}