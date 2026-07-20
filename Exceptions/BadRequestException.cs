using Kpett.ChatApp.Helpers;

namespace Kpett.ChatApp.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string code, string message) : base(code, StatusCodes.Status400BadRequest, message) 
        {
        
        }
    }
}
