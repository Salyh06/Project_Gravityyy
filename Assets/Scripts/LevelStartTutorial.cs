using UnityEngine;

/// <summary>Weathered inscriptions fixed in the starting area, dissolving like passing mist.</summary>
[DisallowMultipleComponent]
public class LevelStartTutorial : MonoBehaviour
{
    public GravityPlayerController player;
    public Font pixelFont;
    [Min(1f)] public float visibleRadius = 28f;
    [Min(1f)] public float fadeDistance = 12f;

    private static readonly string[] Keys = { "W A S D", "SPACE", "SOL TIK", "R / SAĞ TIK", "SHIFT" };
    private static readonly string[] Actions =
    {
        "Yürü · Havada yön ver", "Yerçekiminin tersine zıpla", "Nişan al · Küre fırlat",
        "Yerçekimini yüzeye hizala", "Havada basılı tut · Odaklan"
    };
    private static readonly Vector2[] Offsets =
    {
        new Vector2(-7f, 8f), new Vector2(18f, 17f), new Vector2(42f, 7f),
        new Vector2(73f, 22f), new Vector2(106f, 36f)
    };
    private static readonly float[] Angles = { -5f, 4f, -3f, 5f, -4f };
    private readonly float[] opacity = new float[5];
    private readonly float[] visibleSince = { -1f, -1f, -1f, -1f, -1f };
    private Vector2 startPosition;
    private Camera view;
    private Texture2D mist;
    private GUIStyle keyStyle;
    private GUIStyle actionStyle;
    private int lastDeathCount;

    private void Awake()
    {
        if (player == null) player = GetComponent<GravityPlayerController>();
        if (player == null) { enabled = false; return; }
        startPosition = player.customSpawnPoint != null ? player.customSpawnPoint.position : player.transform.position;
        if (pixelFont == null) pixelFont = player.hudPixelFont;
        view = Camera.main;
        mist = new Texture2D(64, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < mist.height; y++)
        for (int x = 0; x < mist.width; x++)
        {
            float nx = (x - 31.5f) / 31.5f;
            float ny = (y - 15.5f) / 15.5f;
            float edge = Mathf.Clamp01(1f - nx * nx - ny * ny);
            mist.SetPixel(x, y, new Color(1f, 1f, 1f, edge * edge * edge));
        }
        mist.Apply();
    }

    private void Update()
    {
        if (player == null) return;
        if (view == null) view = Camera.main;
        if (lastDeathCount != player.DeathCount)
        {
            lastDeathCount = player.DeathCount;
            for (int i = 0; i < visibleSince.Length; i++) visibleSince[i] = -1f;
        }
        for (int i = 0; i < opacity.Length; i++)
        {
            Vector2 anchor = startPosition + Offsets[i];
            float distance = Vector2.Distance(anchor, player.transform.position);
            float nearby = 1f - Mathf.InverseLerp(visibleRadius, visibleRadius + fadeDistance, distance);
            bool unlocked = IsHintUnlocked(i);
            if (unlocked && nearby > 0.35f && visibleSince[i] < 0f)
                visibleSince[i] = Time.unscaledTime;
            // Ambient writing fades on its own; pointer position and held inputs never affect it.
            float dissolve = visibleSince[i] < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(12f, 16f, Time.unscaledTime - visibleSince[i]));
            float target = unlocked ? nearby * dissolve : 0f;
            opacity[i] = Mathf.MoveTowards(opacity[i], target, Time.unscaledDeltaTime * 1.4f);
        }
    }

    private bool IsHintUnlocked(int index)
    {
        // Throwing and surface alignment are introduced after a successful first landing.
        return (index != 2 && index != 3) || player.CompletedJumpCount > 0;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || player == null || view == null || !view.orthographic) return;
        if (keyStyle == null)
        {
            keyStyle = new GUIStyle(GUI.skin.label)
            {
                font = pixelFont != null ? pixelFont : GUI.skin.font,
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), richText = false
            };
            actionStyle = new GUIStyle(keyStyle);
        }
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        GUI.depth = 5;
        float pixelsPerUnit = Screen.height / (view.orthographicSize * 2f);
        for (int i = 0; i < opacity.Length; i++)
        {
            if (!IsHintUnlocked(i) || opacity[i] < 0.01f) continue;
            Vector2 world = startPosition + Offsets[i];
            Vector3 point = view.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
            if (point.z <= 0f) continue;
            Vector2 center = new Vector2(point.x, Screen.height - point.y);
            float size = Mathf.Clamp(pixelsPerUnit * 2.6f, 16f, 72f);
            Rect area = new Rect(center.x - size * 9f, center.y - size * 1.3f, size * 18f, size * 2.6f);
            if (!area.Overlaps(new Rect(0f, 0f, Screen.width, Screen.height))) continue;
            GUI.matrix = oldMatrix * Matrix4x4.TRS(new Vector3(center.x, center.y, 0f),
                Quaternion.Euler(0f, 0f, Angles[i]), Vector3.one) * Matrix4x4.Translate(-center);
            // Separate soft wisps drift behind the carved lettering without a panel or border.
            for (int wisp = 0; wisp < 3; wisp++)
            {
                float drift = Mathf.Sin(Time.unscaledTime * 0.22f + i * 2f + wisp) * size * 0.75f;
                GUI.color = new Color(0.55f, 0.64f, 0.61f, opacity[i] * 0.035f);
                GUI.DrawTexture(new Rect(area.x + drift + wisp * size * 2f, area.y + wisp * size * 0.3f,
                    area.width * 0.65f, area.height * 0.8f), mist);
            }
            float breath = 0.87f + 0.13f * Mathf.Sin(Time.unscaledTime * 0.7f + i * 1.8f);
            float alpha = opacity[i] * breath;
            keyStyle.fontSize = Mathf.RoundToInt(size);
            actionStyle.fontSize = Mathf.RoundToInt(size * 0.62f);
            DrawEtched(new Rect(area.x, center.y - size, area.width, size * 1.25f), Keys[i], keyStyle, alpha);
            DrawEtched(new Rect(area.x, center.y + size * 0.35f, area.width, size), Actions[i], actionStyle, alpha * 0.85f);
            GUI.matrix = oldMatrix;
        }
        GUI.depth = oldDepth;
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    private static void DrawEtched(Rect rect, string text, GUIStyle style, float alpha)
    {
        GUI.color = Color.white;
        style.normal.textColor = new Color(0.004f, 0.012f, 0.017f, alpha * 0.9f);
        style.Draw(new Rect(rect.x + 1.5f, rect.y + 2f, rect.width, rect.height), text, false, false, false, false);
        style.normal.textColor = new Color(0.58f, 0.6f, 0.49f, alpha);
        style.Draw(rect, text, false, false, false, false);
    }

    private void OnDestroy()
    {
        if (mist != null) Destroy(mist);
    }
}
