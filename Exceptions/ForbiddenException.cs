using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string code, string message) : base(code, StatusCodes.Status403Forbidden, message)
        {
        }
    }
}
