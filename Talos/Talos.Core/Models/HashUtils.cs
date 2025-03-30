using System.Security.Cryptography;
using System.Text;

namespace Talos.Core.Models
{
    public static class HashUtils
    {
        public static string ComputeSha256HashHexString(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
        public static byte[] ComputeSha256Hash(string input)
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(input));
        }
    }
}
