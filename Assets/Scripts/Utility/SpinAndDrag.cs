using UnityEngine;

public class SpinAndDrag : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation around Y-axis in degrees per second")]
    [SerializeField] private float rotationSpeed = 30.0f;

    [Header("Vertical Movement Settings")]
    [Tooltip("Speed of up and down movement")]
    [SerializeField] private float verticalSpeed = 1.0f;
    
    [Tooltip("Maximum distance the object will move up and down from its starting position")]
    [SerializeField] private float verticalDistance = 0.5f;

    private Vector3 _initialLocalPosition;
    
    private void Start()
    {
        _initialLocalPosition = transform.localPosition; // Сохраняем начальную локальную позицию
    }
    private void Update()
    {
        // Вращение вокруг локальной оси Y
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.Self);
        
        // Вертикальное колебание относительно родителя
        float verticalOffset = Mathf.Sin(Time.time * verticalSpeed) * verticalDistance;
        
        // Обновляем только Y-компонент локальной позиции
        transform.localPosition = new Vector3(
            _initialLocalPosition.x,
            _initialLocalPosition.y + verticalOffset,
            _initialLocalPosition.z
        );
    }
}
