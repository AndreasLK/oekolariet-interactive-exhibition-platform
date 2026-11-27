using HighscoreService.Application.Models;
using HighscoreService.Application.UseCases;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Tests.Application
{
        /// <summary>
        /// Unit tests for <see cref="HighscoreStatisticsCalculator"/> aggregation and abandonment metrics.
        /// </summary>
        public sealed class HighscoreStatisticsCalculatorTests
        {
                /// <summary>
                /// Verifies abandonment rate, age brackets, rank distribution, and per-game/per-screen rollups.
                /// </summary>
                [Fact]
                public void BuildReport_ComputesAbandonmentBracketsAndRanks()
                {
                        // Seed one completed play and one timed-out unnamed play to exercise abandonment math.
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset now = new(2026, 11, 26, 14, 0, 0, TimeSpan.Zero);

                        HighscoreEntity completed = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: "Player1",
                                age: 10,
                                score: 100,
                                senderMacAddress: "AA:BB:CC:DD:EE:01",
                                gameId: gameId,
                                playedAtUtc: now.AddMinutes(-30),
                                isTimedOut: false,
                                rankingAtTimeOfScoring: 1);

                        HighscoreEntity timedOut = HighscoreEntity.Create(
                                id: Guid.NewGuid(),
                                name: null,
                                age: 15,
                                score: 20,
                                senderMacAddress: "AA:BB:CC:DD:EE:02",
                                gameId: gameId,
                                playedAtUtc: now.AddMinutes(-15),
                                isTimedOut: true);

                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport(
                                entries: [completed, timedOut],
                                generatedAtUtc: now);

                        Assert.Equal(2, report.TotalPlays);
                        Assert.Equal(1, report.CompletedPlays);
                        Assert.Equal(1, report.TimedOutPlays);
                        Assert.Equal(1, report.UnnamedPlays);
                        Assert.Equal(50.0, report.AbandonmentRatePercent);
                        Assert.Equal(60.0, report.AverageScore);
                        Assert.Equal(1, report.RankDistribution.Top1Count);
                        Assert.Equal(2, report.AgeBrackets.Count);
                        Assert.Single(report.PerGame);
                        Assert.Equal(2, report.PerScreen.Count);
                }

                /// <summary>
                /// Verifies empty input returns zeroed aggregates without divide-by-zero errors.
                /// </summary>
                [Fact]
                public void BuildReport_HandlesEmptyEntriesSafely()
                {
                        // Build report from an empty set to confirm zeroed defaults.
                        DateTimeOffset now = new(2026, 11, 26, 14, 0, 0, TimeSpan.Zero);
                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport([], now);

                        Assert.Equal(0, report.TotalPlays);
                        Assert.Equal(0.0, report.AbandonmentRatePercent);
                        Assert.Equal(0.0, report.AverageScore);
                        Assert.Empty(report.AgeBrackets);
                        Assert.Empty(report.PerGame);
                        Assert.Empty(report.PerScreen);
                }

                /// <summary>
                /// Verifies even-count median averaging and clamped 90th-percentile calculation.
                /// </summary>
                [Fact]
                public void BuildReport_ComputesEvenMedianAndClampedPercentile90()
                {
                        // Arrange: Four scores [10, 20, 30, 100] -> Median = (20 + 30) / 2 = 25; 90th percentile = 100.
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
                        HighscoreEntity[] entries =
                        [
                                HighscoreEntity.Create(Guid.NewGuid(), "P1", 10, 10, "AA:BB:CC:DD:EE:01", gameId, now),
                                HighscoreEntity.Create(Guid.NewGuid(), "P2", 10, 20, "AA:BB:CC:DD:EE:01", gameId, now),
                                HighscoreEntity.Create(Guid.NewGuid(), "P3", 10, 30, "AA:BB:CC:DD:EE:01", gameId, now),
                                HighscoreEntity.Create(Guid.NewGuid(), "P4", 10, 100, "AA:BB:CC:DD:EE:01", gameId, now)
                        ];

                        // Act
                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport(entries, now);

                        // Assert
                        Assert.Equal(25.0, report.MedianScore);
                        Assert.Equal(10, report.MinScore);
                        Assert.Equal(100, report.MaxScore);
                        Assert.Equal(100, report.Percentile90Score);
                }

                /// <summary>
                /// Verifies all configured age brackets and null ages map to their expected labels.
                /// </summary>
                [Fact]
                public void BuildReport_AssignsAllAgeBracketsAndUnknownForNullAge()
                {
                        // Arrange: One play per bracket boundary plus null age.
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
                        int?[] ages = [6, 12, 17, 30, 50, 51, null];
                        HighscoreEntity[] entries =
                        [
                                .. ages.Select((age, index) => HighscoreEntity.Create(
                                        id: Guid.NewGuid(),
                                        name: $"P{index}",
                                        age: age,
                                        score: 100 + index,
                                        senderMacAddress: "AA:BB:CC:DD:EE:01",
                                        gameId: gameId,
                                        playedAtUtc: now))
                        ];

                        // Act
                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport(entries, now);

                        // Assert
                        Assert.Equal(
                                ["0-6", "7-12", "13-17", "18-30", "31-50", "51+", "Unknown"],
                                report.AgeBrackets.Select(b => b.BracketLabel).ToArray());
                }

                /// <summary>
                /// Verifies hourly trend buckets truncate timestamps to UTC hours in ascending chronological order.
                /// </summary>
                [Fact]
                public void BuildReport_GroupsHourlyTrendInAscendingUtcOrder()
                {
                        // Arrange: Two plays in 11:xx UTC (one with +02:00 offset) and one in 10:xx UTC.
                        Guid gameId = Guid.NewGuid();
                        DateTimeOffset hour10 = new(2026, 11, 27, 10, 45, 0, TimeSpan.Zero);
                        DateTimeOffset hour11Offset = new(2026, 11, 27, 13, 15, 0, TimeSpan.FromHours(2));
                        DateTimeOffset hour11Utc = new(2026, 11, 27, 11, 50, 0, TimeSpan.Zero);

                        HighscoreEntity[] entries =
                        [
                                HighscoreEntity.Create(Guid.NewGuid(), "P1", 10, 50, "AA:BB:CC:DD:EE:01", gameId, hour11Offset),
                                HighscoreEntity.Create(Guid.NewGuid(), "P2", 10, 70, "AA:BB:CC:DD:EE:01", gameId, hour10),
                                HighscoreEntity.Create(Guid.NewGuid(), "P3", 10, 90, "AA:BB:CC:DD:EE:01", gameId, hour11Utc)
                        ];

                        // Act
                        HighscoreStatisticsReport report = HighscoreStatisticsCalculator.BuildReport(entries, hour11Utc);

                        // Assert
                        Assert.Equal(2, report.HourlyTrend.Count);
                        Assert.Equal(new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero), report.HourlyTrend[0].BucketStartUtc);
                        Assert.Equal(1, report.HourlyTrend[0].TotalPlays);
                        Assert.Equal(new DateTimeOffset(2026, 11, 27, 11, 0, 0, TimeSpan.Zero), report.HourlyTrend[1].BucketStartUtc);
                        Assert.Equal(2, report.HourlyTrend[1].TotalPlays);
                }
        }
}

