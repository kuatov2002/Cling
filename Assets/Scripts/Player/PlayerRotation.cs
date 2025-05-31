using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRotation : MonoBehaviour
{
    [Header("Настройки вращения")]
    [SerializeField] private float mouseSensitivity = 100f; // Чувствительность мыши
    [SerializeField] private bool smoothRotation = true; // Плавное вращение
    [SerializeField] private float rotationSmoothTime = 0.1f; // Время плавности

    private float _yaw = 0f; // Горизонтальное вращение
    private float _currentRotationVelocity = 0f;

    private void Start()
    {
        UIManager.Instance.LockCursor(true);
    }

    private void Update()
    {
        RotatePlayer();
    }

    private void RotatePlayer()
    {
        // Получаем ввод мыши
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;

        // Обновляем yaw
        _yaw += mouseX;
        _yaw = Mathf.Repeat(_yaw, 360); // Ограничиваем значениями 0-360

        // Плавное вращение
        if (smoothRotation)
        {
            float targetRotation = _yaw;
            float smoothedRotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetRotation,
                ref _currentRotationVelocity,
                rotationSmoothTime
            );
            transform.eulerAngles = new Vector3(0f, smoothedRotation, 0f);
        }
        else
        {
            transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        }
    }
}