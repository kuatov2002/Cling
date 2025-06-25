using System.Linq;
using Mirror;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.4f;
    
    [Header("Камера FreeLook")]
    [SerializeField] private Gun gun;
    
    private AutoAimSystem autoAimSystem;
    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private CinemachineCamera[] freeLookCam;
    private Vector2 _look;
    private bool _isAiming = false;
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;
        
        autoAimSystem = GetComponent<AutoAimSystem>();
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

        HandleGroundCheck();
        HandleJump();
        HandleShooting();
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleGroundCheck()
    {
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundLayer);

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void HandleShooting()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            _isAiming = true;
            if (gun.Charge()) 
                freeLookCam[0].gameObject.SetActive(false);
        }

        if (Input.GetButtonUp("Fire1"))
        {
            _isAiming = false;
            if (gun.Fire()) 
                freeLookCam[0].gameObject.SetActive(true);
        }
    }

    private void HandleMouseLook()
    {
        _look.x = Input.GetAxis("Mouse X");
        _look.y = -Input.GetAxis("Mouse Y");

        Vector3 originalDirection = followTarget.forward;
        
        // Apply auto-aim when aiming
        if (_isAiming && autoAimSystem)
        {
            Transform target = autoAimSystem.GetBestTarget(originalDirection);
            if (target)
            {
                Vector3 adjustedDirection = autoAimSystem.GetAdjustedAimDirection(originalDirection, target);
                followTarget.rotation = Quaternion.LookRotation(adjustedDirection);
            }
        }
        
        // Standard mouse look
        followTarget.rotation *= Quaternion.AngleAxis(_look.x, Vector3.up);
        followTarget.rotation *= Quaternion.AngleAxis(_look.y, Vector3.right);

        var angles = followTarget.localEulerAngles;
        angles.z = 0;

        var angle = followTarget.localEulerAngles.x;
        if (angle is > 180 and < 280)
        {
            angles.x = 300;
        }
        else if (angle is < 180 and > 70)
        {
            angles.x = 70;
        }

        followTarget.localEulerAngles = angles;
        transform.rotation = Quaternion.Euler(0, followTarget.rotation.eulerAngles.y, 0);
        followTarget.localEulerAngles = new Vector3(angles.x, 0, 0);
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 inputVector = new Vector3(horizontal, 0f, vertical).normalized;

        Vector3 moveDirection = Vector3.zero;
        if (inputVector.magnitude > 0.01f)
        {
            moveDirection = transform.TransformDirection(inputVector) * moveSpeed;
        }

        _velocity.y += gravity * Time.deltaTime;
        Vector3 finalMovement = moveDirection + Vector3.up * _velocity.y;
        _controller.Move(finalMovement * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}
