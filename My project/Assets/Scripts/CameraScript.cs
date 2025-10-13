using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float speed = 5f;          // Velocidad de movimiento
    public float rotationSpeed = 100f; // Velocidad de rotación (Q y E)

    void Update()
    {
        // Inputs de movimiento
        float moveX = Input.GetAxis("Horizontal"); // A, D
        float moveZ = Input.GetAxis("Vertical");   // W, S

        // Movimiento relativo a la cámara
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // Ignoramos el eje Y (para que no se mueva hacia arriba/abajo)
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // Dirección final de movimiento
        Vector3 move = (forward * moveZ + right * moveX).normalized;

        // Aplicar movimiento
        transform.position += move * speed * Time.deltaTime;

        // Rotación con Q y E en el eje Y
        if (Input.GetKey(KeyCode.Q))
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);

        if (Input.GetKey(KeyCode.E))
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
}
