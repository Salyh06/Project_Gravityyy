using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    private Transform cameraTransform;
    private Vector3 startPosition;
    private float startCameraX;

    [Header("Parallax Hızı (0 = Sabit, 1 = Kamerayla Aynı Hız)")]
    [SerializeField] private float parallaxMultiplier = 0.5f;

    [Header("Piksel Ayarı (Projedeki PPU Değeri)")]
    [SerializeField] private float pixelsPerUnit = 16f; // 16 veya 32

    void Start()
    {
        cameraTransform = Camera.main.transform;
        startPosition = transform.position;
        startCameraX = cameraTransform.position.x;
    }

    void LateUpdate()
    {
        // 1. Kameranın başlangıçtan itibaren kat ettiği net mesafe
        float cameraDeltaX = cameraTransform.position.x - startCameraX;

        // 2. Parallax oranına göre hedef X pozisyonunu hesapla
        float targetX = startPosition.x + (cameraDeltaX * parallaxMultiplier);

        // 3. Piksel gridine kusursuz sabitleme (Titremeyi önleyen kısım)
        float unitPerPixel = 1f / pixelsPerUnit;
        float snappedX = Mathf.Round(targetX / unitPerPixel) * unitPerPixel;
        float snappedY = Mathf.Round(startPosition.y / unitPerPixel) * unitPerPixel;

        // Pozisyonu güncelle
        transform.position = new Vector3(snappedX, snappedY, transform.position.z);
    }
}