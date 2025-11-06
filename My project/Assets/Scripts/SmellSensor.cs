using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class SmellSensor : MonoBehaviour
{
    public string smellTag = "SmellSource";
    public float sampleNavmeshRadius = 2.0f;

    private ZombieController controller;

    void Awake()
    {
        controller = GetComponentInParent<ZombieController>();

        // Ajustes de trigger/rigidbody para que funcione el OnTriggerEnter de forma fiable
        var col = GetComponent<SphereCollider>();
        if (col) col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other || other.gameObject.tag != smellTag) return;

        Vector3 smellPos = other.transform.position;

        // Opcional: ajusta al NavMesh cercano para evitar puntos no navegables
        if (NavMesh.SamplePosition(smellPos, out NavMeshHit hit, sampleNavmeshRadius, NavMesh.AllAreas))
            smellPos = hit.position;

        if (controller)
            controller.OnSmellDetected(smellPos);
        else
            SendMessageUpwards("OnSmellDetected", smellPos, SendMessageOptions.DontRequireReceiver);
    }
}
