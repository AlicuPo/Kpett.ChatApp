using Kpett.ChatApp.Helper;

namespace Kpett.ChatApp.Services
{
    public interface IToken
    {
        UserClaims GetUserClaims();
    }
}
