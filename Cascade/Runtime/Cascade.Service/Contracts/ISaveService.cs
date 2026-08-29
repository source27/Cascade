namespace Cascade.Service
{
    public interface ISaveService
    {
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
        void Flush();
    }
}
