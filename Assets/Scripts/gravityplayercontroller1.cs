using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 360 derece değişen yerçekimine uyum sağlayan, Oval (Capsule) karakterin dönerken:
/// 1) Asla zeminin içine girmemesini ve havada asılı kalmamasını,
/// 2) Yerde zıplama/sekme yapmadan yağ gibi kayarak takla atmasını,
/// 3) Havada bir noktaya doğru serbest düşerken çok daha hızlı ivmelenip yüksek düşüş hızlarına (Fast Fall & Gravity Shift Boost) ulaşmasını,
/// 4) Sadece senin elle belirlediğin tehlike objelerine (HazardZone scripti ekli veya Hazard Layer/Tag'li objelere) değdiğinde ölüp başa dönmesini sağlayan 2D Karakter Kontrolcüsü.
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
    [Tooltip("Zıplama kuvveti (yerçekiminin tersi yönünde).")]
    public float jumpForce = 15f;

    [Tooltip("Zıplama tolerans süresi (saniye).")]
    public float jumpCoyoteTime = 0.2f;

    [Tooltip("Havada serbest düşerken ulaşılabilecek MAKSİMUM düşüş hızı (Terminal Velocity).")]
    public float maxFallSpeed = 48f;

    [Tooltip("Karakter havada düşerken yerçekimini kaç kat daha güçlü hissetsin? (1 = normal, 2.5 = 2.5 kat daha hızlı ivmelenerek düşer!).")]
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

    [Header("7. Ölüm (Hazard) ve Yeniden Doğma (Respawn)")]
    [Tooltip("İsteğe bağlı: Özel bir doğma noktası belirlemek istersen buraya sürükle. Boş bırakırsan oyuna başladığın ilk konumu otomatik kaydeder!")]
    public Transform customSpawnPoint;

    [Tooltip("İsteğe bağlı: Eğer öldürücü duvarlara özel bir Layer (örn. Hazard) verirsen buradan seçebilirsin (veya öldürecek objelere doğrudan HazardZone scripti ekleyebilirsin).")]
    public LayerMask hazardLayer;

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

    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        zeroFrictionBounceMat = new PhysicsMaterial2D("ZeroFrictionNoBounce")
        {
            friction = 0f,
            bounciness = 0f
        };
        rb.sharedMaterial = zeroFrictionBounceMat;

        SetupSmoothDynamicCircleCollider();
        mainCam = Camera.main;

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

        UpdateVisualTumbleAndDynamicCollider();
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsHazardObject(collision.gameObject))
        {
            DieAndRespawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsHazardObject(other.gameObject))
        {
            DieAndRespawn();
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsHazardObject(collision.gameObject))
        {
            DieAndRespawn();
            return;
        }

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

    /// <summary>
    /// Sadece senin elle belirlediğin tehlike objelerini (HazardZone scripti ekli olanlar,
    /// seçtiğin Hazard Layer katmanındakiler veya Tag'i "Respawn" olanlar) öldürücü kabul eder.
    /// </summary>
    private bool IsHazardObject(GameObject obj)
    {
        if (obj == null) return false;

        // 1. Obje üzerinde HazardZone scripti varsa
        if (obj.GetComponent<HazardZone>() != null)
        {
            return true;
        }

        // 2. Obje senin seçtiğin Hazard Layer katmanındaysa
        if (((1 << obj.layer) & hazardLayer) != 0)
        {
            return true;
        }

        // 3. Objenin Tag'i "Respawn" olarak seçildiyse
        if (obj.CompareTag("Respawn"))
        {
            return true;
        }

        return false;
    }

    public void DieAndRespawn()
    {
        GravityProjectile[] activeProjectiles = FindObjectsByType<GravityProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < activeProjectiles.Length; i++)
        {
            if (activeProjectiles[i] != null)
            {
                Destroy(activeProjectiles[i].gameObject);
            }
        }

        Vector3 spawnPos = customSpawnPoint != null ? customSpawnPoint.position : initialSpawnPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.position = spawnPos;
        transform.rotation = initialSpawnRotation;

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

        // DUVARA YAPIŞMA ENGELLEYİCİ
        if (Mathf.Abs(tangentInput) > 0.01f && playerCollider != null)
        {
            Vector2 moveDir = surfaceTangent * Mathf.Sign(tangentInput);
            ContactFilter2D wallFilter = new ContactFilter2D();
            wallFilter.SetLayerMask(groundLayer);
            wallFilter.useTriggers = false;

            int wallHits = playerCollider.Cast(moveDir, wallFilter, castHits, 0.08f);
            for (int i = 0; i < wallHits; i++)
            {
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

        // Yerde takla atarken zıplama dışı sekme hızlarını bastır
        if (isGrounded && Time.time - lastJumpTime > 0.15f && verticalSpeed > 0f)
        {
            verticalSpeed = 0f;
        }

        // HIZLI SERBEST DÜŞÜŞ (FAST FALL):
        if (!isGrounded && fallGravityMultiplier > 1f)
        {
            float baseGravity = GravityManager.Instance != null ? GravityManager.Instance.gravityStrength : 25f;
            float extraFallAccel = baseGravity * (fallGravityMultiplier - 1f);
            verticalSpeed -= extraFallAccel * Time.fixedDeltaTime;
        }

        float currentMaxMoveSpeed = isGrounded ? maxMoveSpeed : maxAirMoveSpeed;
        float targetTangentSpeed = tangentInput * currentMaxMoveSpeed;

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
}