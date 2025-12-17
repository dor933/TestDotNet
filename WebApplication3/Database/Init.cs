using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

/// <summary>
/// Provides extension methods for database initialization and setup.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Ensures the database and required tables are created.
    /// Creates the database if it doesn't exist and executes the schema script if tables are missing.
    /// </summary>
    /// <param name="app">The application builder instance.</param>
    /// <param name="fileName">The name of the SQL schema file to execute.</param>
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

            // 1. Create Database if it doesn't exist
            builder.InitialCatalog = "master";
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                var checkCmd = new SqlCommand("SELECT 1 FROM sys.databases WHERE name = @name", connection);
                checkCmd.Parameters.AddWithValue("@name", targetDatabaseName);

                if (checkCmd.ExecuteScalar() == null)
                {
                    logger.LogInformation($"Database {targetDatabaseName} does not exist. Creating...");
                    using (var createCmd = new SqlCommand($"CREATE DATABASE [{targetDatabaseName}]", connection))
                    {
                        createCmd.ExecuteNonQuery();
                    }
                    logger.LogInformation("Database created.");
                }
            }

            // 2. Check if Tables exist in the specific database
            bool tablesExist = false;
            using (var connection = new SqlConnection(originalConnectionString))
            {
                connection.Open();
                // Check for a specific table you expect, e.g., "Products"
                var cmd = new SqlCommand("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Products'", connection);
                tablesExist = cmd.ExecuteScalar() != null;
            }

            // 3. Run script if tables are missing
            if (!tablesExist)
            {
                logger.LogInformation("Tables not found. Executing schema script...");
                RunScript(originalConnectionString!, fileName, logger);
            }
            else
            {
                logger.LogInformation("Database and tables already exist.");
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
