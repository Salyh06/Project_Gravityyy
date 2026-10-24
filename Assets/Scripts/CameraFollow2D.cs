
using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform player;
    public float followSpeed = 12f;

    public enum FollowMode
    {
        Both,
        OnlyX,
        OnlyY
    }

    public FollowMode defaultMode = FollowMode.OnlyX;

    private CameraZone2D[] zones;
    private CameraZone2D activeZone;

    private float startCameraX;
    private float startCameraY;

    void Start()
    {
        zones = FindObjectsByType<CameraZone2D>(
            FindObjectsSortMode.None
        );

        if (player == null)
        {
            Debug.LogError("Kamera: Player atanmamis!");
            return;
        }

        startCameraX = player.position.x;
        startCameraY = player.position.y;

        SnapToPlayer();
    }

    void LateUpdate()
    {
        if (player == null) return;

        activeZone = FindActiveZone();

        Vector3 target = GetTargetPosition();

        float smooth = 1f - Mathf.Exp(
            -followSpeed * Time.deltaTime
        );

        transform.position = Vector3.Lerp(
            transform.position,
            target,
            smooth
        );
    }

    private CameraZone2D FindActiveZone()
    {
        CameraZone2D selected = null;

        foreach (CameraZone2D zone in zones)
        {
            if (!zone.isActiveAndEnabled)
                continue;

            if (!zone.IsInside(player.position))
                continue;

            if (selected == null ||
                zone.priority > selected.priority)
            {
                selected = zone;
            }
        }

        return selected;
    }

    private Vector3 GetTargetPosition()
    {
        FollowMode mode = activeZone != null
            ? activeZone.zoneMode
            : defaultMode;

        Vector3 target = transform.position;

        switch (mode)
        {
            case FollowMode.Both:
                target.x = player.position.x;
                target.y = player.position.y;
                break;

            case FollowMode.OnlyX:
                target.x = player.position.x;

                target.y = activeZone != null
                    ? activeZone.transform.position.y
                    : startCameraY;
                break;

            case FollowMode.OnlyY:
                target.x = activeZone != null
                    ? activeZone.transform.position.x
                    : startCameraX;

                target.y = player.position.y;
                break;
        }

        return target;
    }

    public void SnapToPlayer()
    {
        if (player == null) return;

        if (zones == null)
            return;

        activeZone = FindActiveZone();

        transform.position = GetTargetPosition();
    }
}
