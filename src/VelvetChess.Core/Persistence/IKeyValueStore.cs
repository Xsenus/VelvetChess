namespace VelvetChess.Core.Persistence;

public interface IKeyValueStore
{
    string GetString(string key, string defaultValue = "");
    int GetInt(string key, int defaultValue = 0);
    bool GetBool(string key, bool defaultValue);
    void SetString(string key, string value);
    void SetInt(string key, int value);
    void SetBool(string key, bool value);
    void Remove(string key);
}
