namespace Kpett.ChatApp.Helper
{
    public class AppException : Exception
    {
        public string ErrorCode { get; set; }
        public int StatusCode { get; }
        public AppException(string errorCode, int statusCode, string message) : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
