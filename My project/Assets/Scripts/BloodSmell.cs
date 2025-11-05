using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class BloodSmell : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        gameObject.tag = "SmellSource";

        Destroy(gameObject, 4f);
    }
}
