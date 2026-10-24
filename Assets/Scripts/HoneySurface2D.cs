using UnityEngine;

// Bu script, Is Trigger kapali bir BoxCollider2D ile ayni objede bulunmali.
[RequireComponent(typeof(BoxCollider2D))]
public class HoneySurface2D : MonoBehaviour
{
    [Header("Honey Movement")]
    [Min(0f)] public float moveSpeed = 2.5f;
    [Min(0f)] public float slideAcceleration = 15f;
    [Min(0f)] public float stickSpeed = 3f;
    [Min(0f)] public float gravitySlideMultiplier = 0.05f;

    [Header("Surface Attachment")]
    [Min(0f)] public float maxSnapDistance = 1.1f;
    [Min(0f)] public float edgeTolerance = 0.15f;
    [Min(0f)] public float surfaceGap = 0.015f;

    private BoxCollider2D box;
    private GravityPlayerController attachedPlayer;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        if (box.isTrigger)
            Debug.LogError("HoneySurface2D: Box Collider 2D Is Trigger kapali olmali.", this);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryAttach(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryAttach(collision);
    }

    private void TryAttach(Collision2D collision)
    {
        if (collision.contactCount == 0)
            return;

        GravityPlayerController player =
            collision.gameObject.GetComponentInParent<GravityPlayerController>();

        if (player == null)
            return;

        // Balin hangi yuzune dokunuldugunu temas noktasindan bul.
        Vector2 localPoint = transform.InverseTransformPoint(collision.GetContact(0).point);
        Vector2 relative = localPoint - box.offset;
        Vector2 halfSize = box.size * 0.5f;

        float xDistanceToFace = Mathf.Abs(Mathf.Abs(relative.x) - halfSize.x);
        float yDistanceToFace = Mathf.Abs(Mathf.Abs(relative.y) - halfSize.y);

        Vector2 localNormal;
        if (xDistanceToFace < yDistanceToFace)
        {
            localNormal = relative.x >= 0f ? Vector2.right : Vector2.left;
        }
        else
        {
            localNormal = relative.y >= 0f ? Vector2.up : Vector2.down;
        }

        Vector2 worldNormal = transform.TransformDirection(localNormal).normalized;
        attachedPlayer = player;
        player.EnterHoney(this, worldNormal);
    }

    // OnCollisionExit2D kullanmiyoruz: karakter donerken collider yaricapi
    // degisebilir ve Unity temasi bir anligina kesebilir.
    // Yuzeyden gercek ayrilma kontrolunu oyuncu FixedUpdate'te yapar.
    public bool TryGetAttachmentCorrection(
        Vector2 playerCenter,
        float playerRadius,
        Vector2 attachedNormal,
        out Vector2 correction)
    {
        correction = Vector2.zero;

        if (!isActiveAndEnabled || box == null || !box.enabled)
            return false;

        Vector2 right = transform.right.normalized;
        Vector2 up = transform.up.normalized;

        bool sideFace = Mathf.Abs(Vector2.Dot(attachedNormal, right)) >
                        Mathf.Abs(Vector2.Dot(attachedNormal, up));

        Vector2 faceNormal = sideFace
            ? right * Mathf.Sign(Vector2.Dot(attachedNormal, right))
            : up * Mathf.Sign(Vector2.Dot(attachedNormal, up));

        Vector2 tangent = sideFace ? up : right;
        Vector2 center = transform.TransformPoint(box.offset);

        Vector3 scale = transform.lossyScale;
        float halfAlongNormal = sideFace
            ? box.size.x * 0.5f * Mathf.Abs(scale.x)
            : box.size.y * 0.5f * Mathf.Abs(scale.y);

        float halfAlongTangent = sideFace
            ? box.size.y * 0.5f * Mathf.Abs(scale.y)
            : box.size.x * 0.5f * Mathf.Abs(scale.x);

        float alongSurface = Vector2.Dot(playerCenter - center, tangent);
        if (Mathf.Abs(alongSurface) > halfAlongTangent + edgeTolerance)
            return false;

        Vector2 facePoint = center + faceNormal * halfAlongNormal;
        float signedDistance = Vector2.Dot(playerCenter - facePoint, faceNormal);
        float desiredDistance = playerRadius + surfaceGap;
        float gap = signedDistance - desiredDistance;

        // Yuzeyden cok uzaklasmis ya da balin icine fazlaca girmis karakteri
        // zorla geri teleporte etme. Gercekten ayrilmissa normal fizige gec.
        if (Mathf.Abs(gap) > maxSnapDistance)
            return false;

        if (Mathf.Abs(gap) > 0.025f)
            correction = -faceNormal * gap;

        return true;
    }

    private void OnDisable()
    {
        if (attachedPlayer != null)
        {
            attachedPlayer.ExitHoney(this);
            attachedPlayer = null;
        }
    }
}
