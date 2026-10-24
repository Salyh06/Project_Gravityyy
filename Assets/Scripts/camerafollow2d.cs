using UnityEngine;

/// <summary>
/// Ana kameranın (Main Camera) oyuncuyu bölüm boyunca yumuşakça takip etmesini
/// ve küre duvara çarptığında Mikro Ekran Sarsıntısı (Screen Shake) yapmasını sağlar.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    public static CameraFollow2D Instance { get; private set; }

    [Header("Takip Hedefi")]
    [Tooltip("Takip edilecek karakter (Boş bırakırsan sahnedeki GravityPlayerController'ı otomatik bulur).")]
    public Transform target;

    [Header("Takip ve Yumuşatma Ayarları")]
    [Tooltip("Kameranın karaktere göre konumu (Z değeri mutlaka -10 kalmalıdır!).")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Kameranın karakteri yakalama süresi (Düşük = anında/sıkı takip, Yüksek = daha yumuşak/gecikmeli takip).")]
    [Range(0.01f, 1f)]
    public float smoothTime = 0.12f;

    [Header("Hareket Yönüne Önceden Bakış (Look-Ahead)")]
    [Tooltip("Karakter hızla düşerken/giderken kameranın gidiş yönünü biraz önceden göstermesini sağlar.")]
    public bool useVelocityLookAhead = true;

    [Tooltip("Hıza göre ne kadar ileri baksın?")]
    public float lookAheadFactor = 0.12f;

    [Tooltip("Maksimum ileri bakış mesafesi.")]
    public float maxLookAheadDistance = 3f;

    [Header("Mikro Ekran Sarsıntısı (Screen Shake) Ayarları")]
    [Tooltip("Ekran sarsıntısı aktif olsun mu?")]
    public bool enableScreenShake = true;

    [Tooltip("Sarsıntı şiddeti çarpanı (1 = Standart, 2 = Daha sert sarsıntı, 0.5 = Çok hafif).")]
    [Range(0f, 3f)]
    public float shakeIntensityMultiplier = 1f;

    private Vector3 currentVelocity = Vector3.zero;
    private Rigidbody2D targetRb;
    private GravityPlayerController targetPlayer;
    private Vector3 currentLookAhead = Vector3.zero;

    // Kamera takip konumu (Shake eklenmemiş saf konum)
    private Vector3 baseCameraPosition;
    private bool basePosInitialized = false;

    // Screen Shake değişkenleri
    private float shakeTimer = 0f;
    private float shakeTotalDuration = 0.15f;
    private float shakeMagnitude = 0.25f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        FindTargetIfNeeded();

        if (target != null)
        {
            Vector3 startPos = target.position + offset;
            startPos.z = offset.z;
            baseCameraPosition = startPos;
            transform.position = startPos;
            basePosInitialized = true;
        }
        else
        {
            baseCameraPosition = transform.position;
            basePosInitialized = true;
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

    /// <summary>
    /// Küre duvara çarptığında veya önemli bir olay olduğunda kamerayı sarsmak için çağrılır.
    /// </summary>
    public void TriggerScreenShake(float duration = 0.14f, float magnitude = 0.32f)
    {
        if (!enableScreenShake) return;

        shakeTotalDuration = Mathf.Max(0.01f, duration);
        shakeTimer = shakeTotalDuration;
        shakeMagnitude = magnitude * shakeIntensityMultiplier;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTargetIfNeeded();
            if (target == null) return;
        }

        if (!basePosInitialized)
        {
            baseCameraPosition = transform.position;
            basePosInitialized = true;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0.0001f) dt = Time.deltaTime;

        Vector3 desiredPos = target.position + offset;

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

        float effectiveSmoothTime = isFocusing ? smoothTime * 0.55f : smoothTime;
        baseCameraPosition = Vector3.SmoothDamp(
            baseCameraPosition,
            desiredPos,
            ref currentVelocity,
            effectiveSmoothTime,
            Mathf.Infinity,
            dt);

        // Mikro Ekran Sarsıntısı (Screen Shake) hesapla ve uygula
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float damper = Mathf.Clamp01(shakeTimer / shakeTotalDuration); // Zamanla azalan sönümleme
            Vector2 randomCircle = Random.insideUnitCircle * (shakeMagnitude * damper);
            shakeOffset = new Vector3(randomCircle.x, randomCircle.y, 0f);
        }

        transform.position = baseCameraPosition + shakeOffset;
    }

    public void SnapToTargetImmediate()
    {
        if (target == null) return;
        currentVelocity = Vector3.zero;
        currentLookAhead = Vector3.zero;
        shakeTimer = 0f;
        Vector3 snapPos = target.position + offset;
        snapPos.z = offset.z;
        baseCameraPosition = snapPos;
        transform.position = snapPos;
    }
}