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

    [Header("Detección de Suelo")]
    public Transform groundCheck;       // Empty en los pies
    public float groundDistance = 0.2f; // Radio de detección
    public LayerMask groundMask;        // Asigna la capa del suelo
    private bool isGrounded;

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

        // 🔥 Detección de suelo
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        animator.SetBool("TocarSuelo", isGrounded);

        if (isGrounded)
        {
            PuedoSaltar = true;
        }
        else
        {
            EstoyCayendo();
        }

        if (PuedoSaltar)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                animator.SetBool("Salte", true);
                rb.AddForce(new Vector3(0, FuerzaDeSalto, 0), ForceMode.Impulse);
                animator.SetBool("TocarSuelo", true);
                PuedoSaltar = false;
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


