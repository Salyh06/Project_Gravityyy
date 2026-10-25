using UnityEngine;

/// <summary>A rare resting stone. The player must stop on safe ground to remember it.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class SparseCheckpoint : MonoBehaviour
{
    [Min(1)] public int checkpointIndex = 1;
    public Font pixelFont;
    public Vector3 respawnOffset = new Vector3(0f, 1.35f, 0f);
    [Min(0.1f)] public float restDuration = 0.45f;

    private GravityPlayerController candidate;
    private Rigidbody2D candidateBody;
    private float restTime;
    private bool activated;
    private float activatedAt;
    private Camera view;
    private GUIStyle labelStyle;
    private SpriteRenderer stoneRenderer;
    private Sprite idleStone;
    private Sprite savedStone;
    private Material stoneMaterial;

    private void Awake()
    {
        var trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        view = Camera.main;
        stoneRenderer = GetComponent<SpriteRenderer>();
        if (stoneRenderer == null) stoneRenderer = gameObject.AddComponent<SpriteRenderer>();
        var player = FindAnyObjectByType<GravityPlayerController>();
        var playerSprite = player != null ? player.GetComponent<SpriteRenderer>() : null;
        stoneRenderer.sortingLayerID = playerSprite != null ? playerSprite.sortingLayerID : 0;
        stoneRenderer.sortingOrder = playerSprite != null ? playerSprite.sortingOrder - 1 : 4;
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        stoneMaterial = new Material(shader);
        stoneRenderer.sharedMaterial = stoneMaterial;
        idleStone = CreateStone(false);
        savedStone = CreateStone(true);
        stoneRenderer.sprite = idleStone;
    }

    private void LateUpdate()
    {
        stoneRenderer.sprite = activated ? savedStone : idleStone;
    }

    private static Sprite CreateStone(bool saved)
    {
        // A world sprite lets the character pass in front of the stone.
        var texture = new Texture2D(17, 46, TextureFormat.RGBA32, false);
        texture.name = saved ? "Remembered stone" : "Resting stone";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color body = new Color(0.014f, 0.031f, 0.035f);
        Color rune = saved ? new Color(0.27f, 0.36f, 0.19f) : new Color(0.09f, 0.14f, 0.12f);
        var pixels = new Color[17 * 46];
        for (int y = 0; y < 46; y++)
        for (int x = 0; x < 17; x++)
        {
            Color color = body;
            if (y == 42 && x > 0 && x < 16) color = new Color(0.1f, 0.14f, 0.115f);
            if ((x == 8 && y >= 12 && y <= 35) || (y == 29 && x >= 3 && x <= 13) || (y == 20 && x >= 5 && x <= 11)) color = rune;
            pixels[y * 17 + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, 17f, 46f), new Vector2(0.5f, 0f), 10f);
    }

    private void OnDestroy()
    {
        if (idleStone != null) { Destroy(idleStone.texture); Destroy(idleStone); }
        if (savedStone != null) { Destroy(savedStone.texture); Destroy(savedStone); }
        if (stoneMaterial != null) Destroy(stoneMaterial);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var player = other.GetComponentInParent<GravityPlayerController>();
        if (player == null || player.CurrentCheckpointIndex >= checkpointIndex)
        {
            if (player != null) activated = true;
            return;
        }
        if (candidate != player)
        {
            candidate = player;
            candidateBody = player.GetComponent<Rigidbody2D>();
            restTime = 0f;
        }
        Vector2 gravity = GravityManager.Instance != null ? GravityManager.Instance.CurrentGravityDirection : Vector2.down;
        bool resting = player.IsGrounded && candidateBody != null && candidateBody.linearVelocity.sqrMagnitude < 9f &&
            Vector2.Dot(gravity, Vector2.down) > 0.95f;
        if (!resting) { restTime = 0f; return; }
        restTime += Time.fixedDeltaTime;
        if (restTime < restDuration) return;
        if (!player.TryActivateCheckpoint(checkpointIndex, transform.position + respawnOffset)) return;
        activated = true;
        activatedAt = Time.unscaledTime;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<GravityPlayerController>() != candidate) return;
        candidate = null;
        candidateBody = null;
        restTime = 0f;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (view == null) view = Camera.main;
        if (view == null || !view.orthographic) return;
        Vector3 point = view.WorldToScreenPoint(transform.position);
        if (point.z <= 0f || point.x < -100f || point.x > Screen.width + 100f || point.y < -100f || point.y > Screen.height + 100f) return;
        Color oldColor = GUI.color;
        float unit = Screen.height / (view.orthographicSize * 2f);
        Vector2 foot = new Vector2(point.x, Screen.height - point.y);
        if (candidate != null || (activated && Time.unscaledTime - activatedAt < 3.5f))
        {
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = pixelFont != null ? pixelFont : GUI.skin.font,
                alignment = TextAnchor.MiddleCenter, padding = new RectOffset(), richText = false
            };
            labelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(unit * 1.1f), 13, 30);
            labelStyle.normal.textColor = new Color(0.65f, 0.68f, 0.54f);
            GUI.color = Color.white;
            string text = activated ? "İZ KAYDEDİLDİ" : "KONTROL NOKTASI\nKısa bir an dur";
            labelStyle.Draw(new Rect(foot.x - unit * 13f, foot.y - unit * 8.6f, unit * 26f, unit * 3.5f), new GUIContent(text), false, false, false, false);
        }
        GUI.color = oldColor;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.65f, 0.35f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, new Vector3(7f, 6f, 0.1f));
        Gizmos.DrawWireSphere(transform.position + respawnOffset, 1f);
    }
}
