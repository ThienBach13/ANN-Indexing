using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ANNIndexingSample.Helper
{
    internal static class ResultLogger
    {
        private const string FolderPath = "Output";
        private const string FileName = "records.txt";
        private static readonly string FullPath = Path.Combine(FolderPath, FileName);

        public static async Task SaveSearchRecordAsync(string query, List<float> times)
        {
            if (times == null || times.Count == 0) return;

            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            // Create a concise list: "1: 12.45 ms"
            var entry = times.Select((t, i) => $"{i + 1}: {t:F2} ms").ToList();

            // Optional: Add a small separator or the query name so you know what the times belong to
            entry.Insert(0, $"--- Query: {query} ---");
            entry.Add(""); // Add an empty line for spacing between different search sessions

            await File.AppendAllLinesAsync(FullPath, entry);

            Console.WriteLine($"\n[Log] {times.Count} trials saved to {FullPath}");
        }
    }
}
