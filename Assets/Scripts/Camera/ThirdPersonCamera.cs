using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    private Transform _target;
    public Transform Target
    {
        get => _target;
        set
        {
            if (value!=null || _target != value)
            {
                _target = value;
            }
        }
    }
    [SerializeField]private Vector3 offset=new(0,2,-5); // Смещение относительно игрока
    [SerializeField]private float zoom = 3f;
    
    private void Start()
    {   
        zoom = offset.magnitude;
        offset = offset.normalized;
    }

    private void LateUpdate()
    {
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        // Вычисляем целевую позицию камеры
        Quaternion playerRotation = Target.rotation;
        Vector3 desiredPosition = Target.position + playerRotation * (offset * zoom);

        // Проверка на препятствия
        if (Physics.Linecast(Target.position, desiredPosition, out RaycastHit hit))
        {
            desiredPosition = hit.point; // Перемещаем камеру к препятствию
        }

        transform.position = desiredPosition;

        // Камера смотрит на игрока
        transform.LookAt(Target.position + Vector3.up * 1f);
    }
}
