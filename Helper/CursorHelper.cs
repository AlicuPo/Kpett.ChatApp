using System.Text;
using System.Text.Json;

namespace Kpett.ChatApp.Helper
{
    public class CursorHelper
    {
        public static string Encode<T>(T payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes);
        }

        public static T? Decode<T>(string cursor)
        {
            try
            {
                var bytes = Convert.FromBase64String(cursor);
                var json = Encoding.UTF8.GetString(bytes);
                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default;
            }
        }
    }
}
