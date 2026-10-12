using System.Text;
using HappyGopher.WebProxy;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapGet("/test", async (CancellationToken cancellationToken) =>
{
    var client = new GopherClient();
    var responseBytes = await client.FetchAsync("gopher.kgivler.com", 70, "/", cancellationToken);

    return Results.Text(Encoding.ASCII.GetString(responseBytes));
});

app.Run();

var gopherClient = new GopherClient();
var responseBytes = await gopherClient.FetchAsync("gopher.kgivler.com", 70, "/", CancellationToken.None);
