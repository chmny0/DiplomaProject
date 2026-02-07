using System.Security.Cryptography;
using System.Text;

namespace WpfPlannerApp.Helpers
{
    public static class PasswordHelper
    {
        public static string Hash(string plain)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(plain);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
        }

        public static bool Verify(string plain, string hexHash)
        {
            if (string.IsNullOrWhiteSpace(hexHash))
                return false;

            return Hash(plain) == hexHash.ToUpperInvariant();
        }
    }
}
