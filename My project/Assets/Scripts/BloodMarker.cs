using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BloodMarker : MonoBehaviour
{
    public float lifetime = 4f;

    void Start()
    {
        // Por si se olvidó en el inspector
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        Destroy(gameObject, lifetime);
    }
}
