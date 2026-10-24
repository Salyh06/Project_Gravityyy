using UnityEngine;

/// <summary>
/// Karakterin fırlattığı nesne. Fırlatıldığı yönü hafızasında tutar ve bir yüzeye
/// (Ground katmanına) çarptığında dünyanın yerçekimini o fırlatma yönüne çevirir.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class GravityProjectile : MonoBehaviour
{
    [Header("Hareket ve Yaşam Süresi")]
    [Tooltip("Merminin uçuş hızı.")]
    public float speed = 22f;

    [Tooltip("Hiçbir yere çarpmazsa kaç saniye sonra yok olsun?")]
    public float maxLifetime = 4f;

    [Header("Çarpışma Ayarları")]
    [Tooltip("Hangi katmanlara çarptığında yerçekimi değişsin? (Örn: Ground)")]
    public LayerMask surfaceLayerMask;

    private Rigidbody2D rb;
    private Vector2 throwDirection;
    private bool hasTriggered = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // Merminin kendisi mevcut dünya yerçekiminden etkilenip eğik gitmesin,
        // fırlatıldığı 360 derece açıda dümdüz ilerlesin:
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    /// <summary>
    /// Karakter mermiyi fırlattığı anda bu fonksiyonu çağırır.
    /// </summary>
    public void Launch(Vector2 direction)
    {
        throwDirection = direction.normalized;

        // Görsel olarak mermiyi gidiş yönüne döndür
        float angle = Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Hızı doğrudan ver
        rb.linearVelocity = throwDirection * speed;

        // Belirli süre sonra otomatik temizle
        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject);
    }

    private void HandleHit(GameObject hitObject)
    {
        if (hasTriggered) return;

        // Çarptığımız obje belirlediğimiz yüzey katmanında mı kontrol et
        if (((1 << hitObject.layer) & surfaceLayerMask) != 0)
        {
            hasTriggered = true;

            if (GravityManager.Instance != null)
            {
                // Dünyanın yeni yerçekimi yönü = Merminin fırlatıldığı 360 derece yön!
                GravityManager.Instance.SetGravityDirection(throwDirection);
            }

            Destroy(gameObject);
        }
    }
}