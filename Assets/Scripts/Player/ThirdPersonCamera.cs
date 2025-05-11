using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Настройки камеры")]
    [SerializeField] private Transform target; // Цель (игрок)
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -5); // Смещение относительно игрока
    [SerializeField] private float minZoomDistance = 1f;
    [SerializeField] private float maxZoomDistance = 10f;
    [SerializeField] private float zoomSpeed = 2f;

    private float _currentZoom = 5f;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("Цель камеры не назначена!");
            enabled = false;
            return;
        }

        _currentZoom = offset.magnitude;
        offset = offset.normalized;
    }

    private void LateUpdate()
    {
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
        RaycastHit hit;
        if (Physics.Linecast(target.position, desiredPosition, out hit))
        {
            desiredPosition = hit.point; // Мгновенно перемещаем камеру к препятствию
        }

        // Мгновенное изменение позиции камеры
        transform.position = desiredPosition;

        // Камера смотрит на игрока
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}