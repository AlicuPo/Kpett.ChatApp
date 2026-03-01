using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string code, string message) : base(code, StatusCodes.Status409Conflict, message)
        {
        }
    }
}
