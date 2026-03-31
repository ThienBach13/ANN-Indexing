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
            Console.WriteLine("\n--- 🔍 Checking Database Status ---");

            // Use the builder to extract info safely for display
            var builder = new SqlConnectionStringBuilder(connectionString);

            using SqlConnection conn = new(connectionString);
            try
            {
                await conn.OpenAsync();

                // Get counts for your report
                int movieCount = await GetCount(conn, "Movies");
                int centroidCount = await GetCount(conn, "MovieCentroids");
                int indexedCount = await GetCount(conn, "Movies WHERE cluster_id IS NOT NULL");

                Console.WriteLine("✅ Connection: SUCCESSFUL");
                Console.WriteLine($"📍 Server:     {builder.DataSource}");
                Console.WriteLine($"📂 Database:   {builder.InitialCatalog}");
                Console.WriteLine("------------------------------------------");
                Console.WriteLine($"🎬 Total Movies:    {movieCount:N0}");
                Console.WriteLine($"📍 Total Clusters:  {centroidCount:N0}");
                Console.WriteLine($"🔗 Indexed Movies:  {indexedCount:N0}");

                if (indexedCount < movieCount && movieCount > 0)
                {
                    Console.WriteLine("⚠️  Warning: Index is out of date. Please run Option 3.");
                }
                Console.WriteLine("------------------------------------------\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Connection: OFFLINE");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        private static async Task<int> GetCount(SqlConnection conn, string tableWithCriteria)
        {
            try
            {
                string sql = $"SELECT COUNT(*) FROM {tableWithCriteria}";
                using SqlCommand cmd = new(sql, conn);
                return (int)(await cmd.ExecuteScalarAsync() ?? 0);
            }
            catch
            {
                return 0; // Table might not exist yet
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