using System.Linq;
using Mirror;
using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private Transform followTarget;

    [Header("Camera & Shooting")]
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
    private static readonly int Strafe = Animator.StringToHash("Strafe");
    private static readonly int IsAiming = Animator.StringToHash("IsAiming");

    private Vector2 _smoothInput;
    private float _animatorSpeed;
    private float _animatorStrafe;

    // Delta-based animation sync tracking
    private float _lastSentSpeed;
    private float _lastSentStrafe;
    private bool _lastSentAiming;
    private const float AnimSyncThreshold = 0.05f;

    // Network synced animation values
    [SyncVar(hook = nameof(OnSpeedChanged))]
    private float _networkSpeed;

    [SyncVar(hook = nameof(OnStrafeChanged))]
    private float _networkStrafe;

    [SyncVar(hook = nameof(OnIsAimingChanged))]
    private bool _networkIsAiming;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

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
        if (isLocalPlayer)
        {
            HandleShooting();
            HandleMouseLook();
            HandleMovement();
        }
    }

    private void HandleShooting()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            if (gun.Charge())
            {
                _isAiming = true;
                _freeLookCam[0].gameObject.SetActive(false);
                SendAnimationUpdate(true);
            }
        }

        if (Input.GetButtonUp("Fire1"))
        {
            if (gun.Fire())
            {
                _isAiming = false;
                _freeLookCam[0].gameObject.SetActive(true);
                SendAnimationUpdate(true);
            }
        }

        if (Input.GetButtonDown("Fire2") && _isAiming)
        {
            gun.CancelCharge();
            _isAiming = false;
            _freeLookCam[0].gameObject.SetActive(true);
            SendAnimationUpdate(true);
        }
    }

    private void HandleMouseLook()
    {
        _look.x = Input.GetAxis("Mouse X");
        _look.y = -Input.GetAxis("Mouse Y");

        Vector3 originalDirection = followTarget.forward;

        if (_isAiming && _autoAimSystem)
        {
            Transform target = _autoAimSystem.GetBestTarget(originalDirection);
            if (target)
            {
                Vector3 adjustedDirection = _autoAimSystem.GetAdjustedAimDirection(originalDirection, target);
                followTarget.rotation = Quaternion.LookRotation(adjustedDirection);
            }
        }

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
        Vector2 rawInput = new Vector2(horizontal, vertical);

        _smoothInput = Vector2.Lerp(_smoothInput, rawInput, Time.deltaTime * 5f);

        Vector2 inputVector = _smoothInput;
        if (inputVector.magnitude > 1f)
            inputVector.Normalize();

        Vector3 moveDirection = Vector3.zero;
        if (inputVector.magnitude > 0.01f)
        {
            moveDirection = transform.TransformDirection(new Vector3(inputVector.x, 0f, inputVector.y)) * moveSpeed;
        }

        float targetSpeed = inputVector.y * moveSpeed;
        _animatorSpeed = Mathf.Lerp(_animatorSpeed, targetSpeed, Time.deltaTime * 5f);

        float targetStrafe = inputVector.x * moveSpeed;
        _animatorStrafe = Mathf.Lerp(_animatorStrafe, targetStrafe, Time.deltaTime * 5f);

        // Delta-based sync: only send when values actually change
        SendAnimationUpdate(false);

        _velocity.y += gravity * Time.deltaTime;
        Vector3 finalMovement = moveDirection + Vector3.up * _velocity.y;
        _controller.Move(finalMovement * Time.deltaTime);
    }

    private void SendAnimationUpdate(bool force)
    {
        bool changed = force ||
            Mathf.Abs(_animatorSpeed - _lastSentSpeed) > AnimSyncThreshold ||
            Mathf.Abs(_animatorStrafe - _lastSentStrafe) > AnimSyncThreshold ||
            _isAiming != _lastSentAiming;

        if (changed)
        {
            CmdSetAnimationValues(_animatorSpeed, _animatorStrafe, _isAiming);
            _lastSentSpeed = _animatorSpeed;
            _lastSentStrafe = _animatorStrafe;
            _lastSentAiming = _isAiming;
        }
    }

    [Command]
    private void CmdSetAnimationValues(float speed, float strafe, bool isAiming)
    {
        _networkSpeed = speed;
        _networkStrafe = strafe;
        _networkIsAiming = isAiming;
    }

    private void OnSpeedChanged(float oldValue, float newValue)
    {
        animator?.SetFloat(Speed, newValue);
    }

    private void OnStrafeChanged(float oldValue, float newValue)
    {
        animator?.SetFloat(Strafe, newValue);
    }

    private void OnIsAimingChanged(bool oldValue, bool newValue)
    {
        animator?.SetBool(IsAiming, newValue);
    }
}
