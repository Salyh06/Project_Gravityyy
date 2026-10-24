using System.Collections;
using UnityEngine;

/// <summary>
/// Karakterin fırlattığı nesne. Fırlatıldığı yönü hafızasında tutar, arkasında neon bir kuyruk izi (Trail) bırakır
/// ve bir yüzeye (Ground katmanına) çarptığında:
/// 1) Dünyanın yerçekimini o fırlatma yönüne çevirir,
/// 2) Çarpışma noktasında büyüyen bir ŞOK DALGASI HALKASI (Shockwave Ring) oluşturur,
/// 3) Etrafa 18 adet NEON KIVILCIM PARÇACIĞI (Particle Burst) saçar,
/// 4) Kameraya MİKRO EKRAN SARSINTISI (Screen Shake) tetikler!
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

    [Header("Mermi Kuyruk İzi (Trail)")]
    [Tooltip("Merminin arkasında da neon iz çıksın mı?")]
    public bool enableProjectileTrail = true;

    [Tooltip("Mermi izinin ve çarpma efektlerinin ana rengi (HDR/Bloom ile parlar!).")]
    public Color projectileTrailColor = new Color(0f, 0.95f, 1f, 0.95f);

    [Tooltip("Mermi izinin uzunluğu (saniye).")]
    public float projectileTrailDuration = 0.18f;

    [Header("Çarpma Efektleri (Şok Dalgası + Kıvılcım + Screen Shake)")]
    [Tooltip("Duvara çarptığında Şok Dalgası + Kıvılcım Patlaması çıksın mı?")]
    public bool enableImpactVFX = true;

    [Tooltip("Şok dalgası halkasının ulaşacağı maksimum çap.")]
    public float shockwaveMaxRadius = 2.4f;

    [Tooltip("Şok dalgasının yayılma süresi (saniye).")]
    public float shockwaveDuration = 0.22f;

    [Tooltip("Çarpma anında etrafa saçılacak kıvılcım sayısı.")]
    public int sparkCount = 18;

    [Tooltip("Çarpma anında kameranın sarsılma süresi (saniye).")]
    public float impactShakeDuration = 0.14f;

    [Tooltip("Çarpma anında kameranın sarsılma gücü (0 = sarsıntı yok).")]
    public float impactShakeMagnitude = 0.35f;

    private Rigidbody2D rb;
    private Vector2 throwDirection;
    private bool hasTriggered = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (enableProjectileTrail)
        {
            SetupProjectileTrail();
        }
    }

    private void SetupProjectileTrail()
    {
        TrailRenderer tr = GetComponent<TrailRenderer>();
        if (tr == null)
        {
            tr = gameObject.AddComponent<TrailRenderer>();
        }

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            tr.material = new Material(spriteShader);
        }

        if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
        {
            tr.sortingLayerID = sr.sortingLayerID;
            tr.sortingOrder = sr.sortingOrder - 1;
        }

        tr.time = projectileTrailDuration;
        tr.minVertexDistance = 0.05f;
        tr.numCornerVertices = 4;
        tr.numCapVertices = 4;

        float startWidth = Mathf.Max(transform.localScale.x, 0.35f) * 0.85f;
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, startWidth);
        widthCurve.AddKey(1f, 0f);
        tr.widthCurve = widthCurve;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(projectileTrailColor, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(projectileTrailColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(projectileTrailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        tr.colorGradient = grad;
    }

    public void Launch(Vector2 direction)
    {
        throwDirection = direction.normalized;

        float angle = Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        rb.linearVelocity = throwDirection * speed;

        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
        HandleHit(collision.gameObject, hitPoint);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Vector2 hitPoint = other.ClosestPoint(transform.position);
        HandleHit(other.gameObject, hitPoint);
    }

    private void HandleHit(GameObject hitObject, Vector2 hitPoint)
    {
        if (hasTriggered) return;

        if (((1 << hitObject.layer) & surfaceLayerMask) != 0)
        {
            hasTriggered = true;

            // 1. Dünya yerçekimini fırlatılan yöne çevir
            if (GravityManager.Instance != null)
            {
                GravityManager.Instance.SetGravityDirection(throwDirection);
            }

            // 2. Mikro Ekran Sarsıntısı (Screen Shake) tetikle
            if (impactShakeMagnitude > 0f)
            {
                if (CameraFollow2D.Instance != null)
                {
                    CameraFollow2D.Instance.TriggerScreenShake(impactShakeDuration, impactShakeMagnitude);
                }
                else if (Camera.main != null && Camera.main.TryGetComponent<CameraFollow2D>(out CameraFollow2D camFollow))
                {
                    camFollow.TriggerScreenShake(impactShakeDuration, impactShakeMagnitude);
                }
            }

            // 3. Şok Dalgası Halkası + Kıvılcım Patlaması oluştur
            if (enableImpactVFX)
            {
                Color vfxColor = projectileTrailColor;
                if (TryGetComponent<SpriteRenderer>(out SpriteRenderer sr))
                {
                    vfxColor = Color.Lerp(projectileTrailColor, sr.color, 0.4f);
                    vfxColor.a = 1f;
                }
                GravityImpactVFXRunner.SpawnImpactVFX(hitPoint, vfxColor, shockwaveMaxRadius, shockwaveDuration, sparkCount);
            }

            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Mermi yok olduktan sonra sahnede 0.35 saniye yaşayıp Şok Dalgası Halkasını (Shockwave Ring)
/// ve Kıvılcım Patlamasını (Spark Particle Burst) oynatıp kendi kendini temizleyen yardımcı efekt sınıfı.
/// </summary>
public class GravityImpactVFXRunner : MonoBehaviour
{
    private LineRenderer ringRenderer;
    private float maxRadius;
    private float duration;
    private Color baseColor;

    public static void SpawnImpactVFX(Vector2 position, Color color, float maxRadius, float duration, int sparkCount)
    {
        GameObject vfxObj = new GameObject("GravityImpactVFX");
        vfxObj.transform.position = new Vector3(position.x, position.y, 0f);

        GravityImpactVFXRunner runner = vfxObj.AddComponent<GravityImpactVFXRunner>();
        runner.Initialize(color, maxRadius, duration, sparkCount);
    }

    public void Initialize(Color color, float radius, float dur, int sparks)
    {
        baseColor = color;
        maxRadius = radius;
        duration = Mathf.Max(0.05f, dur);

        CreateShockwaveRing();
        CreateSparkBurstParticles(sparks);

        StartCoroutine(AnimateShockwaveAndCleanup());
    }

    private void CreateShockwaveRing()
    {
        ringRenderer = gameObject.AddComponent<LineRenderer>();
        ringRenderer.useWorldSpace = false;
        ringRenderer.loop = true;
        ringRenderer.positionCount = 36;
        ringRenderer.numCornerVertices = 2;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            ringRenderer.material = new Material(spriteShader);
        }

        ringRenderer.sortingOrder = 20;
    }

    private void CreateSparkBurstParticles(int sparkCount)
    {
        if (sparkCount <= 0) return;

        GameObject particleObj = new GameObject("Sparks");
        particleObj.transform.SetParent(transform, false);

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        // ÖNEMLİ DÜZELTME: Unity'de AddComponent<ParticleSystem>() yapıldığı an sistem otomatik olarak
        // "Playing" (Oynuyor) durumunda başlar ve çalışırken main.duration değiştirilirse konsola kırmızı uyarı basar!
        // Bu yüzden önce tamamen durdurup (StopEmittingAndClear), ayarları yapıp sonra Play() diyoruz:
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, baseColor);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.dampen = 0.25f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

        ParticleSystemRenderer psRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            psRenderer.material = new Material(spriteShader);
        }
        psRenderer.sortingOrder = 21;

        ps.Play();
    }

    private IEnumerator AnimateShockwaveAndCleanup()
    {
        float elapsed = 0f;
        int segments = ringRenderer.positionCount;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            float currentRadius = Mathf.Lerp(0.15f, maxRadius, easedT);

            float currentWidth = Mathf.Lerp(0.22f, 0.01f, t);
            ringRenderer.startWidth = currentWidth;
            ringRenderer.endWidth = currentWidth;

            Color c = Color.Lerp(Color.white, baseColor, t * 0.7f);
            c.a = Mathf.Lerp(0.95f, 0f, t);
            ringRenderer.startColor = c;
            ringRenderer.endColor = c;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f));
            }

            yield return null;
        }

        ringRenderer.enabled = false;
        yield return new WaitForSecondsRealtime(0.25f);
        Destroy(gameObject);
    }
}