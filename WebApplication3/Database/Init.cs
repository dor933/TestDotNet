using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

public static class DatabaseExtensions
{
    public static void EnsureDatabaseCreated(this IApplicationBuilder app, string fileName)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var services = scope.ServiceProvider;
            var configuration = services.GetRequiredService<IConfiguration>();
            var logger = services.GetRequiredService<ILogger<Program>>();

            var originalConnectionString = configuration.GetConnectionString("DefaultConnection");

            var builder = new SqlConnectionStringBuilder(originalConnectionString);
            string targetDatabaseName = builder.InitialCatalog;

            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;

            bool dbExists = false;

            try
            {
                using (var connection = new SqlConnection(masterConnectionString))
                {
                    connection.Open();

                    // 1. Check if DB exists in sys.databases
                    var checkCmdText = "SELECT 1 FROM sys.databases WHERE name = @name";
                    using (var checkCmd = new SqlCommand(checkCmdText, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@name", targetDatabaseName);
                        var result = checkCmd.ExecuteScalar();
                        dbExists = (result != null);
                    }

                    // 2. If it doesn't exist, create it
                    if (!dbExists)
                    {
                        logger.LogInformation($"Database {targetDatabaseName} does not exist. Creating...");
                        var createCmdText = $"CREATE DATABASE [{targetDatabaseName}]";
                        using (var createCmd = new SqlCommand(createCmdText, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                        logger.LogInformation("Database created.");
                    }
                    else
                    {
                        logger.LogInformation("Database already exists. Skipping creation and scheme script.");
                    }
                }

                // 3. ONLY run the scheme script if the DB was just created
                if (!dbExists)
                {
                    RunScript(originalConnectionString!, fileName, logger);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while setting up the database.");
                throw;
            }
        }
    }

    private static void RunScript(string connectionString, string fileName, ILogger logger)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(filePath))
        {
            logger.LogError($"Script file not found: {filePath}");
            return;
        }

        var scriptContent = File.ReadAllText(filePath);

        // Split by "GO" for valid SQL execution
        var commandStrings = Regex.Split(
            scriptContent,
            @"^\s*GO\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase
        );

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            foreach (var commandText in commandStrings)
            {
                if (string.IsNullOrWhiteSpace(commandText)) continue;

                using (var command = new SqlCommand(commandText, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            logger.LogInformation("Scheme.sql executed successfully.");
        }
    }
}