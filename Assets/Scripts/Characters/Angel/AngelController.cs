using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

public class AngelController : MonoBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private Transform followTarget;
    
    private AutoAimSystem _autoAimSystem;
    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private CinemachineCamera[] _freeLookCam;
    private Vector2 _look;
    
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        _autoAimSystem = GetComponent<AutoAimSystem>();
        _freeLookCam = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        _freeLookCam = _freeLookCam
            .OrderByDescending(cam => cam.Priority.Value)
            .ToArray();

        foreach (var cam in _freeLookCam)
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
        HandleJump();
        HandleMouseLook();
        HandleMovement();
    }
    
    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void HandleMouseLook()
    {
        _look.x = Input.GetAxis("Mouse X");
        _look.y = -Input.GetAxis("Mouse Y");
        
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
}
