using System.Linq;
using Mirror;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform followTarget;
    [Header("Камера FreeLook")]
    

    [SerializeField] private Gun gun;

    private Rigidbody _rb;
    private bool _isGrounded;
    private CinemachineCamera[] freeLookCam;
    private Vector2 _look;
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;


        freeLookCam = FindObjectsOfType<CinemachineCamera>();
        freeLookCam = freeLookCam
            .OrderByDescending(cam => cam.Priority.Value)
            .ToArray();
        
        
        foreach (var cam in freeLookCam)
        {
            if (cam)
            {
                cam.Follow = followTarget;
                cam.LookAt = followTarget;
            }
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false;
        }

        if (Input.GetButtonDown("Fire1"))
        {
            gun.Charge();
            freeLookCam[0].gameObject.SetActive(false);
        }

        if (Input.GetButtonUp("Fire1"))
        {
            gun.Fire();
            freeLookCam[0].gameObject.SetActive(true);
        }

        _look.x = Input.GetAxis("Mouse X");
        _look.y = -Input.GetAxis("Mouse Y");
        followTarget.rotation *= Quaternion.AngleAxis(_look.x,Vector3.up);
        followTarget.rotation *= Quaternion.AngleAxis(_look.y,Vector3.right);

        var angles = followTarget.localEulerAngles;
        angles.z = 0;

        var angle = followTarget.localEulerAngles.x;

        if (angle > 180 && angle < 280)
        {
            angles.x = 300;
        }else if (angle < 180 && angle > 70)
        {
            angles.x = 70;
        }

        followTarget.localEulerAngles = angles;

        transform.rotation = Quaternion.Euler(0, followTarget.rotation.eulerAngles.y, 0);
        followTarget.localEulerAngles = new Vector3(angles.x, 0, 0);
        
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 inputVector = new Vector3(horizontal, 0f, vertical).normalized;
    
        if (inputVector.magnitude > 0.01f)
        {
            Vector3 moveDirection = transform.TransformDirection(inputVector);
            Vector3 targetVelocity = moveDirection * moveSpeed;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            _rb.linearVelocity = new Vector3(0, _rb.linearVelocity.y, 0);
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;



        _isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.2f, groundLayer);
    }
}