using System.IO;
using System.Security.Cryptography;

namespace AssetVault.Helpers
{
    public static class FileHashService
    {
        public static string Generate(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);

            var hash = sha.ComputeHash(stream);

            return System.BitConverter
                .ToString(hash)
                .Replace("-", "")
                .ToLower();
        }
    }
}