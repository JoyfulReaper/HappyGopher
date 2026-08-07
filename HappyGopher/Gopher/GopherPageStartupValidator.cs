using HappyGopher.Pages;

namespace HappyGopher.Gopher;

public sealed class GopherPageStartupValidator(
    IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        _ = scope.ServiceProvider.GetRequiredService<GopherPageResolver>();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}