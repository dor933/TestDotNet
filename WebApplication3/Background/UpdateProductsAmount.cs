

using ProductInventoryApi.Repositories;

public class DailyCleanUpService : BackgroundService
{
    private readonly ILogger<DailyCleanUpService> _logger;
    private readonly IServiceProvider _serviceProvider;





    public DailyCleanUpService(ILogger<DailyCleanUpService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstRunDelay = CalculateDelayForNextRun(02, 00);

        _logger.LogInformation($"Service started. Waiting {firstRunDelay} until first run.");

        try
        {
            await Task.Delay(firstRunDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return; 
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        try
        {
            await DoWorkAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Daily service is stopping.");
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting daily maintenance...");

        using (var scope = _serviceProvider.CreateScope())
        {
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            await productRepository.IncrementProducts();
        }

        _logger.LogInformation("Daily maintenance finished.");
    }

    private TimeSpan CalculateDelayForNextRun(int hour, int minute)
    {
        var now = DateTime.Now;
        var nextRun = now.Date.AddHours(hour).AddMinutes(minute);

        if (now > nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }
}