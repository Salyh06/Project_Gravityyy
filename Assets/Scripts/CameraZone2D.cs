using UnityEngine;

public class CameraZone2D : MonoBehaviour
{
    public CameraFollow2D.FollowMode zoneMode =
        CameraFollow2D.FollowMode.Both;

    public int priority = 0;

    // Bölgenin boyutu Transform Scale ile belirlenir.
    public bool IsInside(Vector2 playerPosition)
    {
        Vector3 local = transform.InverseTransformPoint(playerPosition);

        return Mathf.Abs(local.x) <= 0.5f &&
               Mathf.Abs(local.y) <= 0.5f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}