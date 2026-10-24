using UnityEngine;

/// <summary>
/// Ana kameranın (Main Camera) oyuncuyu bölüm boyunca yumuşakça takip etmesini sağlar.
/// Time.unscaledDeltaTime kullandığı için Shift ile zaman yavaşlatıldığında (Focus Modu) bile
/// kamera oyuncuyu takılmadan, tereyağı gibi akıcı takip eder!
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Header("Takip Hedefi")]
    [Tooltip("Takip edilecek karakter (Boş bırakırsan sahnedeki GravityPlayerController'ı otomatik bulur).")]
    public Transform target;

    [Header("Takip ve Yumuşatma Ayarları")]
    [Tooltip("Kameranın karaktere göre konumu (Z değeri mutlaka -10 kalmalıdır!).")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Kameranın karakteri yakalama süresi (Düşük = anında/sıkı takip, Yüksek = daha yumuşak/gecikmeli takip).")]
    [Range(0.01f, 1f)]
    public float smoothTime = 0.12f;

    [Header("İsteğe Bağlı: Hareket Yönüne Önceden Bakış (Look-Ahead)")]
    [Tooltip("Karakter hızla düşerken/giderken kameranın gidiş yönünü biraz önceden göstermesini sağlar.")]
    public bool useVelocityLookAhead = true;

    [Tooltip("Hıza göre ne kadar ileri baksın?")]
    public float lookAheadFactor = 0.12f;

    [Tooltip("Maksimum ileri bakış mesafesi.")]
    public float maxLookAheadDistance = 3f;

    private Vector3 currentVelocity = Vector3.zero;
    private Rigidbody2D targetRb;
    private GravityPlayerController targetPlayer;
    private Vector3 currentLookAhead = Vector3.zero;

    private void Start()
    {
        FindTargetIfNeeded();

        if (target != null)
        {
            Vector3 startPos = target.position + offset;
            startPos.z = offset.z;
            transform.position = startPos;
        }
    }

    private void FindTargetIfNeeded()
    {
        if (target == null)
        {
            GravityPlayerController player = FindFirstObjectByType<GravityPlayerController>();
            if (player != null)
            {
                target = player.transform;
                targetRb = player.GetComponent<Rigidbody2D>();
                targetPlayer = player;
            }
        }
        else
        {
            if (targetRb == null) targetRb = target.GetComponent<Rigidbody2D>();
            if (targetPlayer == null) targetPlayer = target.GetComponent<GravityPlayerController>();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTargetIfNeeded();
            if (target == null) return;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0.0001f) dt = Time.deltaTime;

        Vector3 desiredPos = target.position + offset;

        // Shift (Focus) modundayken kamera tam karaktere odaklansın (zoom yaparken savrulmasın)
        bool isFocusing = targetPlayer != null && targetPlayer.IsFocusActive;

        if (useVelocityLookAhead && targetRb != null && !isFocusing)
        {
            Vector2 vel = targetRb.linearVelocity;
            Vector3 targetLookAhead = Vector3.ClampMagnitude(new Vector3(vel.x, vel.y, 0f) * lookAheadFactor, maxLookAheadDistance);
            currentLookAhead = Vector3.Lerp(currentLookAhead, targetLookAhead, dt * 6f);
            desiredPos += currentLookAhead;
        }
        else
        {
            currentLookAhead = Vector3.Lerp(currentLookAhead, Vector3.zero, dt * 8f);
            desiredPos += currentLookAhead;
        }

        desiredPos.z = offset.z;

        // Shift ile zaman yavaşlatıldığında bile kameranın akıcı takip etmesi için unscaledDeltaTime kullan
        float effectiveSmoothTime = isFocusing ? smoothTime * 0.55f : smoothTime;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref currentVelocity,
            effectiveSmoothTime,
            Mathf.Infinity,
            dt);
    }

    public void SnapToTargetImmediate()
    {
        if (target == null) return;
        currentVelocity = Vector3.zero;
        currentLookAhead = Vector3.zero;
        Vector3 snapPos = target.position + offset;
        snapPos.z = offset.z;
        transform.position = snapPos;
    }
}