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

    private void Awake()
    {
        var trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        view = Camera.main;
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
        if (view == null) view = Camera.main;
        if (view == null || !view.orthographic) return;
        Vector3 point = view.WorldToScreenPoint(transform.position);
        if (point.z <= 0f || point.x < -100f || point.x > Screen.width + 100f || point.y < -100f || point.y > Screen.height + 100f) return;
        Color oldColor = GUI.color;
        float unit = Screen.height / (view.orthographicSize * 2f);
        Vector2 foot = new Vector2(point.x, Screen.height - point.y);
        Rect stone = new Rect(foot.x - unit * 0.85f, foot.y - unit * 4.6f, unit * 1.7f, unit * 4.6f);
        Fill(stone, new Color(0.014f, 0.031f, 0.035f));
        Fill(new Rect(stone.x + unit * 0.15f, stone.y + unit * 0.2f, stone.width - unit * 0.3f, unit * 0.15f), new Color(0.1f, 0.14f, 0.115f));
        Color rune = activated ? new Color(0.27f, 0.36f, 0.19f) : new Color(0.09f, 0.14f, 0.12f);
        float thickness = Mathf.Max(1f, unit * 0.12f);
        Fill(new Rect(foot.x - thickness * 0.5f, stone.y + unit, thickness, unit * 2.4f), rune);
        Fill(new Rect(foot.x - unit * 0.5f, stone.y + unit * 1.6f, unit, thickness), rune);
        Fill(new Rect(foot.x - unit * 0.35f, stone.y + unit * 2.5f, unit * 0.7f, thickness), rune);
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
            GUI.Label(new Rect(foot.x - unit * 13f, stone.y - unit * 4f, unit * 26f, unit * 3.5f), text, labelStyle);
        }
        GUI.color = oldColor;
    }

    private static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.65f, 0.35f, 0.8f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, new Vector3(7f, 6f, 0.1f));
        Gizmos.DrawWireSphere(transform.position + respawnOffset, 1f);
    }
}
