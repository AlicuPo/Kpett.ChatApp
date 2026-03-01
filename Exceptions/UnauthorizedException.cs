using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string code, string message) : base(code, StatusCodes.Status401Unauthorized, message)
        {
        }
    }
}
