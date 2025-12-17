using WebApplication3.Interfaces;

/// <summary>
/// A background service that performs daily maintenance tasks on product inventory.
/// Runs at a scheduled time (02:00) to increment product stock quantities.
/// </summary>
public class DailyService : BackgroundService
{
    private readonly ILogger<DailyService> _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider for creating scoped services.</param>
    public DailyService(ILogger<DailyService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

    }

    /// <summary>
    /// Executes the background service, running daily maintenance at the scheduled time.
    /// </summary>
    /// <param name="stoppingToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstRunDelay = CalculateDelayForNextRun(02, 00);

        _logger.LogInformation($"Service started. Waiting {firstRunDelay} until first run.");

        try
        {
            //ensure that the first run will be in the hour we set in CalculateDelayForNextRun
            await Task.Delay(firstRunDelay, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            return; 
        }

        //will run in interval of 24 hours 
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
            //will add 2 quantities for each product
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductsRepository>();
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
