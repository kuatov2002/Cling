using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class NicknameInput : MonoBehaviour
{
    [SerializeField] private AlertForNickname alert;
    [SerializeField] private int minLength = 3;
    [SerializeField] private int maxLength = 16;
    
    private TMP_InputField _inputField;
    private string _lastValidNickname = "";

    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
    
        // Load saved nickname
        SaveData.Load();
        string savedNick = SaveData.Nickname;
        _inputField.text = savedNick;
        _lastValidNickname = savedNick;

        // Добавляем слушателя после инициализации
        _inputField.onEndEdit.AddListener(OnNicknameChanged);
    }

    private void OnDestroy()
    {
        if (_inputField)
            _inputField.onValueChanged.RemoveListener(OnNicknameChanged);
    }

    private void OnNicknameChanged(string newNickname)
    {
        string validationError = ValidateNickname(newNickname);
    
        if (validationError == null)
        {
            _inputField.text = newNickname;
            _lastValidNickname = newNickname;
            SaveData.Nickname = newNickname;
            SaveData.Save();
        }
        else
        {
            if (alert)
                alert.ShowError(validationError);
            
            _inputField.text = _lastValidNickname; // Откатываем к последнему валидному
        }
    }

    private string ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return "Введите никнейм"; // Запрещаем пустой никнейм
        
        if (nickname.Length < minLength)
            return $"Минимум {minLength} символов";
        
        if (nickname.Length > maxLength)
            return $"Максимум {maxLength} символов";
        
        Regex reg = new Regex("^[A-Za-z0-9]+$");
        if (!reg.IsMatch(nickname))
            return "Только буквы A-Z и цифры 0-9";
        
        return null; // Валидация прошла успешно
    }
}