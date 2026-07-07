namespace Kpett.ChatApp.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime ToUtc(this DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Utc => dateTime,
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => dateTime
            };
        }

        public static DateTime? ToUtc(this DateTime? dateTime)
        {
            return dateTime.HasValue
                ? dateTime.Value.ToUtc()
                : null;
        }
    }
}

