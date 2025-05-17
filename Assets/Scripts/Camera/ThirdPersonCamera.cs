using UnityEngine;
using Mirror;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Настройки камеры")]
    [SerializeField] private Transform target; // Цель (игрок)
    [SerializeField] private Vector3 offset; // Смещение относительно игрока
    [SerializeField] private float minZoomDistance = 1f;
    [SerializeField] private float maxZoomDistance = 10f;
    [SerializeField] private float zoomSpeed = 2f;

    [TriInspector.ShowInInspector, TriInspector.ReadOnly]
    private float _currentZoom = 5f;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    public override void OnStartLocalPlayer()
    {
        // Включаем камеру только для локального игрока
        if (_camera != null)
            _camera.enabled = true;

        // Инициализация зума после проверки локального игрока
        if (target == null)
        {
            Debug.LogError("Цель камеры не назначена!");
            enabled = false;
            return;
        }

        _currentZoom = offset.magnitude;
        offset = offset.normalized;
    }

    private void Start()
    {
        // Выключаем камеру и скрипт для всех не-локальных игроков
        if (!isLocalPlayer)
        {
            if (_camera != null)
                _camera.enabled = false;
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer) return;

        HandleInput();
        UpdateCameraPosition();
    }

    private void HandleInput()
    {
        // Зум колесиком мыши
        _currentZoom -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        _currentZoom = Mathf.Clamp(_currentZoom, minZoomDistance, maxZoomDistance);
    }

    private void UpdateCameraPosition()
    {
        // Вычисляем целевую позицию камеры
        Quaternion playerRotation = target.rotation;
        Vector3 desiredPosition = target.position + playerRotation * (offset * _currentZoom);

        // Проверка на препятствия
        if (Physics.Linecast(target.position, desiredPosition, out RaycastHit hit))
        {
            desiredPosition = hit.point; // Перемещаем камеру к препятствию
        }

        transform.position = desiredPosition;

        // Камера смотрит на игрока
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}
