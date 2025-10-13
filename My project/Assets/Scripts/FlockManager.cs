using System.Collections.Generic;
using UnityEngine;

public class FlockManager : MonoBehaviour
{
    [Header("Configuración del enjambre")]
    public GameObject boidPrefab;
    public int cantidad = 30;
    public GameObject[] todosLosBoids;
    public Vector3 limites = new Vector3(50, 50, 50);

    [Header("Parámetros de comportamiento")]
    public float velocidadMin = 2f;
    public float velocidadMax = 5f;
    public float distanciaVecino = 3f;
    public float velocidadRotacion = 4f;

    [Header("Líder")]
    public Transform lider;
    public GameObject suelo;

    void Start()
    {
        todosLosBoids = new GameObject[cantidad];
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 tam = suelo.GetComponent<Renderer>().bounds.size;
            Vector3 pos = suelo.transform.position + new Vector3(
                Random.Range(-tam.x / 2, tam.x / 2),
                1f,
                Random.Range(-tam.z / 2, tam.z / 2)
            );

            GameObject nuevoBoid = Instantiate(boidPrefab, pos, Quaternion.identity);
            nuevoBoid.GetComponent<Flock>().miManager = this;
            todosLosBoids[i] = nuevoBoid;
        }
    }
}
