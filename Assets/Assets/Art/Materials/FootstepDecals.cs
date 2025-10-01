using UnityEngine;

public class FootstepDecals : MonoBehaviour
{
    [Header("Prefab de la huella")]
    public GameObject footprintPrefab; // arrastra aquí tu prefab Footprint_Quad

    [Header("Configuración")]
    public float footprintLifetime = 5f; // cuánto dura la huella antes de borrarse
    public Transform leftFoot;           // asigna el hueso/objeto del pie izquierdo
    public Transform rightFoot;          // asigna el hueso/objeto del pie derecho

    private bool isLeft = true; // alternar pies

    // Método para dejar huella (se puede llamar desde Animation Event)
    public void LeaveFootprint()
    {
        Transform foot = isLeft ? leftFoot : rightFoot;

        // Posición un poco por encima del suelo (para evitar clipping)
        Vector3 pos = foot.position + Vector3.up * 0.01f;

        // Rotación: plano hacia arriba + dirección del personaje
        Quaternion rot = Quaternion.Euler(90, transform.eulerAngles.y + (isLeft ? 0 : 180), 0);

        // Instanciar huella
        GameObject fp = Instantiate(footprintPrefab, pos, rot);
        Destroy(fp, footprintLifetime);

        // Cambiar al otro pie
        isLeft = !isLeft;
    }
}
