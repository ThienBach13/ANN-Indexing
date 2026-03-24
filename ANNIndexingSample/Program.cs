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
                Console.WriteLine("1. Seed Movies (CSV -> SQL)");
                Console.WriteLine("2. Seed Centroids (User-defined Clusters)");
                Console.WriteLine("3. Assign Clusters (Rebuild ANN Index)");
                Console.WriteLine("4. Search Movies (ANN Mode)");
                Console.WriteLine("5. Exit");
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
                            // Pass the user-defined 'k' to your generator
                            await KMeansManager.GenerateCentroidsAsync(connString, k).ConfigureAwait(true);
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please enter a positive number.");
                        }
                        break;

                    case "3":
                        await KMeansManager.AssignMoviesToClustersAsync(connString);
                        break;

                    case "4":
                        await RunSearchLoop(connString, client).ConfigureAwait(true);
                        break;

                    case "5":
                        running = false;
                        break;

                    default:
                        Console.WriteLine("Invalid option. Try again.");
                        break;
                }
            }
        }

        // Moved outside Main loop for cleaner code
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