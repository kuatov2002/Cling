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

    [Header("Камера FreeLook")]
    [SerializeField] private CinemachineCamera freeLookCam;

    [SerializeField] private Gun gun;

    private Rigidbody _rb;
    private bool _isGrounded;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnStartLocalPlayer()
    {
        if (!isLocalPlayer) return;

        if (freeLookCam == null)
        {
            freeLookCam = FindObjectOfType<CinemachineCamera>();
        }

        if (freeLookCam != null)
        {
            freeLookCam.Follow = transform;
            freeLookCam.LookAt = transform;
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
        }
        if (Input.GetButtonUp("Fire1"))
        {
            gun.Fire();
        }
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 inputVector = new Vector3(horizontal, 0f, vertical).normalized;
        bool isMoving = inputVector.magnitude > 0.01f;

        Vector3 moveDirection = Vector3.zero;

        if (isMoving && freeLookCam != null)
        {
            // Получаем угол Y камеры
            float cameraYAngle = freeLookCam.transform.eulerAngles.y;
            
            // Направление движения относительно камеры
            moveDirection = Quaternion.Euler(0f, cameraYAngle, 0f) * inputVector;
            
            // Поворачиваем игрока к направлению движения
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.2f);
        }

        // Используем то же направление для движения
        Vector3 targetVelocity = moveDirection * moveSpeed;
        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);

        _isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.2f, groundLayer);
    }
}