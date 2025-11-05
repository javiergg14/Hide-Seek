using UnityEngine;
using System;

public class SmellSensor : MonoBehaviour
{
    public event Action<Vector3> onSmell;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("SmellSource"))
        {
            onSmell?.Invoke(other.transform.position);
        }
    }

    // Debug visual del radio
    void OnDrawGizmosSelected()
    {
        var sc = GetComponent<SphereCollider>();
        if (!sc) return;
        Gizmos.DrawWireSphere(transform.position + sc.center, sc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
    }
}
