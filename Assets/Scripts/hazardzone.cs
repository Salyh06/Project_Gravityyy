using UnityEngine;

/// <summary>
/// İsteğe bağlı olarak tekil tehlike/diken/asit objelerine de eklenebilir.
/// Karakter (GravityPlayerController) bu objeye çarptığında veya içinden geçtiğinde
/// karakteri başlangıç noktasına geri döndürür (Respawn).
/// Not: GravityPlayerController üzerindeki "Hazard Layer" (Örn: Hazard katmanı) veya
/// "Auto Kill GreenWalls" özelliği sayesinde sahnedeki onlarca yeşil duvara tek tek script
/// eklemene bile gerek kalmadan otomatik çalışır!
/// </summary>
public class HazardZone : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<GravityPlayerController>(out GravityPlayerController player))
        {
            player.DieAndRespawn();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<GravityPlayerController>(out GravityPlayerController player))
        {
            player.DieAndRespawn();
        }
    }
}