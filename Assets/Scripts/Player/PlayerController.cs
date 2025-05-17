using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpForce;
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private Gun _gun;

    private Rigidbody _rb;
    private bool _isGrounded;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if(!isLocalPlayer) return;
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z); // Сбрасываем вертикальную скорость
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            _isGrounded = false;
        }
        if (Input.GetButtonDown("Fire1")) // Левая кнопка мыши
        {
            _gun.Fire();
        }
    }

    private void FixedUpdate()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;

        // 🔁 Преобразуем движение в локальную систему координат игрока
        movement = transform.TransformDirection(movement);

        // Прямое управление скоростью с учётом направления
        Vector3 targetVelocity = movement * moveSpeed;
        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);

        // Проверка на земле
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.2f, groundLayer);
    }
}