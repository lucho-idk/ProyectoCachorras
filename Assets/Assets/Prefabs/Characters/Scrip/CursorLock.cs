using UnityEngine;

public class LockCursor : MonoBehaviour
{
    void Start()
    {
        // Oculta el cursor
        Cursor.visible = false;
        // Lo bloquea en el centro de la pantalla
        Cursor.lockState = CursorLockMode.Locked;
    }
}