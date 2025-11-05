using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;      // El objeto que la cámara debe seguir
    public Vector3 offset;        // Distancia desde el jugador
    public float smoothSpeed = 0.125f; // Suavidad del movimiento

    void LateUpdate()
    {
        if (player == null) return;

        // Posición deseada = posición del jugador + offset
        Vector3 desiredPosition = player.position + offset;

        // Movimiento suave hacia la posición deseada
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Aplica la posición
        transform.position = smoothedPosition;

        // (Opcional) Mantiene la rotación fija de la cámara (por ejemplo, la que tienes ahora)
        transform.rotation = Quaternion.Euler(47.417f, 364.251f, 5.681f);
    }
}
