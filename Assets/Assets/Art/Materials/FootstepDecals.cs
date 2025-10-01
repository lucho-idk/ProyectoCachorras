using UnityEngine;

public class FootstepDecals : MonoBehaviour
{
    [Header("Prefab de la huella")]
    public GameObject footprintPrefab; // arrastra aqu� tu prefab Footprint_Quad

    [Header("Configuraci�n")]
    public float footprintLifetime = 5f; // cu�nto dura la huella antes de borrarse
    public Transform leftFoot;           // asigna el hueso/objeto del pie izquierdo
    public Transform rightFoot;          // asigna el hueso/objeto del pie derecho

    private bool isLeft = true; // alternar pies

    // M�todo para dejar huella (se puede llamar desde Animation Event)
    public void LeaveFootprint()
    {
        Transform foot = isLeft ? leftFoot : rightFoot;

        // Posici�n un poco por encima del suelo (para evitar clipping)
        Vector3 pos = foot.position + Vector3.up * 0.01f;

        // Rotaci�n: plano hacia arriba + direcci�n del personaje
        Quaternion rot = Quaternion.Euler(90, transform.eulerAngles.y + (isLeft ? 0 : 180), 0);

        // Instanciar huella
        GameObject fp = Instantiate(footprintPrefab, pos, rot);
        Destroy(fp, footprintLifetime);

        // Cambiar al otro pie
        isLeft = !isLeft;
    }
}
