using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string code, string message) : base(code, StatusCodes.Status400BadRequest, message) 
        {
        
        }
    }
}
