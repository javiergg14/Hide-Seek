using UnityEngine;

public class SmellSensor : MonoBehaviour
{
    public ZombieAI zombie;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("SmellSource"))
        {
            zombie.SetInvestigateTarget(other.transform.position);
        }
    }
}
