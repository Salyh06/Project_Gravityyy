using UnityEngine;

/// <summary>The cap launches a falling player in the mushroom's own outward direction.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class MushroomBounce : MonoBehaviour
{
    [Min(24f)] public float bounceSpeed = 40f;
    [Min(0.1f)] public float bounceCooldown = 0.25f;

    private BoxCollider2D cap;
    private Vector3 restingScale;
    private Vector3 restingPosition;
    private float lastBounceTime = float.NegativeInfinity;
    private float animationTime = 1f;

    public Vector2 LaunchDirection => transform.up * (GetComponent<SpriteRenderer>().flipY ? -1f : 1f);

    private void Awake()
    {
        cap = GetComponent<BoxCollider2D>();
        cap.isTrigger = true;
        restingScale = transform.localScale;
        restingPosition = transform.localPosition;
    }

    private void OnTriggerEnter2D(Collider2D other) { TryBounce(other); }
    private void OnTriggerStay2D(Collider2D other) { TryBounce(other); }

    private void TryBounce(Collider2D other)
    {
        if (Time.time - lastBounceTime < bounceCooldown) return;
        var player = other.GetComponentInParent<GravityPlayerController>();
        if (player == null || other.isTrigger) return;
        var body = other.attachedRigidbody;
        Vector2 normal = LaunchDirection.normalized;
        if (body == null || Vector2.Dot(body.linearVelocity, normal) >= -0.1f) return;

        // A brush against the stem or the underside must not launch the player.
        Vector2 center = transform.TransformPoint(cap.offset);
        Vector2 extents = other.bounds.extents;
        float playerRadius = Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.y) * extents.y;
        float height = Vector2.Dot((Vector2)other.bounds.center - center, normal);
        float halfDepth = cap.size.y * Mathf.Abs(transform.lossyScale.y) * 0.5f;
        if (height < playerRadius - halfDepth - 0.2f) return;

        player.BounceFromSurface(normal, bounceSpeed);
        lastBounceTime = Time.time;
        animationTime = 0f;
    }

    private void LateUpdate()
    {
        if (animationTime >= 1f) return;
        animationTime = Mathf.Min(1f, animationTime + Time.deltaTime / 0.4f);
        float spring = Mathf.Sin(animationTime * Mathf.PI * 2f) * Mathf.Exp(-animationTime * 3f);
        Vector3 scale = restingScale;
        scale.x *= 1f + spring * 0.12f;
        scale.y *= 1f - spring * 0.28f;
        transform.localScale = scale;
        // Keep the foot attached to its wall or floor during the squash and rebound.
        transform.localPosition = restingPosition + transform.localRotation * Vector3.up * ((scale.y - restingScale.y) * 0.5f);
    }

    private void OnDisable()
    {
        transform.localScale = restingScale;
        transform.localPosition = restingPosition;
    }
}
