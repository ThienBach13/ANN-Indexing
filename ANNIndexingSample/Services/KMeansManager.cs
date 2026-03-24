using System.Globalization;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
namespace ANNIndexingSample.Services
{
    internal class KMeansManager
    {
        public static async Task AssignMoviesToClustersAsync(string connString)
        {
            Console.WriteLine("Partitioning movies into semantic clusters...");

            using SqlConnection conn = new(connString);
            await conn.OpenAsync();

            string sql = @"
                            UPDATE m
                            SET m.cluster_id = c.cluster_id
                            FROM Movies m
                            CROSS APPLY (
                                SELECT TOP 1 cluster_id 
                                FROM MovieCentroids 
                                -- Added 'euclidean' as the 1st argument below
                                ORDER BY VECTOR_DISTANCE('euclidean', m.embedding, centroid_vector) ASC
                            ) c";

            using SqlCommand cmd = new(sql, conn);
            cmd.CommandTimeout = 120; // 10k rows might take a moment
            int rows = await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"Index built: {rows} movies assigned to clusters.");
        }
        public static async Task GenerateCentroidsAsync(string connString, int k = 100)
        {
            Console.WriteLine($"--- Generating {k} Initial Centroids ---");

            using SqlConnection conn = new(connString);
            await conn.OpenAsync();

            using SqlTransaction transaction = conn.BeginTransaction();

            try
            {
                // 1. Clear previous index data
                string clearSql = "DROP TABLE IF EXISTS MovieCentroids; UPDATE Movies SET cluster_id = NULL;";
                using (SqlCommand clearCmd = new(clearSql, conn, transaction))
                {
                    await clearCmd.ExecuteNonQueryAsync();
                }
                string createTbSql = "CREATE TABLE MovieCentroids (\r\n    cluster_id INT PRIMARY KEY,\r\n    centroid_vector VECTOR(1536)\r\n);";
                using (SqlCommand createCmd = new(createTbSql, conn, transaction))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }
                // 2. Pick 100 random movies and promote them to Centroids
                // This ensures the centroids are actual valid vectors in your high-dimensional space
                string seedSql = $@"
                    INSERT INTO MovieCentroids (cluster_id, centroid_vector)
                    SELECT TOP (@K) 
                           ROW_NUMBER() OVER(ORDER BY NEWID()) as cluster_id, 
                           embedding
                    FROM Movies
                    WHERE embedding IS NOT NULL;";

                using (SqlCommand seedCmd = new(seedSql, conn, transaction))
                {
                    seedCmd.Parameters.AddWithValue("@K", k);
                    await seedCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                Console.WriteLine($"Step 1 Success: {k} Centroids created in MovieCentroids table.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Console.WriteLine($"Error during centroid generation: {ex.Message}");
            }
        }
        public static async Task<float> SearchANNAsync(string connString, float[] queryVector)
        {
            string vectorJson = $"[{string.Join(",", queryVector.Select(f => f.ToString(CultureInfo.InvariantCulture)))}]";

            // 1. Start Timing
            Stopwatch sw = Stopwatch.StartNew();

            using SqlConnection conn = new(connString);
            await conn.OpenAsync();

            string sql = @"
                DECLARE @TargetCluster INT;

                -- STEP 1: Find nearest centroid (Probe 1)
                SELECT TOP 1 @TargetCluster = cluster_id
                FROM MovieCentroids
                ORDER BY VECTOR_DISTANCE('euclidean', centroid_vector, CAST(@Query AS VECTOR(1536))) ASC;

                -- STEP 2: Search only in that cluster (Probe 2)
                SELECT TOP 5 title, genre, release_year
                FROM Movies
                WHERE cluster_id = @TargetCluster
                ORDER BY VECTOR_DISTANCE('euclidean', embedding, CAST(@Query AS VECTOR(1536))) ASC;";

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@Query", vectorJson);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"[ANN Match]: {reader.GetString(0)} ({reader.GetString(1)})");
            }

            // 2. Stop Timing
            sw.Stop();

            float elapsedMs = (float)sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"--- Search Time: {elapsedMs:F2} ms ---");
            return elapsedMs;
        }
        public static async Task<float> SearchExactAsync(string connString, float[] queryVector)
        {
            string vectorJson = $"[{string.Join(",", queryVector.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";
            Stopwatch sw = Stopwatch.StartNew();

            using SqlConnection conn = new(connString);
            await conn.OpenAsync();

            // No WHERE clause! We check every single embedding.
            string sql = @"
                        SELECT TOP 5 title, genre, release_year
                        FROM Movies
                        ORDER BY VECTOR_DISTANCE('euclidean', embedding, CAST(@Query AS VECTOR(1536))) ASC;";

            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@Query", vectorJson);

            using SqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()){}

            sw.Stop();
            return (float)sw.Elapsed.TotalMilliseconds;
        }
    }
}
