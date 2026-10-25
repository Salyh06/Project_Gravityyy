using UnityEngine;

/// <summary>
/// KAMERA RAY SİSTEMİ (CameraTrackFollow - Dolly Track):
/// Kameranın koridorun ortasından geçen bir "Rayı (Waypoint dizisini)" ileri-geri takip etmesini sağlar!
/// - Düz koridorlarda ray yatay gittiği için karakter zıplasa da, tavana çıksa da kamera Y ekseninde ZERRE OYNAMAZ, dümdüz rayda kayar!
/// - Ortadaki dikey bacadan aşağı inerken veya eğimli koridorlarda ray aşağı indiği için kamera da rayın üzerinde kayarak alt kata iner!
/// - Ayrıca Ray üstünde "Deadzone (Serbest Hareket Payı)" vardır; ufak geri dönüşlerde kamera hemen sallanmaz.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    public static CameraFollow2D Instance { get; private set; }

    [Header("1. Takip Hedefi")]
    [Tooltip("Takip edilecek karakter (Boş bırakırsan sahnedeki GravityPlayerController'ı otomatik bulur).")]
    public Transform target;

    [Tooltip("Kameranın Z mesafesi (-10 kalmalıdır).")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("2. Kamera Rayı (Dolly Track) Ayarları")]
    [Tooltip("AÇIK OLURSA: Kamera sadece belirlediğin Ray Noktaları (Track Waypoints) hattı üzerinde ileri-geri kayar!")]
    public bool useCameraRailTrack = true;

    [Tooltip("Rayın geçtiği köşe noktalarını tutan ana obje (Örn: 'CameraRail' adında boş bir obje oluşturup, içine köşe noktaları için alt objeler koyup buraya sürükle!).")]
    public Transform railWaypointsParent;

    [Tooltip("İsteğe bağlı: Eğer Parent obje yerine noktaları tek tek listeye sürüklemek istersen burayı kullanabilirsin (railWaypointsParent doluysa otomatik onun alt objelerini alır).")]
    public Transform[] manualRailNodes;

    [Tooltip("RAY ÜZERİNDE DEADZONE (Ölü Bölge): Karakter ray üzerinde bu mesafe kadar ileri/geri gitmedikçe kamera rayda sabit durur, sınırı aşınca ray boyunca kayar! (0 = Ray üzerinde anında takip).")]
    public float railDeadzoneDistance = 8f;

    [Header("3. Kamera Kayma Yumuşaklığı & İleri Bakış")]
    [Tooltip("Kameranın ray üzerinde kayma yumuşaklığı (Düşük = hızlı yakalar, Yüksek = sinematik yumuşak kayar).")]
    [Range(0.02f, 0.6f)]
    public float smoothTime = 0.14f;

    [Tooltip("Karakter ray boyunca hızla giderken kameranın ray üzerinde biraz ileriye bakmasını sağlar.")]
    public bool useRailLookAhead = true;

    [Tooltip("Hıza göre ray üzerinde ne kadar ileri baksın?")]
    public float railLookAheadFactor = 0.18f;

    [Tooltip("Maksimum ileri bakış mesafesi.")]
    public float maxRailLookAhead = 7f;

    [Header("4. Mikro Ekran Sarsıntısı (Screen Shake)")]
    [Tooltip("Ekran sarsıntısı aktif olsun mu?")]
    public bool enableScreenShake = true;

    [Tooltip("Sarsıntı şiddeti çarpanı.")]
    [Range(0f, 3f)]
    public float shakeIntensityMultiplier = 1f;

    private Vector3 currentVelocity = Vector3.zero;
    private Rigidbody2D targetRb;
    private GravityPlayerController targetPlayer;

    // Kamera ray üzerindeki anlık mesafe (Arc-Length parametresi)
    private float currentRailDistance = 0f;
    private float currentLookAheadDist = 0f;
    private Vector3 cameraFocusCenter;
    private bool focusInitialized = false;

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
        SnapToTargetImmediate();
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
    /// Ray noktalarını (Waypoints) ya railWaypointsParent altındaki çocuklardan ya da manualRailNodes dizisinden alır.
    /// </summary>
    private Vector2[] GetRailPoints()
    {
        if (railWaypointsParent != null && railWaypointsParent.childCount >= 2)
        {
            int count = railWaypointsParent.childCount;
            Vector2[] pts = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                pts[i] = railWaypointsParent.GetChild(i).position;
            }
            return pts;
        }

        if (manualRailNodes != null && manualRailNodes.Length >= 2)
        {
            int validCount = 0;
            for (int i = 0; i < manualRailNodes.Length; i++)
            {
                if (manualRailNodes[i] != null) validCount++;
            }

            if (validCount >= 2)
            {
                Vector2[] pts = new Vector2[validCount];
                int idx = 0;
                for (int i = 0; i < manualRailNodes.Length; i++)
                {
                    if (manualRailNodes[i] != null)
                    {
                        pts[idx++] = manualRailNodes[i].position;
                    }
                }
                return pts;
            }
        }

        return null;
    }

    /// <summary>
    /// Karakterin (worldPos) Ray üzerindeki en yakın noktasını ve Ray başlangıcından itibaren
    /// kaçıncı metrede (totalDistAlongRail) olduğunu matematiksel olarak bulur!
    /// </summary>
    private Vector2 ProjectPointOntoRail(Vector2[] railPts, Vector2 worldPos, out float distAlongRail, out Vector2 railTangentAtPoint, out float totalRailLength)
    {
        distAlongRail = 0f;
        totalRailLength = 0f;
        railTangentAtPoint = Vector2.right;

        float bestDistSq = float.MaxValue;
        Vector2 bestProj = railPts[0];
        float cumulativeLength = 0f;

        for (int i = 0; i < railPts.Length - 1; i++)
        {
            Vector2 a = railPts[i];
            Vector2 b = railPts[i + 1];
            Vector2 ab = b - a;
            float segLen = ab.magnitude;

            if (segLen > 0.0001f)
            {
                float t = Mathf.Clamp01(Vector2.Dot(worldPos - a, ab) / (segLen * segLen));
                Vector2 proj = a + ab * t;
                float dSq = (worldPos - proj).sqrMagnitude;

                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestProj = proj;
                    distAlongRail = cumulativeLength + (segLen * t);
                    railTangentAtPoint = ab / segLen;
                }
            }

            cumulativeLength += segLen;
        }

        totalRailLength = cumulativeLength;
        return bestProj;
    }

    /// <summary>
    /// Ray başlangıcından itibaren verilen "distance" (metre) değerine karşılık gelen (X, Y) dünya noktasını döndürür.
    /// </summary>
    private Vector2 EvaluatePointAtRailDistance(Vector2[] railPts, float distance)
    {
        if (distance <= 0f) return railPts[0];

        float accumulated = 0f;
        for (int i = 0; i < railPts.Length - 1; i++)
        {
            Vector2 a = railPts[i];
            Vector2 b = railPts[i + 1];
            float segLen = Vector2.Distance(a, b);

            if (accumulated + segLen >= distance)
            {
                float localT = segLen > 0.0001f ? (distance - accumulated) / segLen : 0f;
                return Vector2.Lerp(a, b, localT);
            }

            accumulated += segLen;
        }

        return railPts[railPts.Length - 1];
    }

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

        if (!focusInitialized)
        {
            SnapToTargetImmediate();
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0.0001f) dt = Time.deltaTime;

        bool isFocusing = targetPlayer != null && targetPlayer.IsFocusActive;
        Vector2[] railPts = useCameraRailTrack ? GetRailPoints() : null;

        Vector3 desiredCameraPos;

        if (railPts != null && railPts.Length >= 2)
        {
            // 1. Karakterin konumunu Ray üzerine izdüşür (En yakın ray noktasını ve ray üzerindeki kilometresini bul)
            ProjectPointOntoRail(railPts, target.position, out float playerDistOnRail, out Vector2 railDir, out float totalLength);

            // 2. Ray Üzerinde Deadzone (Serbest Hareket Payı):
            // Karakter ray üzerinde "railDeadzoneDistance" kadar ileri veya geri gitmedikça kamera rayda sabit durur!
            float activeDeadzone = isFocusing ? railDeadzoneDistance * 0.25f : railDeadzoneDistance;
            float deltaDist = playerDistOnRail - currentRailDistance;

            if (deltaDist > activeDeadzone)
            {
                currentRailDistance = playerDistOnRail - activeDeadzone;
            }
            else if (deltaDist < -activeDeadzone)
            {
                currentRailDistance = playerDistOnRail + activeDeadzone;
            }

            // 3. Ray Boyunca İleri Bakış (Look-Ahead)
            if (useRailLookAhead && targetRb != null && !isFocusing)
            {
                float speedAlongRail = Vector2.Dot(targetRb.linearVelocity, railDir);
                float targetLook = Mathf.Abs(speedAlongRail) > 3f
                    ? Mathf.Clamp(speedAlongRail * railLookAheadFactor, -maxRailLookAhead, maxRailLookAhead)
                    : 0f;
                currentLookAheadDist = Mathf.Lerp(currentLookAheadDist, targetLook, dt * 4f);
            }
            else
            {
                currentLookAheadDist = Mathf.Lerp(currentLookAheadDist, 0f, dt * 6f);
            }

            float targetEvalDist = Mathf.Clamp(currentRailDistance + currentLookAheadDist, 0f, totalLength);
            Vector2 pointOnRail = EvaluatePointAtRailDistance(railPts, targetEvalDist);

            // Shift (Focus/Zoom) basılıysa kamera raydan hafifçe karaktere doğru (35%) yaklaşabilir, bırakınca %100 raya oturur
            Vector2 finalXY = isFocusing
                ? Vector2.Lerp(pointOnRail, (Vector2)target.position, 0.35f)
                : pointOnRail;

            desiredCameraPos = new Vector3(finalXY.x, finalXY.y, offset.z);
        }
        else
        {
            // Eğer henüz sahneye Ray noktaları konulmadıysa, standart yumuşak takip yap
            desiredCameraPos = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);
        }

        float effectiveSmoothTime = isFocusing ? smoothTime * 0.6f : smoothTime;
        cameraFocusCenter = Vector3.SmoothDamp(
            cameraFocusCenter,
            desiredCameraPos,
            ref currentVelocity,
            effectiveSmoothTime,
            Mathf.Infinity,
            dt);

        // Mikro Ekran Sarsıntısı (Screen Shake)
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float damper = Mathf.Clamp01(shakeTimer / shakeTotalDuration);
            Vector2 randomCircle = Random.insideUnitCircle * (shakeMagnitude * damper);
            shakeOffset = new Vector3(randomCircle.x, randomCircle.y, 0f);
        }

        transform.position = cameraFocusCenter + shakeOffset;
    }

    public void SnapToTargetImmediate()
    {
        if (target == null) return;
        currentVelocity = Vector3.zero;
        currentLookAheadDist = 0f;
        shakeTimer = 0f;

        Vector2[] railPts = useCameraRailTrack ? GetRailPoints() : null;
        if (railPts != null && railPts.Length >= 2)
        {
            Vector2 proj = ProjectPointOntoRail(railPts, target.position, out float distOnRail, out _, out _);
            currentRailDistance = distOnRail;
            cameraFocusCenter = new Vector3(proj.x, proj.y, offset.z);
        }
        else
        {
            cameraFocusCenter = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);
        }

        transform.position = cameraFocusCenter;
        focusInitialized = true;
    }

    /// <summary>
    /// Scene ekranında Kamera Rayını (Sarı/Turkuaz Çizgi ve Köşe Noktaları) her zaman görsel olarak çizer!
    /// Böylece noktaları tutup sürüklerken rayın koridorun tam ortasından nasıl geçtiğini canlı görürsün!
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!useCameraRailTrack) return;

        Vector2[] pts = GetRailPoints();
        if (pts == null || pts.Length < 2) return;

        for (int i = 0; i < pts.Length; i++)
        {
            // Köşe noktalarını parlak sarı küre olarak çiz
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            Gizmos.DrawSphere(new Vector3(pts[i].x, pts[i].y, 0f), 1.8f);

            // İki nokta arasındaki Ray Hattını parlak turkuaz çizgi olarak çiz
            if (i < pts.Length - 1)
            {
                Gizmos.color = new Color(0.15f, 1f, 0.85f, 0.95f);
                Gizmos.DrawLine(new Vector3(pts[i].x, pts[i].y, 0f), new Vector3(pts[i + 1].x, pts[i + 1].y, 0f));
            }
        }
    }
}