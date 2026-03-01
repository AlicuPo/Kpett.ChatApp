using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string code, string message)
            : base(code, StatusCodes.Status404NotFound, message) { }
    }
}
