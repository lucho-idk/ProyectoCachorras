using UnityEngine;

public class FootstepRaycast : MonoBehaviour
{
    [Header("Prefab de la huella")]
    public GameObject footprintPrefab;

    [Header("Configuraci�n")]
    public float footprintLifetime = 5f; // cu�nto dura la huella
    public float raycastDistance = 0.2f; // qu� tan lejos del pie se revisa
    public Transform leftFoot;           // asignar hueso del pie izquierdo
    public Transform rightFoot;          // asignar hueso del pie derecho

    private bool leftFootOnGround = false;
    private bool rightFootOnGround = false;

    void Update()
    {
        CheckFoot(leftFoot, ref leftFootOnGround, true);
        CheckFoot(rightFoot, ref rightFootOnGround, false);
    }

    void CheckFoot(Transform foot, ref bool footOnGround, bool isLeft)
    {
        RaycastHit hit;
        if (Physics.Raycast(foot.position, Vector3.down, out hit, raycastDistance))
        {
            if (!footOnGround) // reci�n toca el suelo
            {
                LeaveFootprint(hit.point, isLeft);
                footOnGround = true;
            }
        }
        else
        {
            footOnGround = false;
        }
    }

    void LeaveFootprint(Vector3 pos, bool isLeft)
    {
        // Rotaci�n: plano hacia arriba + direcci�n del personaje
        float angle = transform.eulerAngles.y + (isLeft ? 0 : 180);
        Quaternion rot = Quaternion.Euler(90, angle, 0);

        // Instanciar huella
        GameObject fp = Instantiate(footprintPrefab, pos + Vector3.up * 0.01f, rot);
        Destroy(fp, footprintLifetime);
    }
}
