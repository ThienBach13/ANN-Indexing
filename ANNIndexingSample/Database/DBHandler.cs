using ANNIndexingSample.Database;
using Microsoft.Data.SqlClient;
using OpenAI.Embeddings;

namespace ANNIndexingSample
{
    internal class DBHandler
    {
        public static string GetConnectionString()
        {
            string server = Environment.GetEnvironmentVariable("DB_SERVER") ?? "";
            string database = Environment.GetEnvironmentVariable("DB_NAME") ?? "";
            string user = Environment.GetEnvironmentVariable("DB_USER") ?? "";
            string password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = server,
                InitialCatalog = database,
                UserID = user,
                Password = password,
                Encrypt = true,
                TrustServerCertificate = false,
                ConnectTimeout = 30
            };

            return builder.ConnectionString;
        }

        public static async Task CheckDatabaseStatus(string connectionString)
        {
            Console.WriteLine("--- Checking Database Status ---");
            using SqlConnection conn = new(connectionString);
            try
            {
                await conn.OpenAsync();
                Console.WriteLine("Connection successful!");
                // (Existing table listing logic here...)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Offline: {ex.Message}");
            }
        }

        public static async Task SeedDatabaseAsync(string connString, OpenAI.Embeddings.EmbeddingClient client)
        {
            Console.WriteLine("Starting Seeding Process...");
            await DataSeeder.SeedMoviesFromCsvAsync(connString, client);
            Console.WriteLine("Seeding Complete.");
        }
    }
}