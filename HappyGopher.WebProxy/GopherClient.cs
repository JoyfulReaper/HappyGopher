using System.Net.Sockets;
using System.Text;

namespace HappyGopher.WebProxy;

public sealed class GopherClient
{
    public async Task<byte[]> FetchAsync(
        string host,
        int port,
        string selector,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);

        using var stream = client.GetStream();
        
        var request = Encoding.ASCII.GetBytes(selector + "\r\n");
        await stream.WriteAsync(request, cancellationToken);

        using var response = new MemoryStream();
        await stream.CopyToAsync(response, cancellationToken);
        return response.ToArray();
    }
}