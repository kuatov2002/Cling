using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRotation : MonoBehaviour
{
    [Header("Настройки вращения")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    
    [Header("Ограничения вертикального угла")]
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;

    private float _yaw = 0f;
    private float _pitch = 0f;
    private float _currentRotationVelocity = 0f;
    private float _currentPitchVelocity = 0f;

    public float Pitch => _pitch;
    public float Yaw => _yaw;

    private void Start()
    {
        UIManager.Instance.LockCursor(true);
    }

    private void Update()
    {
        //RotatePlayer();
    }

    private void RotatePlayer()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        _yaw += mouseX;
        _yaw = Mathf.Repeat(_yaw, 360);
        
        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);

        if (smoothRotation)
        {
            float smoothedYaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                _yaw,
                ref _currentRotationVelocity,
                rotationSmoothTime
            );
            transform.eulerAngles = new Vector3(0f, smoothedYaw, 0f);
        }
        else
        {
            transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        }
    }
}