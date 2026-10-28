namespace Hookline.Application.Services
{
    public interface IUrlValidator
    {
        Task<bool> IsSafe(string url);
    }
}
