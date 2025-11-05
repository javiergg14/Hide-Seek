using UnityEngine;

public class ZombieVision : MonoBehaviour
{
    public Camera frustumCam;    
    public LayerMask mask;        
    public Transform target;      
    public float visionRadius = 12f;
    public float rememberTime = 2f; 

    float lastSeenTime = -999f;
    Vector3 lastSeenPos;

    public bool PlayerVisible { get; private set; }
    public Vector3 LastSeenPos => lastSeenPos;

    void Update()
    {
        PlayerVisible = false;
        if (target == null || frustumCam == null) return;

        if (Vector3.Distance(transform.position, target.position) > visionRadius) return;

        var planes = GeometryUtility.CalculateFrustumPlanes(frustumCam);
        var bounds = new Bounds(target.position, Vector3.one * 0.5f);
        if (!GeometryUtility.TestPlanesAABB(planes, bounds)) return;

        Vector3 origin = frustumCam.transform.position;
        Vector3 dir = (target.position - origin).normalized;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, frustumCam.farClipPlane, mask))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                PlayerVisible = true;
                lastSeenTime = Time.time;
                lastSeenPos = target.position;
            }
        }

        if (!PlayerVisible && Time.time - lastSeenTime < rememberTime)
        {
            PlayerVisible = true;
        }
    }
}
