using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using ANNIndexingSample.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject.Services
{
    [TestClass]
    [DoNotParallelize]
    public class KMeansManagerTests
    {
        private string _connectionString = string.Empty;
        private static readonly SemaphoreSlim _dbLock = new SemaphoreSlim(1, 1);

        [TestInitialize]
        public void Setup()
        {
            // Setup
            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("Properties\\launchSettings.json", optional: false, reloadOnChange: true)
                .Build();

            string server = config["profiles:ANNIndexingSample:environmentVariables:DB_SERVER"];
            string db = config["profiles:ANNIndexingSample:environmentVariables:DB_NAME"];
            string user = config["profiles:ANNIndexingSample:environmentVariables:DB_USER"];
            string pass = config["profiles:ANNIndexingSample:environmentVariables:DB_PASSWORD"];

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(db))
            {
                throw new AssertFailedException("Could not find Database Environment Variables in launchSettings.json");
            }

            _connectionString = $"Server={server};Initial Catalog={db};Persist Security Info=False;User ID={user};Password={pass};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        }

        [TestMethod]
        public async Task ClusteringScenario_ShouldGenerateCentroidsAndAssignMovies()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                int k = 10;

                // Act
                await KMeansManager.GenerateCentroidsAsync(_connectionString, k);

                // Assert
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM MovieCentroids", conn))
                    {
                        int centroidCount = (int)await cmd.ExecuteScalarAsync();
                        Assert.AreEqual(k, centroidCount);
                    }
                }

                // Act
                await KMeansManager.AssignMoviesToClustersAsync(_connectionString);

                // Assert
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Movies WHERE cluster_id IS NOT NULL", conn))
                    {
                        int assignedMoviesCount = (int)await cmd.ExecuteScalarAsync();
                        Assert.IsTrue(assignedMoviesCount > 0);
                    }
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        [TestMethod]
        public async Task SearchANNAsync_ShouldExecuteWithoutDatabaseErrors()
        {
            // Arrange
            float[] queryVector = new float[1536];
            for (int i = 0; i < queryVector.Length; i++) { queryVector[i] = 0.05f; }

            // Act
            float elapsedMs = await KMeansManager.SearchANNAsync(_connectionString, queryVector);

            // Assert
            Assert.IsTrue(elapsedMs >= 0);
        }

        [TestMethod]
        public async Task SearchExactAsync_ShouldExecuteWithoutDatabaseErrors()
        {
            // Arrange
            float[] queryVector = new float[1536];
            for (int i = 0; i < queryVector.Length; i++) { queryVector[i] = 0.05f; }

            // Act
            float elapsedMs = await KMeansManager.SearchExactAsync(_connectionString, queryVector);

            // Assert
            Assert.IsTrue(elapsedMs >= 0);
        }

        [TestMethod]
        public async Task GenerateCentroidsAsync_ShouldHandleSingleCentroid()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                int k = 1;

                // Act
                await KMeansManager.GenerateCentroidsAsync(_connectionString, k);

                // Assert
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM MovieCentroids", conn))
                    {
                        int centroidCount = (int)await cmd.ExecuteScalarAsync();
                        Assert.AreEqual(k, centroidCount);
                    }
                }
            }
            finally
            {
                _dbLock.Release();
            }
        }

        [TestMethod]
        public async Task SearchANNAsync_ShouldHandleAllZeroVector()
        {
            // Arrange
            float[] zeroVector = new float[1536];

            // Act
            float elapsedMs = await KMeansManager.SearchANNAsync(_connectionString, zeroVector);

            // Assert
            Assert.IsTrue(elapsedMs >= 0);
        }

        [TestMethod]
        public async Task SearchExactAsync_ShouldHandleNegativeVectorValues()
        {
            // Arrange
            float[] negativeVector = new float[1536];
            for (int i = 0; i < negativeVector.Length; i++)
            {
                negativeVector[i] = -0.5f;
            }

            // Act
            float elapsedMs = await KMeansManager.SearchExactAsync(_connectionString, negativeVector);

            // Assert
            Assert.IsTrue(elapsedMs >= 0);
        }

        [TestMethod]
        public async Task GenerateCentroidsAsync_ShouldHandleSmallBoundaryK()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                int k = 2;

                // Act
                await KMeansManager.GenerateCentroidsAsync(_connectionString, k);

                // Assert
                using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM MovieCentroids", conn);
                int centroidCount = (int)await cmd.ExecuteScalarAsync();

                Assert.AreEqual(k, centroidCount);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        [TestMethod]
        public async Task AssignMoviesToClustersAsync_ShouldNotCrash_IfNoCentroidsExist()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                await KMeansManager.GenerateCentroidsAsync(_connectionString, 0);

                // Act
                await KMeansManager.AssignMoviesToClustersAsync(_connectionString);

                // Assert
                using SqlConnection conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Movies WHERE cluster_id IS NOT NULL", conn);
                int assignedCount = (int)await cmd.ExecuteScalarAsync();

                Assert.AreEqual(0, assignedCount);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        [TestMethod]
        public async Task SearchANNAsync_ShouldHandleExtremelyLargeFloatValues()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                float[] massiveVector = new float[1536];
                for (int i = 0; i < massiveVector.Length; i++) { massiveVector[i] = 999999.99f; }

                // Act
                float elapsedMs = await KMeansManager.SearchANNAsync(_connectionString, massiveVector);

                // Assert
                Assert.IsTrue(elapsedMs >= 0);
            }
            finally
            {
                _dbLock.Release();
            }
        }

        [TestMethod]
        public async Task SearchExactAsync_ShouldHandleMicroscopicFloatValues()
        {
            await _dbLock.WaitAsync();
            try
            {
                // Arrange
                float[] tinyVector = new float[1536];
                for (int i = 0; i < tinyVector.Length; i++) { tinyVector[i] = 0.00000001f; }

                // Act
                float elapsedMs = await KMeansManager.SearchExactAsync(_connectionString, tinyVector);

                // Assert
                Assert.IsTrue(elapsedMs >= 0);
            }
            finally
            {
                _dbLock.Release();
            }
        }
    }
}