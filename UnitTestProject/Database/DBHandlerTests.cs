using ANNIndexingSample;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestProject
{
    [TestClass]
    [DoNotParallelize]
    public class DBHandlerTests
    {
        private static readonly object _envLock = new object();
        private static readonly SemaphoreSlim _consoleLock = new SemaphoreSlim(1, 1);

        [TestMethod]
        public void GetConnectionString_ShouldReturnCorrectFormat()
        {
            lock (_envLock)
            {
                // Setup
                var origServer = Environment.GetEnvironmentVariable("DB_SERVER");
                var origDb = Environment.GetEnvironmentVariable("DB_NAME");
                var origUser = Environment.GetEnvironmentVariable("DB_USER");
                var origPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

                try
                {
                    // Arrange
                    Environment.SetEnvironmentVariable("DB_SERVER", "test-server");
                    Environment.SetEnvironmentVariable("DB_NAME", "test-db");
                    Environment.SetEnvironmentVariable("DB_USER", "test-user");
                    Environment.SetEnvironmentVariable("DB_PASSWORD", "test-password");

                    // Act
                    string connString = DBHandler.GetConnectionString();

                    // Assert
                    connString.Should().Contain("Data Source=test-server");
                    connString.Should().Contain("Initial Catalog=test-db");
                    connString.Should().Contain("User ID=test-user");
                    connString.Should().Contain("Password=test-password");
                    connString.Should().Contain("Encrypt=True");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("DB_SERVER", origServer);
                    Environment.SetEnvironmentVariable("DB_NAME", origDb);
                    Environment.SetEnvironmentVariable("DB_USER", origUser);
                    Environment.SetEnvironmentVariable("DB_PASSWORD", origPass);
                }
            }
        }

        [TestMethod]
        public void GetConnectionString_ShouldHandlePartialEnvironmentVariables()
        {
            lock (_envLock)
            {
                // Setup
                var origServer = Environment.GetEnvironmentVariable("DB_SERVER");
                var origDb = Environment.GetEnvironmentVariable("DB_NAME");
                var origUser = Environment.GetEnvironmentVariable("DB_USER");
                var origPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

                try
                {
                    // Arrange
                    Environment.SetEnvironmentVariable("DB_SERVER", "only-server-exists");
                    Environment.SetEnvironmentVariable("DB_NAME", null);
                    Environment.SetEnvironmentVariable("DB_USER", null);
                    Environment.SetEnvironmentVariable("DB_PASSWORD", null);

                    // Act
                    string connString = DBHandler.GetConnectionString();

                    // Assert
                    connString.Should().Contain("Data Source=only-server-exists");
                    connString.Should().Contain("Initial Catalog=;");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("DB_SERVER", origServer);
                    Environment.SetEnvironmentVariable("DB_NAME", origDb);
                    Environment.SetEnvironmentVariable("DB_USER", origUser);
                    Environment.SetEnvironmentVariable("DB_PASSWORD", origPass);
                }
            }
        }

        [TestMethod]
        public async Task CheckDatabaseStatus_ShouldLogFailure_WhenConnectionStringIsInvalid()
        {
            await _consoleLock.WaitAsync();
            try
            {
                // Arrange
                string invalidConn = "Server=invalid;Database=none;User Id=none;Password=none;";
                var originalConsoleOut = Console.Out;
                using var sw = new StringWriter();

                try
                {
                    Console.SetOut(sw);

                    // Act
                    await DBHandler.CheckDatabaseStatus(invalidConn);
                    string result = sw.ToString();

                    // Assert
                    result.Should().Contain("Connection: OFFLINE");
                }
                finally
                {
                    Console.SetOut(originalConsoleOut);
                }
            }
            finally
            {
                _consoleLock.Release();
            }
        }

        [TestMethod]
        public async Task CheckDatabaseStatus_ShouldTimeoutGracefully_OnUnreachableServer()
        {
            await _consoleLock.WaitAsync();
            try
            {
                // Arrange
                string unreachableConn = "Server=10.255.255.255;Database=test;User Id=sa;Password=pass;Connect Timeout=1;";
                var originalConsoleOut = Console.Out;
                using var sw = new StringWriter();

                try
                {
                    Console.SetOut(sw);

                    // Act
                    await DBHandler.CheckDatabaseStatus(unreachableConn);
                    string result = sw.ToString();

                    // Assert
                    result.Should().Contain("Connection: OFFLINE");
                }
                finally
                {
                    Console.SetOut(originalConsoleOut);
                }
            }
            finally
            {
                _consoleLock.Release();
            }
        }
    }
}