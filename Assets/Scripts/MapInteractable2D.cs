using UnityEngine;

public class MapInteractable2D : MonoBehaviour
{
    public enum ObjectType
    {
        Spike,
        Mushroom
    }

    [Header("Object Settings")]
    [SerializeField] private ObjectType objectType;

    [Header("Mushroom Settings")]
    [SerializeField] private float bounceSpeed = 17f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        GravityPlayerController player =
            other.GetComponentInParent<GravityPlayerController>();

        if (player == null)
            return;

        switch (objectType)
        {
            case ObjectType.Spike:
                player.Respawn();
                break;

            case ObjectType.Mushroom:
                player.Bounce(bounceSpeed);
                break;
        }
    }
}