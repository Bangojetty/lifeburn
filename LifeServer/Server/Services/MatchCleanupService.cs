using Microsoft.Extensions.Hosting;
using Server.Controllers;

namespace Server.Services;

public class MatchCleanupService : BackgroundService {
    private readonly ILogger<MatchCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);
    private const int DisconnectTimeoutSeconds = 20;

    public MatchCleanupService(ILogger<MatchCleanupService> logger) {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Match Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested) {
            try {
                LifeController.CleanupInactiveMatches(DisconnectTimeoutSeconds);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error during match cleanup");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Match Cleanup Service stopped");
    }
}
