using System;
using UnityEngine;

/// <summary>
/// Dünyanın yerçekimi yönünü ve şiddetini yöneten merkezi sistem.
/// Sahnedeki boş bir GameObject'e (örn. "GravityManager") eklenir.
/// </summary>
public class GravityManager : MonoBehaviour
{
    public static GravityManager Instance { get; private set; }

    [Header("Yerçekimi Ayarları")]
    [Tooltip("Yerçekimi ivmesinin büyüklüğü (Standart: 25 - hızlı düşüş hissiyatı için).")]
    public float gravityStrength = 25f;

    [Tooltip("Mevcut yerçekimi yönü (normalize edilmiş birim vektör).")]
    public Vector2 CurrentGravityDirection { get; private set; } = Vector2.down;

    // Yerçekimi yönü değiştiğinde karakter ve diğer nesneleri haberdar eden event
    public event Action<Vector2> OnGravityChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Oyun başladığında varsayılan yerçekimini aşağı yönde ayarla
        SetGravityDirection(Vector2.down);
    }

    /// <summary>
    /// Dünyanın yerçekimi yönünü 360 derece herhangi bir açıdaki vektöre günceller.
    /// </summary>
    /// <param name="newDirection">Yeni yerçekimi yön vektörü</param>
    public void SetGravityDirection(Vector2 newDirection)
    {
        if (newDirection.sqrMagnitude < 0.001f) return;

        CurrentGravityDirection = newDirection.normalized;
        Physics2D.gravity = CurrentGravityDirection * gravityStrength;

        // Dinleyen tüm nesnelere (örn. Player) yeni yönü bildir
        OnGravityChanged?.Invoke(CurrentGravityDirection);
    }
}