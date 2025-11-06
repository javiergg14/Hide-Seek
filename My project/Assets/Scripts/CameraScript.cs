using UnityEngine;

public class PlayerCameraFollow : MonoBehaviour
{
    [Header("Objetivo a seguir")]
    public Transform target;         // Arrastra aquí el Player

    [Header("Posición de la cámara")]
    public Vector3 offset = new Vector3(0f, 5f, -6f);  // distancia respecto al jugador

    [Header("Rotación")]
    public bool lookAtTarget = true; // Si la cámara mira al jugador
    public float rotationSmooth = 5f;

    [Header("Movimiento suave")]
    public float smoothSpeed = 8f;   // velocidad de seguimiento

    void LateUpdate()
    {
        if (!target) return;

        // Posición deseada según el offset relativo al jugador
        Vector3 desiredPosition = target.position + offset;

        // Movimiento suave con interpolación
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Rotación hacia el jugador (opcional)
        if (lookAtTarget)
        {
            Quaternion desiredRot = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * Time.deltaTime);
        }
    }
}
