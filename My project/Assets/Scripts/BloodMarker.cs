using UnityEngine;

public class BloodMarker : MonoBehaviour
{
    void Start()
    {
        // Garantiza que tiene el tag correcto (por si se te olvida en el prefab)
        gameObject.tag = "SmellSource";
        Destroy(gameObject, 4f);
    }
}
