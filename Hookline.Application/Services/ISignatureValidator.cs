namespace Hookline.Application.Services
{
    public interface ISignatureValidator
    {
        public bool IsValid(string source, string payload, IReadOnlyDictionary<string, string> headers);
        public bool TryConvertFromHexingString(string signature, out byte[] providedHash);
    }
}
