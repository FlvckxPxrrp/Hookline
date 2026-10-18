using Hookline.Application.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Hookline.Infrastructure.Security
{
    public class GitHubSignatureValidator : ISignatureValidator
    {
        private readonly string _secret;

        public GitHubSignatureValidator(IConfiguration configuration)
        {
            var secretFromConfig = configuration["GitHub:WebhookSecret"];

            if(string.IsNullOrEmpty(secretFromConfig))
            {
                throw new InvalidOperationException(
                      "Секрет для GitHub не найден в конфигурации. Проверь ключ 'GitHub:WebhookSecret'.");
            }

            _secret = secretFromConfig;
        }

        public bool TryConvertFromHexingString(string signature, out byte[] providedHash)
        {
            providedHash = Array.Empty<byte>();
            string prefix = "sha256=";
            var providedSignature = signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? signature[prefix.Length..]
                : signature;
            
            try
            {
                providedHash = Convert.FromHexString(providedSignature);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public bool IsValid(string source, string payload, IReadOnlyDictionary<string, string> headers)
        {
            if (source.ToLowerInvariant() != "github")
                return true;

            if (!headers.TryGetValue("X-Hub-Signature-256", out var signature))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));


            if (!TryConvertFromHexingString(signature, out byte[] providedHash))
                return false;

            var computedHex = Convert.ToHexString(computedHash).ToLowerInvariant();
            var providedHex = Convert.ToHexString(providedHash).ToLowerInvariant();

            Console.WriteLine("========================================");
            Console.WriteLine($"Длина payload в C#: {payload.Length}");
            Console.WriteLine($"Payload в C#: '{payload}'");
            Console.WriteLine($"Секрет в C#: '{_secret}'");
            Console.WriteLine($"Хеш, посчитанный C#: {computedHex}");
            Console.WriteLine($"Хеш, полученный в заголовке: {providedHex}");
            Console.WriteLine("========================================");

            return CryptographicOperations.FixedTimeEquals(
            computedHash,
            providedHash);
        }
    }
}
