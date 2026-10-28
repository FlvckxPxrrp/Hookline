using Hookline.Application.Services;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Hookline.Infrastructure.Services
{
    internal class UrlValidator : IUrlValidator
    {
        public async Task<bool> IsSafe(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if( uri.Scheme != Uri.UriSchemeHttp &&  uri.Scheme != Uri.UriSchemeHttps)
                return false;

            try
            {
                var ipAddress = await Dns.GetHostAddressesAsync(uri.Host);

                foreach(var ip in ipAddress)
                {
                    if (IsPrivateOrLocalIp(ip))
                        return false;
                }
            }
            catch(SocketException)
            {
                return false; 
            }

            return true;
        }

        private bool IsPrivateOrLocalIp(IPAddress ip)
        {
            if(IPAddress.IsLoopback(ip))
                return true; 

            //Пока только IPV4, IPV6 потом
            if(ip.AddressFamily == AddressFamily.InterNetwork)
                return true;

            byte[] bytes = ip.GetAddressBytes();

            // проверка на 10.0.0.0 - 10.255.255.255
            if (bytes[0] == 10)
                return true;

            // 176.16.0.0 - 172.31.255.255
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0 - 192.168.255.255
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // 169.254.0.0 - 169.254.255.255
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            // 0.0.0.0
            if (bytes[0] == 0)
                return true;

            return false; 
        }

    }
}
