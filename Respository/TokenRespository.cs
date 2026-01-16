using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services;
using System.Security.Claims;


namespace Kpett.ChatApp.Reposoitory
{
    public class TokenRespository : IToken
    {
       
        private readonly IHttpContextAccessor _Accessor;
        public TokenRespository( IHttpContextAccessor Accessor)
        {
            _Accessor = Accessor;
        }

        public UserClaims GetUserClaims()
        {
            var user = _Accessor.HttpContext?.User;

            if (user == null || !user.Identity!.IsAuthenticated)
                return null;

            return new UserClaims(
                Id: user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                Name: user.FindFirstValue(ClaimTypes.Name) ?? "",
                Role: user.FindFirstValue(ClaimTypes.Role) ?? "",
                Avatar: user.FindFirstValue("Avatar") ?? "", 
                Email: user.FindFirstValue(ClaimTypes.Email) ?? ""
            );
        }
    }

}
