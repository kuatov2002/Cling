using System.Linq;
using Mirror;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private Transform followTarget;
    
    [Header("Камера FreeLook")]
    [SerializeField] private Gun gun;
    
    [SerializeField] private Animator animator;
    
    private AutoAimSystem _autoAimSystem;
    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private CinemachineCamera[] _freeLookCam;
    private Vector2 _look;
    private bool _isAiming = false;
    private static readonly int Speed = Animator.StringToHash("Speed");
    private Vector2 _smoothInput;
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;
        
        _autoAimSystem = GetComponent<AutoAimSystem>();
        _freeLookCam = FindObjectsOfType<CinemachineCamera>();
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
        if (!isLocalPlayer) return;
        HandleShooting();
        HandleMouseLook();
        HandleMovement();
    }

    private void HandleShooting()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            if (gun.Charge())
            {
                _isAiming = true;
                animator?.SetBool(IsAiming, true);
                //_freeLookCam[0].gameObject.SetActive(false);
            }
        }

        if (Input.GetButtonUp("Fire1"))
        {
            if (gun.Fire())
            {
                _isAiming = false;
                animator?.SetBool(IsAiming, false);
                //_freeLookCam[0].gameObject.SetActive(true);
            }
        }

        // Cancel charge on right mouse button
        if (Input.GetButtonDown("Fire2") && _isAiming)
        {
            gun.CancelCharge();
            _isAiming = false;
            animator?.SetBool(IsAiming, false);
            //_freeLookCam[0].gameObject.SetActive(true);
        }
    }

    private void HandleMouseLook()
    {
        _look.x = Input.GetAxis("Mouse X");
        _look.y = -Input.GetAxis("Mouse Y");

        Vector3 originalDirection = followTarget.forward;
        
        // Apply auto-aim when aiming
        if (_isAiming && _autoAimSystem)
        {
            Transform target = _autoAimSystem.GetBestTarget(originalDirection);
            if (target)
            {
                Vector3 adjustedDirection = _autoAimSystem.GetAdjustedAimDirection(originalDirection, target);
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

    private float _animatorSpeed;
    private float _animatorStrafe;
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");
    private static readonly int Strafe = Animator.StringToHash("Strafe");

    private void HandleMovement()
    {
        // Получаем "сырой" ввод
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector2 rawInput = new Vector2(horizontal, vertical);

        // Плавно интерполируем реальный ввод
        _smoothInput = Vector2.Lerp(_smoothInput, rawInput, Time.deltaTime * 5f);

        // Нормализуем, чтобы сохранить круглую зону движения (не ромб)
        Vector2 inputVector = _smoothInput;
        if (inputVector.magnitude > 1f)
            inputVector.Normalize();

        // Вычисляем направление движения
        Vector3 moveDirection = Vector3.zero;
        if (inputVector.magnitude > 0.01f)
        {
            moveDirection = transform.TransformDirection(new Vector3(inputVector.x, 0f, inputVector.y)) * moveSpeed;
        }

        // Плавное изменение Speed для аниматора
        float targetSpeed = inputVector.y * moveSpeed;
        _animatorSpeed = Mathf.Lerp(_animatorSpeed, targetSpeed, Time.deltaTime * 5f);
        animator?.SetFloat(Speed, _animatorSpeed);

        // Плавное изменение Strafe для аниматора
        float targetStrafe = inputVector.x * moveSpeed;
        _animatorStrafe = Mathf.Lerp(_animatorStrafe, targetStrafe, Time.deltaTime * 5f);
        animator?.SetFloat(Strafe, _animatorStrafe);

        // Гравитация и движение
        _velocity.y += gravity * Time.deltaTime;
        Vector3 finalMovement = moveDirection + Vector3.up * _velocity.y;
        _controller.Move(finalMovement * Time.deltaTime);
    }
}