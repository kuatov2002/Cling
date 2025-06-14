using UnityEngine;

public static class SaveData
{
    public static string Nickname;

    public static void Save()
    {
        PlayerPrefs.SetString("Nickname",Nickname);
        PlayerPrefs.Save();
    }

    public static void Load()
    {
        Nickname = PlayerPrefs.GetString("Nickname");
    }
}
