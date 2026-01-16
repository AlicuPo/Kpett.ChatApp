using System.Security.Cryptography;
using System.Text;

namespace Kpett.ChatApp.Helper
{
    public class PasswordHasher
    {
        // Hàm tạo Hash và Salt khi Người dùng Đăng ký (Register)
        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key; // Key của HMAC chính là Salt
                passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        // Hàm kiểm tra mật khẩu khi Người dùng Đăng nhập (Login)
        public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt)) // Dùng Salt cũ để khởi tạo
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }
    }
}
