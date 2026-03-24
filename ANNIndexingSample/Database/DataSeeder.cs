using System.Globalization;
using ANNIndexingSample.Entities;
using Microsoft.Data.SqlClient;
using OpenAI.Embeddings;

namespace ANNIndexingSample.Database
{
    internal class DataSeeder
    {
        private const string CsvPath = "Properties/data.csv";

        public static async Task SeedMoviesFromCsvAsync(string connectionString, EmbeddingClient client)
        {
            Console.WriteLine($"--- Seeding Data from {CsvPath} ---");

            if (!File.Exists(CsvPath))
            {
                Console.WriteLine($"Error: CSV file not found at {Path.GetFullPath(CsvPath)}");
                return;
            }

            // Read all lines and skip the header
            var lines = (await File.ReadAllLinesAsync(CsvPath)).Skip(1).ToList();

            // Process in batches of 10 for efficiency
            int batchSize = 10;
            for (int i = 0; i < lines.Count; i += batchSize)
            {
                var batchLines = lines.Skip(i).Take(batchSize).ToList();
                await ProcessBatch(connectionString, batchLines, client);
            }

            Console.WriteLine("--- Seeding Complete ---");
        }

        private static async Task ProcessBatch(string connString, List<string> lines, EmbeddingClient client)
        {
            List<string> inputsToEmbed = new();
            List<MovieData> movieMetadata = new();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                var movie = new MovieData
                {
                    Id = int.Parse(parts[0]), // We need the ID from CSV
                    Title = parts[1],
                    Genre = parts[2],
                    Year = int.Parse(parts[3]),
                    Description = parts[4]
                };

                movieMetadata.Add(movie);
                inputsToEmbed.Add($"{movie.Title}: {movie.Description}");
            }

            try
            {
                OpenAIEmbeddingCollection embeddings = await client.GenerateEmbeddingsAsync(inputsToEmbed).ConfigureAwait(true);

                using SqlConnection conn = new(connString);
                await conn.OpenAsync().ConfigureAwait(true);

                for (int j = 0; j < movieMetadata.Count; j++)
                {
                    float[] vector = embeddings[j].ToFloats().ToArray();
                    // Format as a JSON array string for the VECTOR type
                    string vectorJson = $"[{string.Join(",", vector.Select(f => f.ToString(CultureInfo.InvariantCulture)))}]";

                    // COLUMN NAMES MATCHED TO YOUR SQL SCHEMA
                    string sql = @"INSERT INTO Movies (id, title, genre, release_year, description, embedding) 
                           VALUES (@Id, @Title, @Genre, @Year, @Desc, @Vector)";

                    using SqlCommand cmd = new(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", movieMetadata[j].Id);
                    cmd.Parameters.AddWithValue("@Title", movieMetadata[j].Title);
                    cmd.Parameters.AddWithValue("@Genre", movieMetadata[j].Genre);
                    cmd.Parameters.AddWithValue("@Year", movieMetadata[j].Year);
                    cmd.Parameters.AddWithValue("@Desc", movieMetadata[j].Description);
                    cmd.Parameters.AddWithValue("@Vector", vectorJson);

                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(true);
                    Console.WriteLine($"[Seeded ID {movieMetadata[j].Id}]: {movieMetadata[j].Title}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Batch Error]: {ex.Message}");
            }
        }
    }


}