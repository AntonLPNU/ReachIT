using System;
using System.IO;
using Xunit;
using ReachIT.Infrastructure.Logging;
using ReachIT.Bootstrap;

namespace ReachIT.Tests
{
    public class StartupAndLoggingTests
    {
        [Fact]
        public void LocalLogger_ShouldWriteToLogFile()
        {
            // Arrange
            var logger = new LocalLogger();

            // Act
            logger.LogInformation("Test smoke log");

            // Assert
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appData, "ReachIT", "logs");
            var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            var expectedFile = Path.Combine(logDir, $"ReachIT_{dateStr}.log");

            Assert.True(File.Exists(expectedFile));
            var content = File.ReadAllText(expectedFile);
            Assert.Contains("Test smoke log", content);
        }

        [Fact]
        public void AppHost_ShouldRegisterAndResolveLogger()
        {
            // Arrange
            var appHost = new AppHost();

            // Act
            appHost.Initialize();
            var logger = appHost.GetRequiredService<ILocalLogger>();

            // Assert
            Assert.NotNull(logger);
            Assert.IsType<LocalLogger>(logger);
        }
    }
}
