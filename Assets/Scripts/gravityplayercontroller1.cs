using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 360 derece değişen yerçekimine uyum sağlayan, Oval (Capsule) karakterin dönerken:
/// 1) Asla zeminin içine girmemesini,
/// 2) Asla havada asılı kalmamasını,
/// 3) Yerde zıplama/sekme yapmadan yağ gibi kayarak takla atmasını,
/// 4) Duvarlara takılıp tırmanmamasını sağlayan Dinamik Elips Yarıçaplı 2D Karakter Kontrolcüsü.
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
    [Tooltip("Ulaşılabilecek maksimum yürüme/süzülme hızı.")]
    public float maxMoveSpeed = 10f;

    [Tooltip("Yerdeyken maksimum hıza ulaşma ivmesi.")]
    public float groundAcceleration = 65f;

    [Tooltip("Yerdeyken tuşu bıraktığında durma ivmesi / sürtünme.")]
    public float groundDeceleration = 70f;

    [Tooltip("Havadaki (düşerken) hızlanma ivmesi.")]
    public float airAcceleration = 40f;

    [Tooltip("Havada tuşu bıraktığında yatay momentumun ne kadar korunacağı (0 = havada hiç yavaşlamaz).")]
    public float airDeceleration = 12f;

    [Header("2. Zıplama ve Takla Toleransı (Coyote Time)")]
    [Tooltip("Zıplama kuvveti (yerçekiminin tersi yönünde).")]
    public float jumpForce = 14f;

    [Tooltip("Zıplama tolerans süresi (saniye).")]
    public float jumpCoyoteTime = 0.2f;

    [Tooltip("Sürekli düşerek ilerlerken ulaşılabilecek maksimum düşüş hızı (Terminal Velocity).")]
    public float maxFallSpeed = 22f;

    [Header("3. Yerçekimi Değişiminde Momentum Davranışı")]
    [Tooltip("Küre duvara çarpıp yerçekimi değiştiğinde karakterin mevcut hızına ne olsun?")]
    public GravityShiftMomentumMode momentumMode = GravityShiftMomentumMode.KeepWorldMomentum;

    [Tooltip("KeepWorldMomentum veya RedirectSpeedToNewDown seçiliyse, geçiş anında eski hızın yüzde kaçı korunsun?")]
    [Range(0f, 2f)]
    public float momentumRetentionMultiplier = 0.85f;

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
    public float projectileSpeedMultiplier = 1f;

    [Tooltip("Karakterin kendi hareket hızı fırlatılan mermiye eklensin mi?")]
    public bool inheritPlayerMomentumOnThrow = false;

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

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private CircleCollider2D circleCollider;
    private PhysicsMaterial2D zeroFrictionBounceMat;
    private Camera mainCam;
    private Vector2 moveInput;
    private bool isGrounded;
    private float lastGroundedTime = -10f;
    private float lastJumpTime = -10f;
    private int currentAirThrowsUsed = 0;
    private Quaternion targetRotation = Quaternion.identity;

    private Vector2 lastContactSurfaceNormal = Vector2.up;
    private bool hasActiveSurfaceContact = false;
    private bool isAligningToSurface = false;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[4];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Eğer kullanıcıda hala CapsuleCollider2D takılıysa veya CircleCollider2D varsa ikisini de destekle,
        // ancak duvarlara tırmanmayı ve sekmeyi %100 sıfırlamak için otomatik Sürtünmesiz & Sekmesiz Fizik Materyali ata:
        zeroFrictionBounceMat = new PhysicsMaterial2D("ZeroFrictionNoBounce")
        {
            friction = 0f,
            bounciness = 0f
        };
        rb.sharedMaterial = zeroFrictionBounceMat;

        SetupSmoothDynamicCircleCollider();
        mainCam = Camera.main;
    }

    /// <summary>
    /// Oval karakterin duvarlara takılıp tırmanmasını ve yerde zıplamasını engellemek için
    /// CircleCollider2D kullanırız ve yarıçapını her karede ovalin yere bakan açısına göre dinamik ayarlarız.
    /// </summary>
    private void SetupSmoothDynamicCircleCollider()
    {
        circleCollider = GetComponent<CircleCollider2D>();

        // Eğer objede hala CapsuleCollider2D varsa ve CircleCollider2D yoksa otomatik dönüştür:
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

    private void OnEnable()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged += HandleGravityChanged;
        }
    }

    private void Start()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged -= HandleGravityChanged;
            GravityManager.Instance.OnGravityChanged += HandleGravityChanged;
            UpdateTargetRotation(GravityManager.Instance.CurrentGravityDirection);
        }
    }

    private void OnDisable()
    {
        if (GravityManager.Instance != null)
        {
            GravityManager.Instance.OnGravityChanged -= HandleGravityChanged;
        }
    }

    private void Update()
    {
        CheckGrounded();
        ReadPlayerInputs(out bool jumpPressed, out bool firePressed, out bool alignSurfacePressed, out Vector2 mouseScreenPos);

        // R tuşu veya Sağ Tık: Karakterin bulunduğu yüzeyin normaline göre yerçekimini dik (normal) hale getir
        if (alignSurfacePressed)
        {
            AlignGravityToCurrentSurface();
        }

        // Zıplama: Yerdeysek VEYA Coyote Time içindeysek zıpla
        bool canJump = isGrounded || (Time.time - lastGroundedTime <= jumpCoyoteTime && Time.time - lastJumpTime > 0.22f);
        if (jumpPressed && canJump)
        {
            Jump();
        }

        if (firePressed)
        {
            TryThrowProjectile(mouseScreenPos);
        }

        UpdateVisualTumbleAndDynamicCollider();
    }

    /// <summary>
    /// BU FONKSİYON ARADIĞIN BÜTÜN SORUNLARI ÇÖZER:
    /// 1) Karakterin görsel açısını döndürür.
    /// 2) Oval şeklin o anki açısında yerçekimi yönüne (zemine) olan tam yüksekliğini trigonometrik olarak hesaplar.
    /// 3) CircleCollider2D'nin yarıçapını (`radius`) tam o yüksekliğe eşitler!
    /// Sonuç:
    /// - Kapsül dik dururken yarıçap 1.0 olur (Yere asla gömülmez!).
    /// - Kapsül yan yatarken yarıçap 0.5 olur (Havada asla asılı kalmaz!).
    /// - Çarpıştırıcı her an pürüzsüz bir daire olduğu ve sürtünmesi 0 olduğu için ne yerde seker ne de duvara tırmanır!
    /// </summary>
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

        // Ovalin (Kapsülün) o anki dönüş açısında zemine (downDir) olan tam teğet yüksekliğini hesapla:
        // Bir elipsin/kapsülün verilen bir (downDir) yönündeki destek fonksiyonu (Support Distance):
        if (circleCollider != null)
        {
            // Kapsülün uzun ekseni kendi local Y eksenidir (transform.up), kısa ekseni local X eksenidir (transform.right)
            float cosTheta = Mathf.Abs(Vector2.Dot(transform.up, downDir));
            float sinTheta = Mathf.Abs(Vector2.Dot(transform.right, downDir));

            // Kapsül geometrisi: Yarıçapı 'r = ovalHalfWidth' olan iki yarım küre ve aralarındaki 'h = ovalHalfHeight - ovalHalfWidth' çubuk!
            // Kapsülün herhangi bir açıdaki zemine olan KESİN mesafesi = r + h * |cos(theta)| formülüyle milimetrik bulunur!
            float capsuleRadius = ovalHalfWidth;
            float cylinderHalfLength = Mathf.Max(0f, ovalHalfHeight - ovalHalfWidth);

            float exactGroundDistance = capsuleRadius + (cylinderHalfLength * cosTheta);

            // CircleCollider2D yarıçapını bu kesin mesafeye eşitle:
            circleCollider.radius = exactGroundDistance;
            circleCollider.offset = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        ApplyMovementAndTerminalVelocity();
    }

    private void ReadPlayerInputs(out bool jumpPressed, out bool firePressed, out bool alignSurfacePressed, out Vector2 mouseScreenPos)
    {
        moveInput = Vector2.zero;
        jumpPressed = false;
        firePressed = false;
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
                hitGround = true;
                lastContactSurfaceNormal = castHits[0].normal.normalized;
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
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
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

            // Sadece karakterin alt yarı küresine (yerçekimi yönüne) yakın temasları "Zemin" kabul et
            // Böylece yan duvara sürtünürken duvarı zemin sanıp duvara tırmanmaz!
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

            transform.rotation = targetRotation;
        }
    }

    private void ApplyMovementAndTerminalVelocity()
    {
        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection
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

        // DUVARA YAPIŞMA / TIRMANMA ENGELLEYİCİ:
        // Eğer oyuncu bir duvara doğru yürümeye çalışıyorsa (yüzeye paralel yönde önünde duvar varsa)
        // o yöndeki itişi kes ki karakter duvara tırmanmasın veya duvara yapışıp asılı kalmasın!
        if (Mathf.Abs(tangentInput) > 0.01f && playerCollider != null)
        {
            Vector2 moveDir = surfaceTangent * Mathf.Sign(tangentInput);
            ContactFilter2D wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(groundLayer);
            wallFilter.useTriggers = false;

            int wallHits = playerCollider.Cast(moveDir, wallFilter, castHits, 0.08f);
            for (int i = 0; i < wallHits; i++)
            {
                // Çarptığımız yüzey yürüme yönümüze karşı duran dik bir duvarsa:
                if (Vector2.Dot(castHits[i].normal, -moveDir) > 0.7f)
                {
                    tangentInput = 0f;
                    break;
                }
            }
        }

        Vector2 currentVel = rb.linearVelocity;
        float currentTangentSpeed = Vector2.Dot(currentVel, surfaceTangent);
        float verticalSpeed = Vector2.Dot(currentVel, upDir);

        // Yerde takla atarken kapsülün yarıçap değişiminden kaynaklı yukarı yönlü zıplama/sekme hızlarını bastır
        if (isGrounded && Time.time - lastJumpTime > 0.15f && verticalSpeed > 0f)
        {
            verticalSpeed = 0f;
        }

        float targetTangentSpeed = tangentInput * maxMoveSpeed;

        float accelRate;
        if (Mathf.Abs(targetTangentSpeed) > 0.01f)
        {
            accelRate = isGrounded ? groundAcceleration : airAcceleration;
        }
        else
        {
            accelRate = isGrounded ? groundDeceleration : airDeceleration;
        }

        float newTangentSpeed = Mathf.MoveTowards(currentTangentSpeed, targetTangentSpeed, accelRate * Time.fixedDeltaTime);

        if (verticalSpeed < -maxFallSpeed)
        {
            verticalSpeed = -maxFallSpeed;
        }

        rb.linearVelocity = (surfaceTangent * newTangentSpeed) + (upDir * verticalSpeed);
    }

    private void Jump()
    {
        lastJumpTime = Time.time;
        isGrounded = false;
        hasActiveSurfaceContact = false;

        Vector2 downDir = GravityManager.Instance != null
            ? GravityManager.Instance.CurrentGravityDirection
            : Vector2.down;
        Vector2 upDir = -downDir;
        Vector2 surfaceTangent = new Vector2(upDir.y, -upDir.x);

        float currentTangentSpeed = Vector2.Dot(rb.linearVelocity, surfaceTangent);
        rb.linearVelocity = (surfaceTangent * currentTangentSpeed) + (upDir * jumpForce);
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

        Vector2 currentVel = rb.linearVelocity;
        float currentSpeed = currentVel.magnitude;

        switch (momentumMode)
        {
            case GravityShiftMomentumMode.ResetToZero:
                rb.linearVelocity = Vector2.zero;
                break;

            case GravityShiftMomentumMode.KeepWorldMomentum:
                rb.linearVelocity = currentVel * momentumRetentionMultiplier;
                break;

            case GravityShiftMomentumMode.RedirectSpeedToNewDown:
                rb.linearVelocity = newGravityDir.normalized * (currentSpeed * momentumRetentionMultiplier);
                break;
        }
    }

    private void UpdateTargetRotation(Vector2 gravityDir)
    {
        Vector2 upDir = -gravityDir.normalized;
        float angle = Mathf.Atan2(upDir.y, upDir.x) * Mathf.Rad2Deg - 90f;
        targetRotation = Quaternion.Euler(0f, 0f, angle);
    }
}