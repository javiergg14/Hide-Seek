using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ZombieFrustumPerception : MonoBehaviour
{
    public float checkInterval = 0.1f;
    private Renderer rend;
    private bool wasVisible = false;
    private float nextCheck;

    void Awake() { rend = GetComponent<Renderer>(); }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        var cam = Camera.main;
        if (!cam || !rend) return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        bool inFrustum = GeometryUtility.TestPlanesAABB(planes, rend.bounds);

        // Además, aseguramos que el zombie esté delante de la cámara (no detrás)
        Vector3 toZombie = transform.position - cam.transform.position;
        bool inFront = Vector3.Dot(cam.transform.forward, toZombie.normalized) > 0f;

        bool nowVisible = inFrustum && inFront;

        if (nowVisible && !wasVisible)
        {
            // Avisamos al script Zombie del root
            var brain = GetComponentInParent<Zombie>();
            if (brain) brain.SendMessage("OnSeenByPlayerCamera", cam, SendMessageOptions.DontRequireReceiver);
        }

        wasVisible = nowVisible;
    }
}
