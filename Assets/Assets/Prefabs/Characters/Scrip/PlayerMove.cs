using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float RunSpeed = 7;
    public float RotationSpeed = 250;
    public Animator animator;

    private float x, y;
    private Rigidbody rb;

    public float FuerzaDeSalto = 8f;
    public bool PuedoSaltar;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        PuedoSaltar = false;
    }

    void FixedUpdate()
    {
        transform.Rotate(0, x * Time.deltaTime * RotationSpeed, 0);
        transform.Translate(0, 0, y * Time.deltaTime * RunSpeed);
    }

    void Update()
    {
        x = Input.GetAxis("Horizontal");
        y = Input.GetAxis("Vertical");

        animator.SetFloat("VelX", x);
        animator.SetFloat("VelY", y);

        if (PuedoSaltar)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                animator.SetBool("Salte", true);
                rb.AddForce(new Vector3(0, FuerzaDeSalto, 0), ForceMode.Impulse);
                animator.SetBool("TocarSuelo", true);
            }
        }
        else
        {
            EstoyCayendo();
        }
    }

    public void EstoyCayendo()
    {
        animator.SetBool("TocarSuelo", false);
        animator.SetBool("Salte", false);
    }
}

