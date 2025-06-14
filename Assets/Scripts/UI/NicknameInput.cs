using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class NicknameInput : MonoBehaviour
{
    private TMP_InputField _inputField;

    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
        _inputField.onValueChanged.AddListener(OnNicknameChanged);
        
        // Load saved nickname
        SaveData.Load();
        _inputField.text = SaveData.Nickname;
    }

    private void OnDestroy()
    {
        if (_inputField != null)
            _inputField.onValueChanged.RemoveListener(OnNicknameChanged);
    }

    private void OnNicknameChanged(string newNickname)
    {
        SaveData.Nickname = newNickname;
        SaveData.Save();
    }
}