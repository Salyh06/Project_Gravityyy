using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>A subdued forest menu with interactive, colliding letter particles.</summary>
public class MainMenuController : MonoBehaviour
{
    [Header("1. Sahne Geçiş Ayarları")]
    public string firstLevelSceneName = "Bolum_1_Eren_Kaplama";
    public bool loadByBuildIndex = false;
    public int firstLevelBuildIndex = 1;

    [Header("2. Menü Görsel & Font Ayarları")]
    public bool showBuiltInPixelMenu = true;
    public Font customPixelFont;
    public string gameTitle = "Bok Böceğinin Boku";
    public string gameSubtitle = "Düşmeye Devam Baba";

    [Header("3. Arka Plan Canlandırma (Kamera Salınımı)")]
    public bool animateCameraGently = true;
    public float cameraSwayAmount = 1.2f;
    public float cameraSwaySpeed = 0.6f;

    [Header("4. Buton Görünümü")]
    public float buttonHoverSmoothSpeed = 12f;
    [Range(4f, 14f)] public float cornerCutSize = 6f;

    [Header("5. Harf Yerçekimi & Geri Dönüş")]
    [Tooltip("Harflerin yukarı doğru ivmesi (menü pikseli / saniye kare).")]
    [Min(0f)] public float letterUpwardGravity = 160f;
    [Tooltip("Harflerin yuvalarına dönüşteki yay kuvveti.")]
    [Min(1f)] public float letterReturnSpring = 115f;
    [Tooltip("Düşük değer daha çok sekme, yüksek değer daha sakin dönüş sağlar.")]
    [Min(1f)] public float letterReturnDamping = 15f;

    private const float ReferenceWidth = 1280f;
    private const float ReferenceHeight = 720f;
    private static readonly string[] ButtonLabels =
        { "OYUNA BAŞLA", "NASIL OYNANIR", "OYUNDAN ÇIK", "GERİ DÖN" };
    private static readonly Color Ink = new Color(0.006f, 0.015f, 0.021f, 1f);
    private static readonly Color Paper = new Color(0.83f, 0.83f, 0.74f, 1f);
    private static readonly Color Moss = new Color(0.23f, 0.27f, 0.16f, 1f);
    private static readonly Color Clay = new Color(0.34f, 0.19f, 0.13f, 1f);

    private sealed class Letter
    {
        public string text;
        public Vector2 home;
        public Vector2 position;
        public Vector2 velocity;
        public float angle;
        public float angularVelocity;
        public float radius;
        public float phase;
        public float returnDelay;
    }

    private sealed class LetterButton
    {
        public Rect rect;
        public Letter[] letters;
        public float hover;
        public bool active;
        public bool moving;
        public float gustTime;
        public float returnTime;
    }

    private readonly LetterButton[] buttons =
        { new LetterButton(), new LetterButton(), new LetterButton(), new LetterButton() };
    // Keep menu randomness independent of gameplay's UnityEngine.Random state.
    private readonly System.Random letterRandom = new System.Random();
    private Vector3 initialCamPos;
    private Camera mainCam;
    private bool showHowToPlayModal;
    private bool hasFocus = true;
    private int pressedButton = -1;
    private int capturedControl;
    private Texture2D whiteTex;
    private Texture2D vignetteTex;
    private GUIStyle letterStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle controlsStyle;
    private Font styledFont;

    private float MenuScale => Mathf.Max(0.01f,
        Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight));
    private Vector2 MenuOffset => new Vector2(
        (Screen.width - ReferenceWidth * MenuScale) * 0.5f,
        (Screen.height - ReferenceHeight * MenuScale) * 0.5f);

    private void Awake()
    {
        Time.timeScale = 1f;
        mainCam = Camera.main;
        if (mainCam != null) initialCamPos = mainCam.transform.position;
        whiteTex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        GenerateMenuVignetteTexture();
        buttons[0].rect = new Rect(460f, 346f, 360f, 58f);
        buttons[1].rect = new Rect(460f, 440f, 360f, 58f);
        buttons[2].rect = new Rect(460f, 534f, 360f, 58f);
        buttons[3].rect = new Rect(510f, 572f, 260f, 52f);
    }

    private void GenerateMenuVignetteTexture()
    {
        const int size = 128;
        vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), Vector2.one * (size * 0.5f)) / (size * 0.5f);
            float alpha = Mathf.SmoothStep(0.04f, 0.72f, Mathf.InverseLerp(0.15f, 1.12f, distance));
            vignetteTex.SetPixel(x, y, new Color(0.02f, 0.035f, 0.055f, alpha));
        }
        vignetteTex.Apply();
    }

    private void Update()
    {
        if (animateCameraGently && mainCam != null)
        {
            float x = Mathf.Sin(Time.unscaledTime * cameraSwaySpeed) * cameraSwayAmount;
            float y = Mathf.Cos(Time.unscaledTime * cameraSwaySpeed * 0.7f) * cameraSwayAmount * 0.4f;
            mainCam.transform.position = initialCamPos + new Vector3(x, y, 0f);
        }

        if (!showBuiltInPixelMenu) return;
        Vector2 pointer = (PointerPosition() - MenuOffset) / MenuScale;
        // Substeps keep contacts and return springs stable across different frame rates.
        float elapsed = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed / (1f / 120f)));
        float dt = elapsed / steps;
        for (int i = 0; i < buttons.Length; i++)
        {
            LetterButton button = buttons[i];
            bool visible = showHowToPlayModal ? i == 3 : i < 3;
            bool hovered = hasFocus && visible && button.rect.Contains(pointer);
            bool active = hovered || (hasFocus && visible && pressedButton == i && PointerHeld());
            if (active && !button.active) Scatter(button);
            if (!active && button.active) BeginReturn(button);
            button.active = active;
            button.hover = Mathf.MoveTowards(button.hover, active ? 1f : 0f,
                elapsed * Mathf.Max(1f, buttonHoverSmoothSpeed));
            if (button.letters == null || !button.moving) continue;
            for (int step = 0; step < steps; step++) SimulateLetters(button, dt);
        }

        if (hasFocus && !showHowToPlayModal && pressedButton < 0 && WasQuickStartPressed())
            StartGameButton();
        if (hasFocus && showHowToPlayModal && WasBackPressed()) ToggleHowToPlayButton();
    }

    private static Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return Vector2.one * -10000f;
        Vector2 point = Mouse.current.position.ReadValue();
#else
        Vector2 point = Input.mousePosition;
#endif
        return new Vector2(point.x, Screen.height - point.y);
    }

    private static bool PointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private static bool WasQuickStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private static bool WasBackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)letterRandom.NextDouble());

    private void Scatter(LetterButton button)
    {
        if (button.letters == null) return;
        button.moving = true;
        button.gustTime = Range(0.12f, 0.35f);
        foreach (Letter letter in button.letters)
        {
            letter.velocity = new Vector2(Range(-95f, 95f), Range(-155f, -70f));
            letter.angularVelocity = Range(-160f, 160f);
            letter.phase = Range(0f, Mathf.PI * 2f);
        }
    }

    private void BeginReturn(LetterButton button)
    {
        button.returnTime = 0f;
        if (button.letters == null) return;
        foreach (Letter letter in button.letters)
        {
            // Preserve momentum; each letter catches its own spring a moment apart.
            letter.returnDelay = Range(0f, 0.1f);
            letter.velocity *= 0.48f;
            letter.angularVelocity *= 0.4f;
        }
    }

    private void SimulateLetters(LetterButton button, float dt)
    {
        if (!button.active)
        {
            AnimateReturn(button, dt);
            return;
        }

        button.gustTime -= dt;
        bool gust = button.gustTime <= 0f;
        if (gust) button.gustTime = Range(0.25f, 0.6f);
        float halfWidth = button.rect.width * 0.5f - 22f;
        foreach (Letter letter in button.letters)
        {
            float sideways = Mathf.Sin(Time.unscaledTime * 2.7f + letter.phase) * 85f;
            letter.velocity += new Vector2(sideways, -letterUpwardGravity) * dt;
            if (gust) letter.velocity.x += Range(-40f, 40f);
            letter.velocity *= Mathf.Exp(-0.24f * dt);
            letter.position += letter.velocity * dt;
            letter.angle = Mathf.Repeat(letter.angle + letter.angularVelocity * dt + 180f, 360f) - 180f;
            KeepInside(button, letter, halfWidth);
        }

        // Pairwise contact impulses make letters bump and exchange sideways momentum.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < button.letters.Length; i++)
            for (int j = i + 1; j < button.letters.Length; j++)
                ResolveContact(button.letters[i], button.letters[j]);
            foreach (Letter letter in button.letters) KeepInside(button, letter, halfWidth);
        }
    }

    private void KeepInside(LetterButton button, Letter letter, float halfWidth)
    {
        float left = -halfWidth + letter.radius;
        float right = halfWidth - letter.radius;
        float top = -button.rect.height * 0.5f - 26f + letter.radius;
        float bottom = button.rect.height * 0.5f + 7f - letter.radius;
        if (letter.position.x < left || letter.position.x > right)
        {
            letter.position.x = Mathf.Clamp(letter.position.x, left, right);
            letter.velocity.x = letter.position.x <= left ? Mathf.Abs(letter.velocity.x) * 0.8f : -Mathf.Abs(letter.velocity.x) * 0.8f;
            letter.angularVelocity = -letter.angularVelocity * 0.7f;
        }
        if (letter.position.y < top)
        {
            letter.position.y = top;
            // A fresh kick at the ceiling avoids a frozen row after a long hover.
            letter.velocity.y = Mathf.Max(48f, Mathf.Abs(letter.velocity.y) * 0.72f);
            letter.velocity.x += Range(-24f, 24f);
            letter.angularVelocity += Range(-55f, 55f);
        }
        else if (letter.position.y > bottom)
        {
            letter.position.y = bottom;
            letter.velocity.y = -Mathf.Abs(letter.velocity.y) * 0.7f;
        }
        letter.velocity = Vector2.ClampMagnitude(letter.velocity, 240f);
        letter.angularVelocity = Mathf.Clamp(letter.angularVelocity, -200f, 200f);
    }

    private static void ResolveContact(Letter a, Letter b)
    {
        Vector2 delta = b.position - a.position;
        float minimum = a.radius + b.radius;
        float sqrDistance = delta.sqrMagnitude;
        if (sqrDistance >= minimum * minimum) return;
        float distance = Mathf.Sqrt(sqrDistance);
        Vector2 normal = distance > 0.001f ? delta / distance : Vector2.right;
        Vector2 correction = normal * ((minimum - distance) * 0.5f);
        a.position -= correction;
        b.position += correction;
        float closingSpeed = Vector2.Dot(b.velocity - a.velocity, normal);
        if (closingSpeed >= 0f) return;
        Vector2 impulse = normal * (-(1f + 0.72f) * closingSpeed * 0.5f);
        a.velocity -= impulse;
        b.velocity += impulse;
        float spin = (normal.x * impulse.y - normal.y * impulse.x) * 3f + impulse.x * 0.75f;
        a.angularVelocity -= spin;
        b.angularVelocity += spin;
    }

    private void AnimateReturn(LetterButton button, float dt)
    {
        button.returnTime += dt;
        bool settled = true;
        foreach (Letter letter in button.letters)
        {
            if (button.returnTime < letter.returnDelay)
            {
                letter.position += letter.velocity * dt;
                letter.angle += letter.angularVelocity * dt;
                settled = false;
                continue;
            }
            Vector2 spring = (letter.home - letter.position) * letterReturnSpring - letter.velocity * letterReturnDamping;
            letter.velocity += spring * dt;
            letter.position += letter.velocity * dt;
            float angleToHome = Mathf.DeltaAngle(letter.angle, 0f);
            letter.angularVelocity += (angleToHome * letterReturnSpring - letter.angularVelocity * letterReturnDamping) * dt;
            letter.angle += letter.angularVelocity * dt;
            bool atRest = (letter.home - letter.position).sqrMagnitude < 0.01f &&
                letter.velocity.sqrMagnitude < 0.1f && Mathf.Abs(angleToHome) < 0.15f && Mathf.Abs(letter.angularVelocity) < 0.5f;
            if (!atRest) { settled = false; continue; }
            letter.position = letter.home;
            letter.velocity = Vector2.zero;
            letter.angle = 0f;
            letter.angularVelocity = 0f;
        }
        button.moving = !settled;
    }

    public void StartGameButton()
    {
        Time.timeScale = 1f;
        if (loadByBuildIndex) SceneManager.LoadScene(firstLevelBuildIndex);
        else if (!string.IsNullOrEmpty(firstLevelSceneName)) SceneManager.LoadScene(firstLevelSceneName);
    }

    public void ToggleHowToPlayButton()
    {
        showHowToPlayModal = !showHowToPlayModal;
        ReleaseCapture();
    }

    public void QuitGameButton()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void EnsureStyles()
    {
        if (letterStyle != null && styledFont == customPixelFont) return;
        styledFont = customPixelFont;
        letterStyle = MakeStyle(22, TextAnchor.MiddleCenter, Paper);
        titleStyle = MakeStyle(46, TextAnchor.MiddleCenter, Paper);
        subtitleStyle = MakeStyle(19, TextAnchor.MiddleCenter, new Color(0.64f, 0.66f, 0.59f));
        controlsStyle = MakeStyle(19, TextAnchor.MiddleLeft, Paper);
        controlsStyle.wordWrap = true;
        for (int i = 0; i < buttons.Length; i++) BuildLetters(buttons[i], ButtonLabels[i]);
    }

    private GUIStyle MakeStyle(int size, TextAnchor alignment, Color color)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            font = customPixelFont != null ? customPixelFont : GUI.skin.font,
            fontSize = size, fontStyle = FontStyle.Bold, alignment = alignment,
            padding = new RectOffset(), margin = new RectOffset(), richText = false
        };
        style.normal.textColor = color;
        return style;
    }

    private void BuildLetters(LetterButton button, string text)
    {
        float width = letterStyle.CalcSize(new GUIContent(text)).x;
        var letters = new System.Collections.Generic.List<Letter>();
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i])) continue;
            string glyph = text[i].ToString();
            float glyphWidth = letterStyle.CalcSize(new GUIContent(glyph)).x;
            float end = letterStyle.CalcSize(new GUIContent(text.Substring(0, i + 1))).x;
            Vector2 home = new Vector2(end - glyphWidth * 0.5f - width * 0.5f, 0f);
            letters.Add(new Letter
            {
                text = glyph, home = home, position = home,
                radius = Mathf.Clamp(glyphWidth * 0.46f, 5f, 11f)
            });
        }
        button.letters = letters.ToArray();
        button.active = false;
        button.moving = false;
    }

    private void OnGUI()
    {
        if (!showBuiltInPixelMenu) return;
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTex, ScaleMode.StretchToFill, true);
        Vector2 offset = MenuOffset;
        GUI.matrix = oldMatrix * Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity,
            new Vector3(MenuScale, MenuScale, 1f));

        DrawHeader();
        if (showHowToPlayModal) DrawHowToPlayWindow();
        else
        {
            if (DrawForestButton(0, Moss)) StartGameButton();
            if (DrawForestButton(1, new Color(0.16f, 0.24f, 0.27f))) ToggleHowToPlayButton();
            if (DrawForestButton(2, Clay)) QuitGameButton();
        }

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private void DrawHeader()
    {
        float bob = Mathf.Sin(Time.unscaledTime * 1.25f) * 2f;
        Rect header = new Rect(320f, 90f + bob, 640f, 136f);
        DrawPanel(header, new Color(Ink.r, Ink.g, Ink.b, 0.94f), new Color(0.07f, 0.115f, 0.13f), 8f);
        DrawText(new Rect(header.x + 14f, header.y + 14f, header.width - 28f, 60f), gameTitle, titleStyle);
        DrawSolidRect(new Rect(554f, header.y + 85f, 172f, 1f), new Color(0.37f, 0.4f, 0.33f));
        DrawSolidRect(new Rect(637f, header.y + 83f, 5f, 5f), Moss);
        DrawText(new Rect(header.x, header.y + 96f, header.width, 27f), gameSubtitle, subtitleStyle);
    }

    private bool DrawForestButton(int index, Color accent)
    {
        LetterButton button = buttons[index];
        // Hit areas stay fixed even when the face lifts and letters leave their slots.
        int control = GUIUtility.GetControlID(0x47524156 + index, FocusType.Passive, button.rect);
        Event current = Event.current;
        bool clicked = false;
        switch (current.GetTypeForControl(control))
        {
            case EventType.MouseDown:
                if (current.button == 0 && button.rect.Contains(current.mousePosition) && GUIUtility.hotControl == 0)
                {
                    GUIUtility.hotControl = control;
                    capturedControl = control;
                    pressedButton = index;
                    current.Use();
                }
                break;
            case EventType.MouseUp:
                if (current.button == 0 && GUIUtility.hotControl == control)
                {
                    clicked = button.rect.Contains(current.mousePosition);
                    ReleaseCapture();
                    current.Use();
                }
                break;
            case EventType.Ignore:
                if (GUIUtility.hotControl == control) ReleaseCapture();
                break;
        }

        float hover = Mathf.SmoothStep(0f, 1f, button.hover);
        Rect face = button.rect;
        face.y += pressedButton == index ? 1f : -hover * 2f;
        Color border = Color.Lerp(new Color(0.055f, 0.095f, 0.11f), Color.Lerp(accent, Ink, 0.25f), hover);
        Color fill = Color.Lerp(Ink, new Color(0.016f, 0.031f, 0.038f), hover);
        DrawPanel(face, fill, border, cornerCutSize);
        DrawSolidRect(new Rect(face.x + 12f, face.y + 15f, 3f, face.height - 30f), Color.Lerp(border, accent, 0.4f + hover * 0.4f));
        DrawSolidRect(new Rect(face.xMax - 17f, face.center.y - 2f, 4f, 4f), Color.Lerp(border, accent, hover * 0.7f));
        letterStyle.normal.textColor = Color.Lerp(Paper, new Color(0.94f, 0.91f, 0.8f), hover * 0.65f);
        foreach (Letter letter in button.letters)
        {
            Vector2 center = face.center + letter.position;
            Matrix4x4 matrix = GUI.matrix;
            // Apply rotation in reference-space before the canvas scale. RotateAroundPivot
            // unclips its pivot into screen pixels and displaces letters on scaled Game views.
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(center.x, center.y, 0f),
                Quaternion.Euler(0f, 0f, letter.angle), Vector3.one) *
                Matrix4x4.Translate(new Vector3(-center.x, -center.y, 0f));
            DrawText(new Rect(center.x - 22f, center.y - 19f, 44f, 38f), letter.text, letterStyle);
            GUI.matrix = matrix;
        }
        return clicked;
    }

    private void DrawHowToPlayWindow()
    {
        Rect modal = new Rect(290f, 282f, 700f, 360f);
        DrawPanel(modal, new Color(Ink.r, Ink.g, Ink.b, 0.98f), new Color(0.08f, 0.12f, 0.105f), 8f);
        DrawText(new Rect(330f, 304f, 620f, 28f), "NASIL OYNANIR", subtitleStyle);
        string controls =
            "SOL TIK   /   360° yerçekimi küresi fırlat\n\n" +
            "W A S D   /   Yüzeyde yürü ve havada süzül\n\n" +
            "SPACE   /   Yerçekiminin tersine zıpla\n\n" +
            "R / SAĞ TIK   /   Yerçekimini yüzeye hizala\n\n" +
            "SHIFT (HAVADA)   /   Odak ve keskin manevra";
        DrawText(new Rect(338f, 348f, 604f, 210f), controls, controlsStyle);
        if (DrawForestButton(3, Moss)) ToggleHowToPlayButton();
    }

    private void DrawPanel(Rect rect, Color fill, Color border, float cut)
    {
        DrawChamferedRect(new Rect(rect.x + 2f, rect.y + 5f, rect.width, rect.height), new Color(0.01f, 0.025f, 0.03f, 0.7f), cut);
        DrawChamferedRect(rect, border, cut);
        DrawChamferedRect(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), fill, Mathf.Max(2f, cut - 2f));
        DrawSolidRect(new Rect(rect.x + cut + 2f, rect.y + 3f, rect.width - cut * 2f - 4f, 1f), new Color(0.4f, 0.48f, 0.46f, 0.16f));
        DrawSolidRect(new Rect(rect.x + cut, rect.yMax - 4f, rect.width - cut * 2f, 2f), new Color(0f, 0.015f, 0.025f, 0.5f));
    }

    private void DrawChamferedRect(Rect rect, Color color, float cut)
    {
        float c = Mathf.Floor(Mathf.Clamp(cut, 2f, Mathf.Min(rect.width, rect.height) * 0.4f));
        float half = Mathf.Floor(c * 0.5f);
        DrawSolidRect(new Rect(rect.x, rect.y + c, rect.width, rect.height - c * 2f), color);
        DrawSolidRect(new Rect(rect.x + half, rect.y + half, rect.width - half * 2f, c - half), color);
        DrawSolidRect(new Rect(rect.x + half, rect.yMax - c, rect.width - half * 2f, c - half), color);
        DrawSolidRect(new Rect(rect.x + c, rect.y, rect.width - c * 2f, half), color);
        DrawSolidRect(new Rect(rect.x + c, rect.yMax - half, rect.width - c * 2f, half), color);
    }

    private void DrawSolidRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, whiteTex);
        GUI.color = Color.white;
    }

    private static void DrawText(Rect rect, string text, GUIStyle style)
    {
        Color color = style.normal.textColor;
        style.normal.textColor = new Color(0.005f, 0.015f, 0.02f, 0.8f);
        GUI.Label(new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }

    private void ReleaseCapture()
    {
        if (capturedControl != 0 && GUIUtility.hotControl == capturedControl) GUIUtility.hotControl = 0;
        capturedControl = 0;
        pressedButton = -1;
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (!focused) ReleaseCapture();
    }

    private void OnDisable()
    {
        ReleaseCapture();
        foreach (LetterButton button in buttons)
        {
            if (button.active) BeginReturn(button);
            button.active = false;
        }
    }

    private void OnDestroy()
    {
        if (whiteTex != null) Destroy(whiteTex);
        if (vignetteTex != null) Destroy(vignetteTex);
    }
}
