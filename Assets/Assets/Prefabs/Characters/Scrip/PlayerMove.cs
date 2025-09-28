using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float RunSpeed = 7;
    public float RotationSpeed = 250;
    public Animator animator;

    private float x, y;





    void Update()
    {
        x = Input.GetAxis("Horizontal");
        y = Input.GetAxis("Vertical");

        transform.Rotate(0, x * Time.deltaTime * RotationSpeed, 0);

        transform.Translate(0, 0, y * Time.deltaTime * RunSpeed);

        animator.SetFloat("VelX", x);
        animator.SetFloat("VelY", y);

    }
}
