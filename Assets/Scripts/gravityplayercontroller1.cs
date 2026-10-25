using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 360 derece değişen yerçekimine uyum sağlayan, Oval (Capsule) karakterin dönerken:
/// 1) Asla zeminin içine girmemesini ve havada asılı kalmamasını,
/// 2) Yerde zıplama/sekme yapmadan yağ gibi kayarak takla atmasını,
/// 3) Havada bir noktaya doğru serbest düşerken yüksek düşüş hızlarına ulaşmasını,
/// 4) Küçültülebilir Özel Ölüm Hitbox'ı (hazardHitboxScaleMultiplier) ile adil çarpışma sağlamasını,
/// 5) GTA 5 Franklin "Focus / Manevra Modu" (Shift + Zoom + Zaman Yavaşlatma + Enerji Barı),
/// 6) Havalı Kuyruk İzi (Trail Renderer) + Shift Modunda Hayalet İzi (Afterimage) ve Vignette,
/// 7) ÖZEL PIXEL ART OYUN İÇİ HUD (Canlı 360° Yerçekimi Pusulası, Pahlı Odak Barı, Hız/Ölüm/Süre Göstergeleri) içeren 2D Karakter Kontrolcüsü.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GravityPlayerController : MonoBehaviour
{
    public enum GravityShiftMomentumMode
    {
        ResetToZero,           // 1. Anında Durdur: Eski hızı tamamen sıfırlar, yeni yöne sıfırdan düşmeye başlar.
        KeepWorldMomentum,     // 2. Gerçek Fiziksel Savrulma (Atalet): Eski hızını korur, yeni yerçekimi seni kavisli bir yörüngeyle çeker.
        RedirectSpeedToNewDown // 3. Hızı Yeni Yöne Aktar (Portal Tarzı): Ne kadar hızlı düşüyorsan, o hızın tamamını anında yeni yerçekimi yönüne çevirir!
    }

    [Header("1. Hareket ve İvmelenme (Acceleration & Momentum)")]
    [Tooltip("Yerde yürürken ulaşılabilecek maksimum hız.")]
    public float maxMoveSpeed = 12f;

    [Tooltip("Havada düşerken/süzülürken ulaşılabilecek maksimum yanal manevra hızı.")]
    public float maxAirMoveSpeed = 16f;

    [Tooltip("Yerdeyken maksimum hıza ulaşma ivmesi.")]
    public float groundAcceleration = 65f;

    [Tooltip("Yerdeyken tuşu bıraktığında durma ivmesi / sürtünme.")]
    public float groundDeceleration = 70f;

    [Tooltip("Havadaki (düşerken) hızlanma ivmesi.")]
    public float airAcceleration = 55f;

    [Tooltip("Havada tuşu bıraktığında yatay momentumun ne kadar korunacağı (0 = havada hiç yavaşlamaz).")]
    public float airDeceleration = 8f;

    [Header("2. Zıplama ve Hızlı Serbest Düşüş (Fast Fall)")]
    [Tooltip("Zıplama kuvveti sabit 24. Sahne veya prefab değeri bunu değiştirmez.")]
    public const float FixedJumpForce = 24f;
    public float jumpForce = FixedJumpForce;

    [Tooltip("Zıplama tolerans süresi (saniye).")]
    public float jumpCoyoteTime = 0.2f;

    [Tooltip("Havada serbest düşerken ulaşılabilecek MAKSİMUM düşüş hızı (Terminal Velocity).")]
    public float maxFallSpeed = 48f;

    [Tooltip("Karakter havada düşerken yerçekimini kaç kat daha güçlü hissetsin? (1 = normal, 2.6 = 2.6 kat daha hızlı ivmelenerek düşer!).")]
    public float fallGravityMultiplier = 2.6f;

    [Tooltip("Küre bir duvara çarpıp yerçekimi yönü değiştiği anda karaktere yeni düşüş yönünde verilecek ANINDA başlangıç fırlama hızı.")]
    public float gravityShiftInitialFallBoost = 16f;

    [Header("3. Yerçekimi Değişiminde Momentum Davranışı")]
    [Tooltip("Küre duvara çarpıp yerçekimi değiştiğinde karakterin mevcut hızına ne olsun?")]
    public GravityShiftMomentumMode momentumMode = GravityShiftMomentumMode.KeepWorldMomentum;

    [Tooltip("KeepWorldMomentum veya RedirectSpeedToNewDown seçiliyse, geçiş anında eski hızın yüzde kaçı korunsun?")]
    [Range(0f, 2f)]
    public float momentumRetentionMultiplier = 0.9f;

    [Header("4. Zemin Kontrolü ve Yüzeye Hizalanma (R / Sağ Tık)")]
    [Tooltip("İsteğe bağlı alt nokta.")]
    public Transform groundCheckPoint;

    [Tooltip("Zemini algılama ekstra mesafesi.")]
    public float groundCheckExtraDistance = 0.25f;

    [Tooltip("Zemin olarak kabul edilecek katman (Layer).")]
    public LayerMask groundLayer;

    [Tooltip("R veya Sağ Tık basıldığında karakterin temas ettiği / en yakın olduğu yüzeyi bulma menzili.")]
    public float surfaceAlignCastDistance = 2.5f;

    [Header("5. Fırlatma (Throwable) Ayarları")]
    [Tooltip("Fırlatılacak mermi Prefab'ı (GravityProjectile içeren obje).")]
    public GravityProjectile projectilePrefab;

    [Tooltip("Merminin çıkış noktası (karakterin eli veya merkezi).")]
    public Transform throwOrigin;

    [Tooltip("Merminin uçuş hızını doğrudan buradan çarpan olarak değiştirebilirsin.")]
    public float projectileSpeedMultiplier = 1.6f;

    [Tooltip("Karakterin kendi hareket hızı fırlatılan mermiye eklensin mi?")]
    public bool inheritPlayerMomentumOnThrow = true;

    [Tooltip("Havada yere değmeden kaç kez fırlatma yapılabilir? (0 = Sınırsız)")]
    public int maxAirThrows = 0;

    [Header("6. Kusursuz Oval Takla & Dinamik Collider Ayarları")]
    [Tooltip("Açık olursa karakter yüzeyde hareket ederken takla atarak döner ve CircleCollider2D yarıçapı ovalin o anki açısına göre otomatik büyüyüp küçülür!")]
    public bool rollVisuallyLikeBall = true;

    [Tooltip("Takla atma hızı çarpanı.")]
    public float tumbleSpeedMultiplier = 1.15f;

    [Tooltip("Oval kapsülün en dar yarıçapı (Yan yatıkken yere olan mesafe - Standart Unity Kapsülü için 0.5).")]
    public float ovalHalfWidth = 0.5f;

    [Tooltip("Oval kapsülün en uzun yarıçapı (Dik dururken yere olan mesafe - Standart Unity Kapsülü için 1.0).")]
    public float ovalHalfHeight = 1.0f;

    [Tooltip("Roll Visually Like Ball kapalıysa, karakterin yeni yerçekimi yönüne dönme hızı.")]
    public float rotationSmoothSpeed = 18f;

    [Header("7. Ölüm Hitbox Küçültme (Hazard) ve Yeniden Doğma (Respawn)")]
    [Tooltip("ÖLÜM HITBOX KÜÇÜLTME ORANI: 1.0 = Fiziksel boyutla aynı, 0.45 = Karakterin ölüm hitbox'ı %55 daha küçüktür!")]
    [Range(0.1f, 1f)]
    public float hazardHitboxScaleMultiplier = 0.45f;

    [Tooltip("İsteğe bağlı: Özel bir doğma noktası belirlemek istersen buraya sürükle. Boş bırakırsan oyuna başladığın ilk konumu otomatik kaydeder!")]
    public Transform customSpawnPoint;

    [Tooltip("İsteğe bağlı: Eğer öldürücü duvarlara özel bir Layer (örn. Hazard) verirsen buradan seçebilirsin (veya öldürecek objelere Tag = Respawn ya da HazardZone scripti ekleyebilirsin).")]
    public LayerMask hazardLayer;

    [Header("8. Franklin Odak / Hava Manevra Modu (Shift + Zoom + Bar)")]
    [Tooltip("Shift tuşuyla çalışan Franklin tarzı Hava Manevra & Odak özelliği aktif olsun mu?")]
    public bool enableFocusAbility = true;

    [Tooltip("Barın maksimum kapasitesi (Örn: 100 birim). Yere değdiğin an otomatik olarak tamamen dolar!")]
    public float maxFocusEnergy = 100f;

    [Tooltip("Shift'e basılı tutarken bar saniyede kaç birim tükensin?")]
    public float focusDrainRatePerSecond = 45f;

    [Tooltip("Shift'e basıldığında oyun zamanı ne kadar yavaşlasın? (Örn: 0.35 = %65 yavaşlatma).")]
    [Range(0.05f, 1f)]
    public float focusTimeScale = 0.35f;

    [Tooltip("Shift'e basıldığında kamera ne kadar yakınlaşsın (Zoom)? (0.78 = %22 yakınlaşır, 1 = Zoom yapmaz).")]
    [Range(0.4f, 1f)]
    public float focusCameraZoomMultiplier = 0.78f;

    [Tooltip("Kamera Zoom geçiş yumuşaklığı hızı.")]
    public float focusZoomLerpSpeed = 8f;

    [Tooltip("FRANKLIN ETKİSİ: Shift basılıyken havadaki sağa/sola manevra ivmesi (Air Acceleration) kaç kat artsın?")]
    public float focusAirAccelerationMultiplier = 3.5f;

    [Tooltip("FRANKLIN ETKİSİ: Shift basılıyken havadaki maksimum yanal manevra hızı (Max Air Move Speed) kaç kat artsın?")]
    public float focusAirSpeedMultiplier = 1.45f;

    [Tooltip("Shift basılıyken düşüş hızı biraz frenlensin mi?")]
    [Range(0.2f, 1f)]
    public float focusFallSpeedDamping = 0.7f;

    [Tooltip("Ekranda Odak/Manevra Barı ve Pixel Art Oyun İçi HUD gösterilsin mi?")]
    public bool showFocusBarUI = true;

    [Header("9. Havalı Kuyruk İzi (Trail Effect) Ayarları")]
    [Tooltip("Karakterin arkasında havalı ışık izi (Trail) çıksın mı?")]
    public bool enablePlayerTrail = true;

    [Tooltip("İz sadece havada süzülürken/düşerken mi çıksın (True), yoksa yerde yürürken de hep açık mı kalsın (False)?")]
    public bool trailOnlyInAir = false;

    [Tooltip("İzin ekranda kalma süresi / uzunluğu (Saniye - Örn: 0.35).")]
    [Range(0.05f, 1.5f)]
    public float trailDuration = 0.35f;

    [Tooltip("İzin başlangıç kalınlığı (Karakterin arkasından çıkarkenki genişliği).")]
    [Range(0.1f, 2f)]
    public float trailStartWidth = 0.75f;

    [Tooltip("Normal uçuş/hareket sırasındaki iz rengi (Varsayılan: Parlak Neon Yeşil/Turkuaz).")]
    public Color normalTrailColor = new Color(0.2f, 1f, 0.55f, 0.85f);

    [Tooltip("Shift (Franklin Focus) basılıyken izin dönüşeceği renk (Varsayılan: Parlak Altın Sarısı!).")]
    public Color focusTrailColor = new Color(1f, 0.85f, 0.15f, 0.95f);

    [Header("10. Shift Modu: Hayalet İzi (Afterimage) & Sinematik Vignette")]
    [Tooltip("Shift (Focus) basılıyken karakterin arkasında Cyberpunk/Celeste tarzı yarı saydam Hayalet Kopyalar (Afterimage) bıraksın mı?")]
    public bool enableFocusAfterimages = true;

    [Tooltip("Hayalet kopyaların çıkma sıklığı (Gerçek saniye - Örn: 0.045 sn'de bir kopya bırakır).")]
    [Range(0.015f, 0.2f)]
    public float afterimageSpawnInterval = 0.045f;

    [Tooltip("Her bir hayalet kopyanın ekranda eriyerek kaybolma süresi (Gerçek saniye).")]
    [Range(0.1f, 1f)]
    public float afterimageFadeDuration = 0.28f;

    [Tooltip("Hayalet kopyaların rengi (Varsayılan: Parlak Turkuaz/Altın karışımı).")]
    public Color afterimageColor = new Color(0.2f, 1f, 0.9f, 0.65f);

    [Tooltip("Shift (Focus) basılıyken ekranın kenarlarında sinematik kararma ve odak atmosferi (Vignette) oluşsun mu?")]
    public bool enableFocusScreenVignette = true;

    [Tooltip("Ekran kenarı kararma (Vignette) yoğunluğu.")]
    [Range(0f, 1f)]
    public float vignetteIntensity = 0.65f;

    [Header("11. Pixel Art Oyun İçi HUD (Pusula + Odak Barı + Sayaçlar)")]
    [Tooltip("İndirdiğin Pixelify Sans (.ttf) fontunu buraya da sürükle ki oyun içi HUD ana menüyle birebir aynı piksel fontta olsun!")]
    public Font hudPixelFont;

    [Tooltip("Sol üstte 360° dönen Canlı Yerçekimi Pusulası ve Düşüş Hızı (km/s) gösterilsin mi?")]
    public bool showGravityCompassHUD = true;

    [Tooltip("Sağ üstte Süre (Timer) ve Ölüm Sayacı (Death Counter) rozeti gösterilsin mi?")]
    public bool showSpeedrunStatsHUD = true;

    [Tooltip("Ekranın sol altında mini kontrol ipucu rozetleri ([R] Hizala, [SHIFT] Odak) gösterilsin mi?")]
    public bool showControlHintsHUD = true;

    public bool IsGrounded => isGrounded;
    public int CompletedJumpCount => completedJumpCount;
    public int CurrentCheckpointIndex => currentCheckpointIndex;
    public Vector3 CurrentRespawnPosition => currentCheckpointIndex > 0 ? checkpointSpawnPosition :
        (customSpawnPoint != null ? customSpawnPoint.position : initialSpawnPosition);
    public bool IsFocusActive => isFocusActive;
    public float CurrentFocusEnergyNormalized => maxFocusEnergy > 0f ? Mathf.Clamp01(currentFocusEnergy / maxFocusEnergy) : 0f;
    public int DeathCount => deathCount;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private CircleCollider2D circleCollider;
    private SpriteRenderer playerSpriteRenderer;
    private PhysicsMaterial2D zeroFrictionBounceMat;
    private TrailRenderer playerTrail;
    private Gradient normalTrailGradient;
    private Gradient focusTrailGradient;
    private Camera mainCam;
    private Vector2 moveInput;
    private bool isGrounded;
    private float lastGroundedTime = -10f;
    private float lastJumpTime = -10f;
    private bool tutorialJumpInProgress;
    private bool tutorialJumpLeftGround;
    private int completedJumpCount;
    private int currentAirThrowsUsed = 0;
    private Quaternion targetRotation = Quaternion.identity;

    private Vector2 lastContactSurfaceNormal = Vector2.up;
    private bool hasActiveSurfaceContact = false;
    private bool isAligningToSurface = false;

    private float currentFocusEnergy;
    private float smoothDisplayedFocusRatio = 1f;
    private bool isFocusActive = false;
    private float defaultFixedDeltaTime = 0.02f;
    private float baseCameraOrthoSize = -1f;
    private Texture2D uiWhiteTex;
    private Texture2D vignetteTex;
    private Texture2D compassArrowTex;
    private Texture2D compassRingTex;
    private float currentVignetteAlpha = 0f;
    private float afterimageTimer = 0f;

    // HUD İstatistikleri ve Smooth Pusula Açısı
    private int deathCount = 0;
    private float runElapsedTime = 0f;
    private float smoothCompassAngle = -90f;
    private float alignFlashTimer = 0f;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];
    private readonly Collider2D[] hazardOverlapHits = new Collider2D[8];
    private int currentCheckpointIndex;
    private Vector3 checkpointSpawnPosition;
    private int lastRespawnFrame = -1;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;

    private void OnValidate()
    {
        jumpForce = FixedJumpForce;
    }

    private void Awake()
    {
        jumpForce = FixedJumpForce;
        rb = GetComponent<Rigidbody2D>();
        playerSpriteRenderer = GetComponent<SpriteRenderer>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        defaultFixedDeltaTime = Time.fixedDeltaTime > 0.001f ? Time.fixedDeltaTime : 0.02f;
        currentFocusEnergy = maxFocusEnergy;

        zeroFrictionBounceMat = new PhysicsMaterial2D("ZeroFrictionNoBounce")
        {
            friction = 0f,
            bounciness = 0f
        };
        rb.sharedMaterial = zeroFrictionBounceMat;

        SetupSmoothDynamicCircleCollider();
        SetupPlayerTrailRenderer();
        GenerateVignetteTexture();
        GenerateHUDTextures();

        if (TryGetComponent<LineRenderer>(out LineRenderer oldLine))
        {
            oldLine.enabled = false;
        }

        mainCam = Camera.main;
        if (mainCam != null && mainCam.orthographic)
        {
            baseCameraOrthoSize = mainCam.orthographicSize;
        }

        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;
    }

    private void SetupSmoothDynamicCircleCollider()
    {
        circleCollider = GetComponent<CircleCollider2D>();

        CapsuleCollider2D cap = GetComponent<CapsuleCollider2D>();
        if (cap != null)
        {
            ovalHalfWidth = cap.size.x * 0.5f;
            ovalHalfHeight = cap.size.y * 0.5f;
            Destroy(cap);
        }

        if (circleCollider == null)
        {
            circleCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        circleCollider.sharedMaterial = zeroFrictionBounceMat;
        playerCollider = circleCollider;
    }

    private void SetupPlayerTrailRenderer()
    {
        playerTrail = GetComponent<TrailRenderer>();
        if (playerTrail == null)
        {
            playerTrail = gameObject.AddComponent<TrailRenderer>();
        }

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            playerTrail.material = new Material(spriteShader);
        }

        if (playerSpriteRenderer != null)
        {
            playerTrail.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            playerTrail.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
        }

        playerTrail.time = trailDuration;
        playerTrail.minVertexDistance = 0.08f;
        playerTrail.numCornerVertices = 5;
        playerTrail.numCapVertices = 5;

        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, trailStartWidth);
        widthCurve.AddKey(0.4f, trailStartWidth * 0.55f);
        widthCurve.AddKey(1f, 0f);
        playerTrail.widthCurve = widthCurve;

        RebuildTrailGradients();
        playerTrail.colorGradient = normalTrailGradient;
        playerTrail.emitting = enablePlayerTrail;
    }

    private void RebuildTrailGradients()
    {
        normalTrailGradient = CreateFadeGradient(normalTrailColor);
        focusTrailGradient = CreateFadeGradient(focusTrailColor);
    }

    private Gradient CreateFadeGradient(Color baseColor)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(baseColor, 0f),
                new GradientColorKey(Color.Lerp(baseColor, Color.white, 0.25f), 0.3f),
                new GradientColorKey(baseColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(baseColor.a, 0f),
                new GradientAlphaKey(baseColor.a * 0.45f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return grad;
    }

    private void GenerateVignetteTexture()
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
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1.05f, dist));
                vignetteTex.SetPixel(x, y, new Color(0.02f, 0.05f, 0.1f, alpha));
            }
        }
        vignetteTex.Apply();
    }

    /// <summary>
    /// HUD üzerindeki 360° Yerçekimi Pusulası için keskin Pixel Art Ok (Arrow) ve Çember (Ring) dokularını kodla üretir!
    /// </summary>
    private void GenerateHUDTextures()
    {
        uiWhiteTex = new Texture2D(1, 1);
        uiWhiteTex.filterMode = FilterMode.Point;
        uiWhiteTex.SetPixel(0, 0, Color.white);
        uiWhiteTex.Apply();

        // 1. Pixel Art Pusula Çemberi (32x32)
        int ringSize = 32;
        compassRingTex = new Texture2D(ringSize, ringSize, TextureFormat.RGBA32, false);
        compassRingTex.filterMode = FilterMode.Point;
        Vector2 rCenter = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < ringSize; y++)
        {
            for (int x = 0; x < ringSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), rCenter);
                if (d >= 12.2f && d <= 14.8f)
                {
                    compassRingTex.SetPixel(x, y, new Color(0.28f, 0.95f, 0.82f, 0.9f));
                }
                else if (d < 12.2f)
                {
                    compassRingTex.SetPixel(x, y, new Color(0.04f, 0.07f, 0.14f, 0.85f));
                }
                else
                {
                    compassRingTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        compassRingTex.Apply();

        // 2. Sağa (+X) bakan keskin Pixel Art Pusula Oku (24x24) -> GUIUtility.RotateAroundPivot ile 360° döner!
        int arrSize = 24;
        compassArrowTex = new Texture2D(arrSize, arrSize, TextureFormat.RGBA32, false);
        compassArrowTex.filterMode = FilterMode.Point;
        for (int y = 0; y < arrSize; y++)
        {
            for (int x = 0; x < arrSize; x++)
            {
                compassArrowTex.SetPixel(x, y, Color.clear);

                int dy = Mathf.Abs(y - 12);
                // Ok gövdesi (Sol-orta)
                if (x >= 4 && x <= 13 && dy <= 2)
                {
                    compassArrowTex.SetPixel(x, y, new Color(0.25f, 1f, 0.82f, 1f));
                }
                // Ok ucu (Sağa doğru sivrilen üçgen)
                if (x >= 12 && x <= 20 && dy <= (20 - x))
                {
                    compassArrowTex.SetPixel(x, y, new Color(1f, 0.9f, 0.3f, 1f)); // Altın sarısı uç!
                }
            }
        }
        compassArrowTex.Apply();
    }

    private void UpdatePlayerTrailEffect()
    {
        if (playerTrail == null) return;

        if (!enablePlayerTrail)
        {
            playerTrail.emitting = false;
            return;
        }

        playerTrail.time = trailDuration;
        playerTrail.widthMultiplier = 1f;

        bool shouldEmit = trailOnlyInAir ? !isGrounded : (rb.linearVelocity.sqrMagnitude > 0.5f || !isGrounded);
        playerTrail.emitting = shouldEmit;

        RebuildTrailGradients();
        playerTrail.colorGradient = isFocusActive ? focusTrailGradient : normalTrailGradient;
    }

    private void UpdateAfterimageTrail()
    {
        if (!enableFocusAfterimages || !isFocusActive || playerSpriteRenderer == null || playerSpriteRenderer.sprite == null)
        {
            afterimageTimer = 0f;
            return;
        }

        afterimageTimer += Time.unscaledDeltaTime;
        if (afterimageTimer >= afterimageSpawnInterval)
        {
            afterimageTimer = 0f;
            SpawnSingleAfterimage();
        }
    }

    private void SpawnSingleAfterimage()
    {
        GameObject ghostObj = new GameObject("PlayerAfterimage");
        ghostObj.transform.position = transform.position;
        ghostObj.transform.rotation = transform.rotation;
        ghostObj.transform.localScale = transform.lossyScale;

        SpriteRenderer ghostSr = ghostObj.AddComponent<SpriteRenderer>();
        ghostSr.sprite = playerSpriteRenderer.sprite;
        ghostSr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        ghostSr.sortingOrder = playerSpriteRenderer.sortingOrder - 2;
        ghostSr.color = afterimageColor;

        StartCoroutine(FadeAndDestroyAfterimage(ghostSr, afterimageFadeDuration));
    }

    private IEnumerator FadeAndDestroyAfterimage(SpriteRenderer ghostSr, float duration)
    {
        float elapsed = 0f;
        Color startColor = ghostSr.color;
        Vector3 startScale = ghostSr.transform.localScale;

        while (elapsed < duration && ghostSr != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            ghostSr.color = c;

            ghostSr.transform.localScale = Vector3.Lerp(startScale, startScale * 0.85f, t);
            yield return null;
        }

        if (ghostSr != null)
        {
            Destroy(ghostSr.gameObject);
        }
    }

    private void OnEnable()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged += HandleGravityChanged;
        }
    }

    private void Start()
    {
        if (customSpawnPoint != null)
        {
            initialSpawnPosition = customSpawnPoint.position;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null && mainCam.orthographic && baseCameraOrthoSize <= 0f)
        {
            baseCameraOrthoSize = mainCam.orthographicSize;
        }

        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged -= HandleGravityChanged;
            GravityManager.Instance.OnGravityChanged += HandleGravityChanged;
            UpdateTargetRotation(GravityManager.Instance.CurrentGravityDirection);
        }
    }

    private void OnDisable()
    {
        SetFocusAbilityState(false);

        if (mainCam != null && mainCam.orthographic && baseCameraOrthoSize > 0f)
        {
            mainCam.orthographicSize = baseCameraOrthoSize;
        }

        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged -= HandleGravityChanged;
        }
    }

    private void Update()
    {
        runElapsedTime += Time.deltaTime;
        if (alignFlashTimer > 0f) alignFlashTimer -= Time.unscaledDeltaTime;

        CheckGrounded();
        CheckShrunkHazardHitbox();
        UpdateJumpTutorialProgress();

        ReadPlayerInputs(
            out bool jumpPressed,
            out bool firePressed,
            out bool shiftHeld,
            out bool alignSurfacePressed,
            out Vector2 mouseScreenPos);

        if (alignSurfacePressed)
        {
            AlignGravityToCurrentSurface();
        }

        bool canJump = isGrounded || (Time.time - lastGroundedTime <= jumpCoyoteTime && Time.time - lastJumpTime > 0.22f);
        if (jumpPressed && canJump)
        {
            Jump();
        }

        if (firePressed)
        {
            TryThrowProjectile(mouseScreenPos);
        }

        HandleFocusAbilityAndCameraZoom(shiftHeld);
        UpdateVisualTumbleAndDynamicCollider();
        UpdatePlayerTrailEffect();
        UpdateAfterimageTrail();

        float targetVig = (isFocusActive && enableFocusScreenVignette) ? vignetteIntensity : 0f;
        currentVignetteAlpha = Mathf.MoveTowards(currentVignetteAlpha, targetVig, Time.unscaledDeltaTime * 5f);

        // HUD Bar ve Pusula yumuşak geçişleri
        smoothDisplayedFocusRatio = Mathf.Lerp(smoothDisplayedFocusRatio, CurrentFocusEnergyNormalized, Time.unscaledDeltaTime * 14f);

        Vector2 gravDir = GravityManager.Instance != null ? GravityManager.Instance.CurrentGravityDirection : Vector2.down;
        float targetGravAngle = Mathf.Atan2(-gravDir.y, gravDir.x) * Mathf.Rad2Deg; // GUI koordinatında Y aşağı pozitiftir
        smoothCompassAngle = Mathf.LerpAngle(smoothCompassAngle, targetGravAngle, Time.unscaledDeltaTime * 16f);
    }

    private float GetLethalHitboxRadiusWorld()
    {
        float baseRadius = ovalHalfWidth * Mathf.Clamp(hazardHitboxScaleMultiplier, 0.1f, 1f);
        float maxScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        return baseRadius * maxScale;
    }

    private void CheckShrunkHazardHitbox()
    {
        float lethalRadius = GetLethalHitboxRadiusWorld();

        ContactFilter2D filter = ContactFilter2D.noFilter;
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(transform.position, lethalRadius, filter, hazardOverlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = hazardOverlapHits[i];
            if (col != null && col.gameObject != gameObject && IsHazardObject(col.gameObject))
            {
                DieAndRespawn();
                return;
            }
        }
    }

    public bool IsColliderTouchingLethalCore(Collider2D hazardCollider)
    {
        if (hazardCollider == null) return false;
        float lethalRadius = GetLethalHitboxRadiusWorld();
        Vector2 closestPoint = hazardCollider.ClosestPoint(transform.position);
        float distSq = ((Vector2)transform.position - closestPoint).sqrMagnitude;
        return distSq <= (lethalRadius * lethalRadius);
    }

    private void HandleFocusAbilityAndCameraZoom(bool shiftHeld)
    {
        if (isGrounded)
        {
            currentFocusEnergy = maxFocusEnergy;
        }

        bool wantsAndCanFocus = enableFocusAbility && shiftHeld && !isGrounded && currentFocusEnergy > 0.01f;

        if (wantsAndCanFocus)
        {
            SetFocusAbilityState(true);

            currentFocusEnergy -= focusDrainRatePerSecond * Time.unscaledDeltaTime;
            if (currentFocusEnergy <= 0f)
            {
                currentFocusEnergy = 0f;
                SetFocusAbilityState(false);
            }
        }
        else
        {
            if (isFocusActive)
            {
                SetFocusAbilityState(false);
            }
        }

        UpdateCameraZoomEffect();
    }

    public void SetBulletTimeState(bool active)
    {
        SetFocusAbilityState(active);
    }

    public void SetFocusAbilityState(bool active)
    {
        if (active)
        {
            isFocusActive = true;
            float targetScale = Mathf.Clamp(focusTimeScale, 0.05f, 1f);
            Time.timeScale = targetScale;
            Time.fixedDeltaTime = defaultFixedDeltaTime * targetScale;
        }
        else
        {
            if (isFocusActive)
            {
                isFocusActive = false;
                Time.timeScale = 1f;
                Time.fixedDeltaTime = defaultFixedDeltaTime;
            }
        }
    }

    private void UpdateCameraZoomEffect()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || !mainCam.orthographic) return;

        if (!isFocusActive && Mathf.Abs(mainCam.orthographicSize - baseCameraOrthoSize) > 0.01f && baseCameraOrthoSize <= 0f)
        {
            baseCameraOrthoSize = mainCam.orthographicSize;
        }

        if (baseCameraOrthoSize <= 0f) return;

        float targetOrthoSize = isFocusActive
            ? baseCameraOrthoSize * focusCameraZoomMultiplier
            : baseCameraOrthoSize;

        mainCam.orthographicSize = Mathf.Lerp(
            mainCam.orthographicSize,
            targetOrthoSize,
            Time.unscaledDeltaTime * focusZoomLerpSpeed);
    }

    private void UpdateVisualTumbleAndDynamicCollider()
    {
        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection.normalized
            : Vector2.down;
        Vector2 upDir = -downDir;
        Vector2 surfaceTangent = new Vector2(upDir.y, -upDir.x);

        if (rollVisuallyLikeBall)
        {
            float tangentSpeed = Vector2.Dot(rb.linearVelocity, surfaceTangent);
            if (Mathf.Abs(tangentSpeed) > 0.05f)
            {
                float angularDelta = -(tangentSpeed / 0.65f) * tumbleSpeedMultiplier * Mathf.Rad2Deg * Time.deltaTime;
                transform.Rotate(0f, 0f, angularDelta);
            }
        }
        else
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
        }

        if (circleCollider != null)
        {
            float cosTheta = Mathf.Abs(Vector2.Dot(transform.up, downDir));
            float capsuleRadius = ovalHalfWidth;
            float cylinderHalfLength = Mathf.Max(0f, ovalHalfHeight - ovalHalfWidth);

            float exactGroundDistance = capsuleRadius + (cylinderHalfLength * cosTheta);

            circleCollider.radius = exactGroundDistance;
            circleCollider.offset = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        ApplyMovementAndTerminalVelocity();
    }

    private void ReadPlayerInputs(
        out bool jumpPressed,
        out bool firePressed,
        out bool shiftHeld,
        out bool alignSurfacePressed,
        out Vector2 mouseScreenPos)
    {
        moveInput = Vector2.zero;
        jumpPressed = false;
        firePressed = false;
        shiftHeld = false;
        alignSurfacePressed = false;
        mouseScreenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            float left = (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) ? -1f : 0f;
            float right = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) ? 1f : 0f;
            float down = (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) ? -1f : 0f;
            float up = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) ? 1f : 0f;

            moveInput = new Vector2(left + right, down + up);
            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
            shiftHeld = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                alignSurfacePressed = true;
            }
        }

        if (Mouse.current != null)
        {
            firePressed = Mouse.current.leftButton.wasPressedThisFrame;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                alignSurfacePressed = true;
            }

            mouseScreenPos = Mouse.current.position.ReadValue();
        }
#else
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(h, v);
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }

        jumpPressed = Input.GetButtonDown("Jump");
        firePressed = Input.GetMouseButtonDown(0);
        shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        alignSurfacePressed = Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(1);
        mouseScreenPos = Input.mousePosition;
#endif
    }

    private void CheckGrounded()
    {
        if (Time.time - lastJumpTime < 0.08f)
        {
            isGrounded = false;
            return;
        }

        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection.normalized
            : Vector2.down;

        bool hitGround = false;

        if (playerCollider != null)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;

            int hitCount = playerCollider.Cast(downDir, filter, castHits, groundCheckExtraDistance);
            if (hitCount > 0)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    if (!IsHazardObject(castHits[i].collider.gameObject))
                    {
                        hitGround = true;
                        lastContactSurfaceNormal = castHits[i].normal.normalized;
                        break;
                    }
                }
            }
        }

        if (hasActiveSurfaceContact)
        {
            hitGround = true;
        }

        isGrounded = hitGround;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            currentAirThrowsUsed = 0;
            currentFocusEnergy = maxFocusEnergy;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsHazardObject(collision.gameObject))
        {
            if (IsColliderTouchingLethalCore(collision.collider))
            {
                DieAndRespawn();
            }
            else
            {
                collision.collider.isTrigger = true;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsHazardObject(other.gameObject) && IsColliderTouchingLethalCore(other))
        {
            DieAndRespawn();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsHazardObject(other.gameObject) && IsColliderTouchingLethalCore(other))
        {
            DieAndRespawn();
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsHazardObject(collision.gameObject))
        {
            if (IsColliderTouchingLethalCore(collision.collider))
            {
                DieAndRespawn();
            }
            else
            {
                collision.collider.isTrigger = true;
            }
            return;
        }

        if (Time.time - lastJumpTime < 0.08f) return;

        if (((1 << collision.gameObject.layer) & groundLayer) != 0 && collision.contactCount > 0)
        {
            Vector2 currentDown = GravityManager.Instance != null
                ? GravityManager.Instance.CurrentGravityDirection
                : Vector2.down;

            Vector2 bestNormal = collision.GetContact(0).normal;
            float bestDot = Vector2.Dot(bestNormal, -currentDown);

            for (int i = 1; i < collision.contactCount; i++)
            {
                Vector2 n = collision.GetContact(i).normal;
                float d = Vector2.Dot(n, -currentDown);
                if (d > bestDot)
                {
                    bestDot = d;
                    bestNormal = n;
                }
            }

            if (bestDot > 0.35f)
            {
                lastContactSurfaceNormal = bestNormal.normalized;
                hasActiveSurfaceContact = true;
                lastGroundedTime = Time.time;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            hasActiveSurfaceContact = false;
        }
    }

    private bool IsHazardObject(GameObject obj)
    {
        if (obj == null) return false;

        if (obj.GetComponent<HazardZone>() != null)
        {
            return true;
        }

        if (((1 << obj.layer) & hazardLayer) != 0)
        {
            return true;
        }

        if (obj.CompareTag("Respawn"))
        {
            return true;
        }

        return false;
    }

    public bool TryActivateCheckpoint(int index, Vector3 spawnPosition)
    {
        // Old stones cannot roll progress back; passing them in midair does not save.
        if (index <= currentCheckpointIndex || !isGrounded) return false;
        currentCheckpointIndex = index;
        checkpointSpawnPosition = spawnPosition;
        return true;
    }

    public void DieAndRespawn()
    {
        // Physics can report several hazards from the old location in a single frame.
        if (lastRespawnFrame == Time.frameCount) return;
        lastRespawnFrame = Time.frameCount;
        tutorialJumpInProgress = false;
        tutorialJumpLeftGround = false;
        deathCount++;
        SetFocusAbilityState(false);
        currentFocusEnergy = maxFocusEnergy;
        currentVignetteAlpha = 0f;

        if (mainCam != null && mainCam.orthographic && baseCameraOrthoSize > 0f)
        {
            mainCam.orthographicSize = baseCameraOrthoSize;
        }

        GravityProjectile[] activeProjectiles = FindObjectsByType<GravityProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < activeProjectiles.Length; i++)
        {
            if (activeProjectiles[i] != null)
            {
                Destroy(activeProjectiles[i].gameObject);
            }
        }

        if (playerTrail != null)
        {
            playerTrail.emitting = false;
            playerTrail.Clear();
        }

        Vector3 spawnPos = CurrentRespawnPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = spawnPos;
        transform.position = spawnPos;
        // Both rare stones stand on horizontal, hazard-free floor.
        transform.rotation = currentCheckpointIndex > 0 ? Quaternion.identity : initialSpawnRotation;
        targetRotation = transform.rotation;
        lastGroundedTime = -10f;
        lastJumpTime = -10f;
        lastContactSurfaceNormal = Vector2.up;

        if (playerTrail != null)
        {
            playerTrail.Clear();
            playerTrail.emitting = enablePlayerTrail;
        }

        isGrounded = false;
        hasActiveSurfaceContact = false;
        currentAirThrowsUsed = 0;

        if (GravityManager.Instance != null)
        {
            isAligningToSurface = true;
            GravityManager.Instance.SetGravityDirection(Vector2.down);
            isAligningToSurface = false;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null && mainCam.TryGetComponent<CameraFollow2D>(out CameraFollow2D camFollow))
        {
            camFollow.SnapToTargetImmediate();
        }
    }

    private void AlignGravityToCurrentSurface()
    {
        if (GravityManager.Instance == null) return;

        Vector2 targetSurfaceNormal = Vector2.zero;
        bool foundSurface = false;

        if ((hasActiveSurfaceContact || isGrounded) && lastContactSurfaceNormal.sqrMagnitude > 0.001f)
        {
            targetSurfaceNormal = lastContactSurfaceNormal;
            foundSurface = true;
        }
        else
        {
            Vector2 currentDown = GravityManager.Instance.CurrentGravityDirection;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, currentDown, surfaceAlignCastDistance, groundLayer);

            if (hit.collider != null && hit.normal.sqrMagnitude > 0.001f)
            {
                targetSurfaceNormal = hit.normal;
                foundSurface = true;
            }
        }

        if (foundSurface)
        {
            Vector2 newGravityDir = -targetSurfaceNormal.normalized;

            isAligningToSurface = true;
            GravityManager.Instance.SetGravityDirection(newGravityDir);
            isAligningToSurface = false;

            rb.linearVelocity = Vector2.zero;
            currentAirThrowsUsed = 0;
            currentFocusEnergy = maxFocusEnergy;
            alignFlashTimer = 0.45f; // HUD'da "YÜZEYE HİZALANDI" parlaması

            transform.rotation = targetRotation;
        }
    }

    private void ApplyMovementAndTerminalVelocity()
    {
        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection.normalized
            : Vector2.down;

        Vector2 upDir = -downDir;
        Vector2 surfaceTangent = new Vector2(upDir.y, -upDir.x);

        float tangentInput = Vector2.Dot(moveInput, surfaceTangent);
        if (Mathf.Abs(tangentInput) > 0.05f)
        {
            tangentInput = Mathf.Sign(tangentInput) * moveInput.magnitude;
        }
        else
        {
            tangentInput = 0f;
        }

        if (Mathf.Abs(tangentInput) > 0.01f && playerCollider != null)
        {
            Vector2 moveDir = surfaceTangent * Mathf.Sign(tangentInput);
            ContactFilter2D wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(groundLayer);
            wallFilter.useTriggers = false;

            int wallHits = playerCollider.Cast(moveDir, wallFilter, castHits, 0.08f);
            for (int i = 0; i < wallHits; i++)
            {
                if (!IsHazardObject(castHits[i].collider.gameObject) && Vector2.Dot(castHits[i].normal, -moveDir) > 0.7f)
                {
                    tangentInput = 0f;
                    break;
                }
            }
        }

        Vector2 currentVel = rb.linearVelocity;
        float currentTangentSpeed = Vector2.Dot(currentVel, surfaceTangent);
        float verticalSpeed = Vector2.Dot(currentVel, upDir);

        if (isGrounded && Time.time - lastJumpTime > 0.15f && verticalSpeed > 0f)
        {
            verticalSpeed = 0f;
        }

        if (!isGrounded && fallGravityMultiplier > 1f)
        {
            float baseGravity = GravityManager.Instance != null ? GravityManager.Instance.gravityStrength : 25f;
            float extraFallAccel = baseGravity * (fallGravityMultiplier - 1f);
            verticalSpeed -= extraFallAccel * Time.fixedDeltaTime;
        }

        float currentMaxMoveSpeed = isGrounded ? maxMoveSpeed : maxAirMoveSpeed;
        float currentAirAccel = airAcceleration;
        float currentAirDecel = airDeceleration;

        if (isFocusActive && !isGrounded)
        {
            currentMaxMoveSpeed *= focusAirSpeedMultiplier;
            currentAirAccel *= focusAirAccelerationMultiplier;
            currentAirDecel *= focusAirAccelerationMultiplier;
        }

        float targetTangentSpeed = tangentInput * currentMaxMoveSpeed;

        float accelRate;
        if (Mathf.Abs(targetTangentSpeed) > 0.01f)
        {
            accelRate = isGrounded ? groundAcceleration : currentAirAccel;
        }
        else
        {
            accelRate = isGrounded ? groundDeceleration : currentAirDecel;
        }

        float newTangentSpeed = Mathf.MoveTowards(currentTangentSpeed, targetTangentSpeed, accelRate * Time.fixedDeltaTime);

        float effectiveMaxFallSpeed = (isFocusActive && !isGrounded)
            ? maxFallSpeed * focusFallSpeedDamping
            : maxFallSpeed;

        if (verticalSpeed < -effectiveMaxFallSpeed)
        {
            verticalSpeed = -effectiveMaxFallSpeed;
        }

        rb.linearVelocity = (surfaceTangent * newTangentSpeed) + (upDir * verticalSpeed);
    }

    private void UpdateJumpTutorialProgress()
    {
        if (!tutorialJumpInProgress) return;
        if (!isGrounded)
        {
            tutorialJumpLeftGround = true;
            return;
        }
        if (!tutorialJumpLeftGround) return;
        completedJumpCount++;
        tutorialJumpInProgress = false;
    }

    public void BounceFromSurface(Vector2 direction, float speed)
    {
        if (rb == null || direction.sqrMagnitude < 0.001f) return;
        Vector2 normal = direction.normalized;
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        float tangentSpeed = Vector2.Dot(rb.linearVelocity, tangent);
        lastJumpTime = Time.time;
        lastGroundedTime = -10f;
        isGrounded = false;
        hasActiveSurfaceContact = false;
        rb.linearVelocity = tangent * tangentSpeed + normal * Mathf.Max(FixedJumpForce, speed);
    }

    private void Jump()
    {
        tutorialJumpInProgress = true;
        tutorialJumpLeftGround = false;
        lastJumpTime = Time.time;
        isGrounded = false;
        hasActiveSurfaceContact = false;

        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection
            : Vector2.down;
        Vector2 upDir = -downDir;
        Vector2 surfaceTangent = new Vector2(upDir.y, -upDir.x);

        float currentTangentSpeed = Vector2.Dot(rb.linearVelocity, surfaceTangent);
        rb.linearVelocity = (surfaceTangent * currentTangentSpeed) + (upDir * FixedJumpForce);
    }

    private void TryThrowProjectile(Vector2 mouseScreenPos)
    {
        if (projectilePrefab == null) return;

        if (!isGrounded && maxAirThrows > 0 && currentAirThrowsUsed >= maxAirThrows)
        {
            return;
        }

        if (!isGrounded)
        {
            currentAirThrowsUsed++;
        }

        if (mainCam == null) mainCam = Camera.main;

        float zDist = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, zDist));
        mouseWorldPos.z = 0f;

        Vector3 spawnPos = throwOrigin != null ? throwOrigin.position : transform.position;
        Vector2 throwDir = ((Vector2)mouseWorldPos - (Vector2)spawnPos).normalized;

        if (throwDir.sqrMagnitude < 0.001f) return;

        GravityProjectile proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        proj.speed *= projectileSpeedMultiplier;
        proj.Launch(throwDir);

        if (inheritPlayerMomentumOnThrow && proj.TryGetComponent<Rigidbody2D>(out Rigidbody2D projRb))
        {
            projRb.linearVelocity += rb.linearVelocity;
        }
    }

    private void HandleGravityChanged(Vector2 newGravityDir)
    {
        UpdateTargetRotation(newGravityDir);

        if (isAligningToSurface) return;

        Vector2 downDir = newGravityDir.normalized;
        Vector2 currentVel = rb.linearVelocity;
        float currentSpeed = currentVel.magnitude;

        Vector2 resultingVel = Vector2.zero;

        switch (momentumMode)
        {
            case GravityShiftMomentumMode.ResetToZero:
                resultingVel = Vector2.zero;
                break;

            case GravityShiftMomentumMode.KeepWorldMomentum:
                resultingVel = currentVel * momentumRetentionMultiplier;
                break;

            case GravityShiftMomentumMode.RedirectSpeedToNewDown:
                resultingVel = downDir * (currentSpeed * momentumRetentionMultiplier);
                break;
        }

        if (gravityShiftInitialFallBoost > 0f)
        {
            float speedInNewDown = Vector2.Dot(resultingVel, downDir);
            if (speedInNewDown < gravityShiftInitialFallBoost)
            {
                Vector2 tangentComponent = resultingVel - (downDir * speedInNewDown);
                resultingVel = tangentComponent + (downDir * gravityShiftInitialFallBoost);
            }
        }

        rb.linearVelocity = resultingVel;
    }

    private void UpdateTargetRotation(Vector2 gravityDir)
    {
        Vector2 upDir = -gravityDir.normalized;
        float angle = Mathf.Atan2(upDir.y, upDir.x) * Mathf.Rad2Deg - 90f;
        targetRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, GetLethalHitboxRadiusWorld());
    }

    // =========================================================================
    // PIXEL ART OYUN İÇİ HUD SİSTEMİ (Ana Menü Çerçeveleriyle Birebir Uyumlu)
    // =========================================================================
    private void OnGUI()
    {
        // 1. Shift (Focus) aktifken ekran kenarı sinematik kararma/odak (Vignette)
        if (currentVignetteAlpha > 0.01f && vignetteTex != null)
        {
            GUI.color = new Color(1f, 1f, 1f, currentVignetteAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTex, ScaleMode.StretchToFill, true);
        }

        if (!showFocusBarUI) return;

        if (uiWhiteTex == null || compassRingTex == null)
        {
            GenerateHUDTextures();
        }

        if (hudPixelFont != null)
        {
            GUI.skin.font = hudPixelFont;
        }

        // A) ÜST-ORTA: PAHLİ (CHAMFERED) PIXEL ÇERÇEVELİ ODAK & MANEVRA BARI
        if (enableFocusAbility)
        {
            DrawOrnateFocusBarHUD();
        }

        // B) SOL-ÜST: CANLI 360° YERÇEKİMİ PUSULASI & ANLIK HIZ GÖSTERGESİ
        if (showGravityCompassHUD)
        {
            DrawGravityCompassAndSpeedHUD();
        }

        // C) SAĞ-ÜST: SÜRE & ÖLÜM SAYACI ROZETİ (SPEEDRUN STATS)
        if (showSpeedrunStatsHUD)
        {
            DrawSpeedrunStatsHUD();
        }

        // D) SOL-ALT: DURUMA DUYARLI MİNİ KONTROL ROZETLERİ ([R] HİZALA / [SHIFT] ODAK)
        if (showControlHintsHUD)
        {
            DrawMiniControlBadgesHUD();
        }
    }

    private void DrawOrnateFocusBarHUD()
    {
        float panelW = 310f;
        float panelH = 44f;
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = 18f;

        Color borderCol = isFocusActive
            ? new Color(1f, 0.88f, 0.25f, 1f)
            : new Color(0.28f, 0.95f, 0.82f, 0.95f);

        DrawHUDChamferedPanel(new Rect(panelX, panelY, panelW, panelH), new Color(0.05f, 0.08f, 0.15f, 0.9f), borderCol, 6f);

        // Üst etiket
        string title = isFocusActive ? "» ODAK MANEVRA AKTİF «" : "ODAK MANEVRA [SHIFT]";
        Color titleCol = isFocusActive ? new Color(1f, 0.9f, 0.3f, 1f) : new Color(0.75f, 0.95f, 0.9f, 0.9f);
        DrawHUDShadowedText(new Rect(panelX, panelY + 3f, panelW, 18f), title, 12, titleCol, TextAnchor.MiddleCenter);

        // Bar Yatağı
        float barX = panelX + 14f;
        float barY = panelY + 23f;
        float barW = panelW - 28f;
        float barH = 12f;

        DrawHUDSolidRect(new Rect(barX, barY, barW, barH), new Color(0.12f, 0.16f, 0.24f, 0.95f));

        // Dolu Bar Kısmı (Bölmeli/Segmentli Pixel Art Görünümü)
        Color fillCol = isFocusActive
            ? new Color(1f, 0.85f, 0.18f, 1f)
            : Color.Lerp(new Color(1f, 0.35f, 0.3f, 1f), new Color(0.22f, 0.95f, 0.72f, 1f), smoothDisplayedFocusRatio);

        float filledWidth = barW * Mathf.Clamp01(smoothDisplayedFocusRatio);
        if (filledWidth > 1f)
        {
            DrawHUDSolidRect(new Rect(barX, barY, filledWidth, barH), fillCol);
            // Üst parlama çizgisi (Bevel Highlight)
            DrawHUDSolidRect(new Rect(barX, barY, filledWidth, 3f), new Color(1f, 1f, 1f, 0.35f));
        }

        // Pixel Art Segment Ayırıcı Çentikler (Barı 10 bölmeli retro göstergeye çevirir)
        for (int i = 1; i < 10; i++)
        {
            float tickX = barX + (barW * (i / 10f));
            DrawHUDSolidRect(new Rect(tickX, barY, 2f, barH), new Color(0.05f, 0.08f, 0.15f, 0.85f));
        }
    }

    private void DrawGravityCompassAndSpeedHUD()
    {
        float boxX = 20f;
        float boxY = 18f;
        float boxW = 215f;
        float boxH = 58f;

        Color borderCol = alignFlashTimer > 0f
            ? new Color(1f, 0.9f, 0.3f, 1f)
            : new Color(0.28f, 0.95f, 0.82f, 0.9f);

        DrawHUDChamferedPanel(new Rect(boxX, boxY, boxW, boxH), new Color(0.05f, 0.08f, 0.15f, 0.9f), borderCol, 6f);

        // Sol tarafta Dönen 360° Yerçekimi Pusulası Çemberi ve Oku
        Rect compassRect = new Rect(boxX + 10f, boxY + 9f, 40f, 40f);
        GUI.color = Color.white;
        GUI.DrawTexture(compassRect, compassRingTex);

        // Pusula Okunu mevcut yerçekimi açısına göre döndürerek çiz
        Matrix4x4 oldMatrix = GUI.matrix;
        Vector2 pivot = compassRect.center;
        GUIUtility.RotateAroundPivot(smoothCompassAngle, pivot);
        Rect arrowRect = new Rect(pivot.x - 14f, pivot.y - 14f, 28f, 28f);
        GUI.DrawTexture(arrowRect, compassArrowTex);
        GUI.matrix = oldMatrix;

        // Sağ tarafta Yerçekimi Durumu ve Anlık Düşüş/Hareket Hızı (km/s)
        float speedVal = rb != null ? rb.linearVelocity.magnitude : 0f;
        string gravLabel = alignFlashTimer > 0f ? "HİZALANDI!" : (isGrounded ? "ZEMİNDE" : "SERBEST DÜŞÜŞ");
        Color stateCol = alignFlashTimer > 0f
            ? new Color(1f, 0.9f, 0.3f, 1f)
            : (isGrounded ? new Color(0.35f, 1f, 0.75f, 1f) : new Color(0.45f, 0.88f, 1f, 1f));

        DrawHUDShadowedText(new Rect(boxX + 58f, boxY + 8f, boxW - 64f, 20f), $"ÇEKİM: {gravLabel}", 12, stateCol, TextAnchor.MiddleLeft);

        Color speedCol = speedVal > 28f ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(0.9f, 0.96f, 1f, 0.95f);
        DrawHUDShadowedText(new Rect(boxX + 58f, boxY + 28f, boxW - 64f, 22f), $"HIZ: {speedVal:F1} m/s", 14, speedCol, TextAnchor.MiddleLeft);
    }

    private void DrawSpeedrunStatsHUD()
    {
        float boxW = 185f;
        float boxH = 54f;
        float boxX = Screen.width - boxW - 20f;
        float boxY = 18f;

        DrawHUDChamferedPanel(new Rect(boxX, boxY, boxW, boxH), new Color(0.05f, 0.08f, 0.15f, 0.9f), new Color(0.28f, 0.95f, 0.82f, 0.9f), 6f);

        int minutes = Mathf.FloorToInt(runElapsedTime / 60f);
        float seconds = runElapsedTime % 60f;
        string timeStr = minutes > 0 ? $"{minutes:00}:{seconds:00.0}" : $"{seconds:00.0} sn";

        DrawHUDShadowedText(new Rect(boxX + 14f, boxY + 7f, boxW - 28f, 20f), $"SÜRE:  {timeStr}", 13, new Color(0.35f, 1f, 0.85f, 1f), TextAnchor.MiddleLeft);
        DrawHUDShadowedText(new Rect(boxX + 14f, boxY + 27f, boxW - 28f, 20f), $"ÖLÜM:  {deathCount}", 13, new Color(1f, 0.55f, 0.55f, 0.95f), TextAnchor.MiddleLeft);
    }

    private void DrawMiniControlBadgesHUD()
    {
        float badgeY = Screen.height - 42f;

        // Rozet 1: [SOL TIK] 360° Küre
        DrawSingleKeyBadge(new Rect(20f, badgeY, 138f, 26f), "[SOL TIK] Küre", new Color(0.28f, 0.95f, 0.82f, 0.85f));

        // Rozet 2: [R / SAĞ TIK] Yüzeye Hizala
        Color alignBadgeCol = alignFlashTimer > 0f ? new Color(1f, 0.9f, 0.25f, 1f) : new Color(0.45f, 0.85f, 1f, 0.85f);
        DrawSingleKeyBadge(new Rect(168f, badgeY, 165f, 26f), "[R / SAĞ TIK] Hizala", alignBadgeCol);
    }

    private void DrawSingleKeyBadge(Rect rect, string text, Color borderCol)
    {
        DrawHUDChamferedPanel(rect, new Color(0.05f, 0.08f, 0.15f, 0.82f), borderCol, 4f);
        DrawHUDShadowedText(rect, text, 11, borderCol, TextAnchor.MiddleCenter);
    }

    private void DrawHUDChamferedPanel(Rect r, Color bgCol, Color borderCol, float cut)
    {
        float c = Mathf.Floor(cut);

        // Alt Gölge
        DrawHUDChamferedRect(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.5f), c);

        // Dış Çerçeve
        DrawHUDChamferedRect(r, borderCol, c);

        // İç Dolgu
        float b = 2f;
        Rect inner = new Rect(r.x + b, r.y + b, r.width - b * 2f, r.height - b * 2f);
        DrawHUDChamferedRect(inner, bgCol, Mathf.Max(2f, c - b));

        GUI.color = Color.white;
    }

    private void DrawHUDChamferedRect(Rect r, Color col, float cut)
    {
        float c1 = Mathf.Floor(cut);
        float c2 = Mathf.Floor(cut * 0.5f);

        GUI.color = col;
        GUI.DrawTexture(new Rect(r.x, r.y + c1, r.width, r.height - c1 * 2f), uiWhiteTex);
        GUI.DrawTexture(new Rect(r.x + c2, r.y + c2, r.width - c2 * 2f, c1 - c2), uiWhiteTex);
        GUI.DrawTexture(new Rect(r.x + c2, r.yMax - c1, r.width - c2 * 2f, c1 - c2), uiWhiteTex);
        GUI.DrawTexture(new Rect(r.x + c1, r.y, r.width - c1 * 2f, c2), uiWhiteTex);
        GUI.DrawTexture(new Rect(r.x + c1, r.yMax - c2, r.width - c1 * 2f, c2), uiWhiteTex);
    }

    private void DrawHUDSolidRect(Rect r, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(r, uiWhiteTex);
        GUI.color = Color.white;
    }

    private void DrawHUDShadowedText(Rect rect, string text, int fontSize, Color color, TextAnchor align)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = align,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };

        style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);

        style.normal.textColor = color;
        GUI.Label(rect, text, style);
    }
}