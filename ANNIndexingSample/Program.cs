using ANNIndexingSample.Database;
using ANNIndexingSample.Helper;
using ANNIndexingSample.Services;
using OpenAI.Embeddings;

namespace ANNIndexingSample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string connString = DBHandler.GetConnectionString();
            EmbeddingClient client = new("text-embedding-3-small", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

            await DBHandler.CheckDatabaseStatus(connString).ConfigureAwait(true);

            bool running = true;
            while (running)
            {
                Console.WriteLine("\n--- MOVIE VECTOR SYSTEM ---");
                Console.WriteLine("1. Seeding Movies Data (CSV -> SQL)");
                Console.WriteLine("2. Generating Centroids (User-defined Clusters)");
                Console.WriteLine("3. Assigning Clusters (Rebuild ANN Index)");
                Console.WriteLine("4. Searching Movies (ANN Mode)");
                Console.WriteLine("5. Searching Movies (Exact Mode - Brute Force)");
                Console.WriteLine("6. Restarting Database Connection"); 
                Console.WriteLine("7. Exit");
                Console.Write("\nSelect Option: ");

                string choice = Console.ReadLine() ?? "";

                switch (choice)
                {
                    case "1":
                        await DBHandler.SeedDatabaseAsync(connString, client).ConfigureAwait(true);
                        break;
                    case "2":
                        Console.Write("How many clusters (centroids) do you want to create? (e.g., 100): ");
                        if (int.TryParse(Console.ReadLine(), out int k) && k > 0)
                        {
                            await KMeansManager.GenerateCentroidsAsync(connString, k).ConfigureAwait(true);
                        }
                        break;
                    case "3":
                        await KMeansManager.AssignMoviesToClustersAsync(connString).ConfigureAwait(true);
                        break;
                    case "4":
                        await RunANNSearch(connString, client).ConfigureAwait(true);
                        break;
                    case "5":
                        await RunExactSearch(connString, client).ConfigureAwait(true);
                        break;
                    case "6":
                        await DBHandler.CheckDatabaseStatus(connString).ConfigureAwait(true);
                        break;
                    case "7":
                        running = false;
                        break;
                    default:
                        Console.WriteLine("Invalid option. Try again.");
                        break;
                }
            }
        }
        static async Task RunANNSearch(string conn, EmbeddingClient ec)
        {

            Console.Write("\nEnter search query (or 'back'): ");
            string query = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "back") return;

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                // Generate embedding once (saves API costs)
                var embeddingResult = await ec.GenerateEmbeddingsAsync([query]).ConfigureAwait(true);
                float[] queryVector = embeddingResult.Value[0].ToFloats().ToArray();

                float time = await KMeansManager.SearchANNAsync(conn, queryVector).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search Error: {ex.Message}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
        static async Task RunExactSearch(string conn, EmbeddingClient ec)
        {
            Console.Write("\nEnter search query for EXACT search (or 'back'): ");
            string query = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "back") return;

            try
            {
                Console.WriteLine("✨ Generating embedding...");
                var embeddingResult = await ec.GenerateEmbeddingsAsync([query]).ConfigureAwait(true);
                float[] queryVector = embeddingResult.Value[0].ToFloats().ToArray();

                Console.WriteLine("🐢 Running Brute-Force Exact Search (Scanning all rows)...");
                float time = await KMeansManager.SearchExactAsync(conn, queryVector).ConfigureAwait(true);

                Console.WriteLine($"\n🐢 Exact Search Time: {time:F2} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Search Error: {ex.Message}");
            }
        }
        static async Task RunExactSearchLoop(string conn, EmbeddingClient ec)
        {
            // 1. Get trial quantity
            Console.Write("\n[EXACT MODE] Quantity of trials for benchmarking: ");
            if (!int.TryParse(Console.ReadLine(), out int searchCnt)) searchCnt = 1;

            // 2. Get search query
            Console.Write("Enter search query (or 'back'): ");
            string query = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "back") return;

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                // 3. Generate embedding ONCE (saves OpenAI API costs)
                Console.WriteLine("✨ Generating query vector...");
                var embeddingResult = await ec.GenerateEmbeddingsAsync([query]).ConfigureAwait(true);
                float[] queryVector = embeddingResult.Value[0].ToFloats().ToArray();

                List<float> recordedTimes = new();

                Console.WriteLine($"🐢 Starting {searchCnt} Exact Search trials...");

                // 4. Run the Brute-Force loop
                for (int i = 0; i < searchCnt; i++)
                {
                    // Call the Exact (Linear) search method
                    float time = await KMeansManager.SearchExactAsync(conn, queryVector).ConfigureAwait(true);

                    recordedTimes.Add(time);
                    Console.WriteLine($"Trial {i + 1}: {time:F2} ms");
                }

                // 5. Save results to Output/records.txt
                // We add "[EXACT]" to the query name for better logging
                await ResultLogger.SaveSearchRecordAsync($"{query} [EXACT]", recordedTimes).ConfigureAwait(true);

                Console.WriteLine($"\n✅ Done! Exact search data saved to records.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exact Search Error: {ex.Message}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        static async Task RunSearchLoop(string conn, EmbeddingClient ec)
        {
            // Fix: Read quantity once
            Console.Write("Quantity of trials for this search: ");
            if (!int.TryParse(Console.ReadLine(), out int searchCnt)) searchCnt = 1;

            Console.Write("\nEnter search query (or 'back'): ");
            string query = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "back") return;

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                // Generate embedding once (saves API costs)
                var embeddingResult = await ec.GenerateEmbeddingsAsync([query]).ConfigureAwait(true);
                float[] queryVector = embeddingResult.Value[0].ToFloats().ToArray();

                List<float> recordedTimes = new();

                for (int i = 0; i < searchCnt; i++)
                {
                    float time = await KMeansManager.SearchANNAsync(conn, queryVector).ConfigureAwait(true);
                    recordedTimes.Add(time);
                    Console.WriteLine($"Trial {i + 1}: {time:F2} ms");
                }

                // Use our new helper class to save the data
                await ResultLogger.SaveSearchRecordAsync(query, recordedTimes).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search Error: {ex.Message}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

    }
}