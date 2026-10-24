using UnityEngine;

/// <summary>
/// Tehlike / Spike / Diken objelerine eklenir.
/// Karakterin dış fiziksel çemberine değdiğinde hemen öldürmez; sadece karakterin
/// küçültülmüş iç ölüm hitbox'ına (Lethal Core) değdiğinde öldürür!
/// </summary>
public class HazardZone : MonoBehaviour
{
    private Collider2D myCollider;

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckHit(collision.gameObject, collision.otherCollider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckHit(collision.gameObject, collision.otherCollider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckHit(other.gameObject, myCollider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CheckHit(other.gameObject, myCollider);
    }

    private void CheckHit(GameObject hitObj, Collider2D hazardCol)
    {
        if (hitObj.TryGetComponent<GravityPlayerController>(out GravityPlayerController player))
        {
            if (hazardCol == null) hazardCol = GetComponent<Collider2D>();

            // Sadece küçültülmüş iç ölüm hitbox'ına (Lethal Core) değdiyse öldür!
            if (hazardCol == null || player.IsColliderTouchingLethalCore(hazardCol))
            {
                player.DieAndRespawn();
            }
            else if (hazardCol != null && !hazardCol.isTrigger)
            {
                // Dikenin kenarından sıyırıp geçerken fiziksel olarak takılmaması için Trigger yap
                hazardCol.isTrigger = true;
            }
        }
    }
}