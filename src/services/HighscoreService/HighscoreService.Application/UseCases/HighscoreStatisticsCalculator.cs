using HighscoreService.Application.Models;
using HighscoreService.Domain.Entities;

namespace HighscoreService.Application.UseCases
{
        /// <summary>
        /// Pure calculation routines for building exhibition statistics reports from highscore entries.
        /// </summary>
        public static class HighscoreStatisticsCalculator
        {
                private const double PERCENT_MULTIPLIER = 100.0;
                private const int TOP_1_RANK = 1;
                private const int TOP_3_RANK = 3;
                private const int TOP_10_RANK = 10;

                private static readonly (string Label, int? MinAge, int? MaxAge)[] AGE_BRACKETS =
                [
                        ("0-6", 0, 6),
                        ("7-12", 7, 12),
                        ("13-17", 13, 17),
                        ("18-30", 18, 30),
                        ("31-50", 31, 50),
                        ("51+", 51, 200),
                        ("Unknown", null, null)
                ];

                /// <summary>
                /// Builds a complete <see cref="HighscoreStatisticsReport"/> from a list of entries.
                /// </summary>
                /// <param name="entries">The filtered non-deleted entries.</param>
                /// <param name="generatedAtUtc">The UTC generation timestamp.</param>
                /// <returns>The aggregated statistics report.</returns>
                public static HighscoreStatisticsReport BuildReport(
                        IReadOnlyList<HighscoreEntity> entries,
                        DateTimeOffset generatedAtUtc)
                {
                        if (entries.Count == 0)
                        {
                                return CreateEmptyReport(generatedAtUtc);
                        }

                        int totalPlays = entries.Count;
                        int timedOutPlays = entries.Count(e => e.IsTimedOut);
                        int unnamedPlays = entries.Count(e => e.Name is null);
                        // A play is considered abandoned if it timed out OR the player left without entering a name.
                        int abandonedPlays = entries.Count(e => IsAbandoned(e));
                        int completedPlays = totalPlays - abandonedPlays;

                        int[] sortedScores = [.. entries.Select(e => e.Score).OrderBy(s => s)];

                        return new HighscoreStatisticsReport(
                                GeneratedAtUtc: generatedAtUtc,
                                TotalPlays: totalPlays,
                                CompletedPlays: completedPlays,
                                TimedOutPlays: timedOutPlays,
                                UnnamedPlays: unnamedPlays,
                                AbandonedPlays: abandonedPlays,
                                CompletionRatePercent: CalculateRate(completedPlays, totalPlays),
                                AbandonmentRatePercent: CalculateRate(abandonedPlays, totalPlays),
                                AverageScore: Math.Round(entries.Average(e => e.Score), 2),
                                MedianScore: CalculateMedian(sortedScores),
                                MinScore: sortedScores[0],
                                MaxScore: sortedScores[^1],
                                Percentile90Score: CalculatePercentile(sortedScores, 0.90),
                                RankDistribution: BuildRankDistribution(entries),
                                AgeBrackets: BuildAgeBrackets(entries),
                                HourlyTrend: BuildHourlyTrend(entries),
                                PerGame: BuildGameStatistics(entries),
                                PerScreen: BuildScreenStatistics(entries));
                }

                /// <summary>
                /// Computes rank distribution statistics for a set of entries.
                /// </summary>
                /// <param name="entries">The entries to inspect.</param>
                /// <returns>The rank distribution summary.</returns>
                public static RankDistributionStatistics BuildRankDistribution(IReadOnlyList<HighscoreEntity> entries)
                {
                        List<int> ranks =
                        [
                                .. entries
                                        .Where(e => e.RankingAtTimeOfScoring.HasValue)
                                        .Select(e => e.RankingAtTimeOfScoring!.Value)
                        ];

                        if (ranks.Count == 0)
                        {
                                return new RankDistributionStatistics(0, 0, 0, 0, 0.0, 0.0, null);
                        }

                        int top1 = ranks.Count(r => r <= TOP_1_RANK);
                        int top3 = ranks.Count(r => r <= TOP_3_RANK);
                        int top10 = ranks.Count(r => r <= TOP_10_RANK);

                        return new RankDistributionStatistics(
                                RankedPlayCount: ranks.Count,
                                Top1Count: top1,
                                Top3Count: top3,
                                Top10Count: top10,
                                Top1RatePercent: CalculateRate(top1, ranks.Count),
                                Top10RatePercent: CalculateRate(top10, ranks.Count),
                                AverageRank: Math.Round(ranks.Average(), 2));
                }

                private static HighscoreStatisticsReport CreateEmptyReport(DateTimeOffset generatedAtUtc)
                {
                        return new HighscoreStatisticsReport(
                                GeneratedAtUtc: generatedAtUtc,
                                TotalPlays: 0,
                                CompletedPlays: 0,
                                TimedOutPlays: 0,
                                UnnamedPlays: 0,
                                AbandonedPlays: 0,
                                CompletionRatePercent: 0.0,
                                AbandonmentRatePercent: 0.0,
                                AverageScore: 0.0,
                                MedianScore: 0.0,
                                MinScore: 0,
                                MaxScore: 0,
                                Percentile90Score: 0,
                                RankDistribution: new RankDistributionStatistics(0, 0, 0, 0, 0.0, 0.0, null),
                                AgeBrackets: [],
                                HourlyTrend: [],
                                PerGame: [],
                                PerScreen: []);
                }

                private static List<AgeBracketStatistics> BuildAgeBrackets(IReadOnlyList<HighscoreEntity> entries)
                {
                        List<AgeBracketStatistics> results = [];

                        foreach ((string label, int? minAge, int? maxAge) in AGE_BRACKETS)
                        {
                                List<HighscoreEntity> bucket =
                                [
                                        .. entries.Where(e => MatchesAgeBracket(e.Age, minAge, maxAge))
                                ];

                                if (bucket.Count == 0)
                                {
                                        continue;
                                }

                                int abandoned = bucket.Count(IsAbandoned);
                                int completed = bucket.Count - abandoned;

                                results.Add(new AgeBracketStatistics(
                                        BracketLabel: label,
                                        MinAge: minAge,
                                        MaxAge: maxAge,
                                        TotalPlays: bucket.Count,
                                        CompletedPlays: completed,
                                        AbandonedPlays: abandoned,
                                        AbandonmentRatePercent: CalculateRate(abandoned, bucket.Count),
                                        AverageScore: Math.Round(bucket.Average(e => e.Score), 2),
                                        MaxScore: bucket.Max(e => e.Score)));
                        }

                        return results;
                }

                private static List<TimeBucketStatistics> BuildHourlyTrend(IReadOnlyList<HighscoreEntity> entries)
                {
                        return
                        [
                                .. entries
                                        .GroupBy(e => TruncateToHourUtc(e.PlayedAtUtc))
                                        .OrderBy(g => g.Key)
                                        .Select(g =>
                                        {
                                                List<HighscoreEntity> items = [.. g];
                                                int timedOut = items.Count(e => e.IsTimedOut);
                                                int unnamed = items.Count(e => e.Name is null);
                                                int abandoned = items.Count(IsAbandoned);
                                                return new TimeBucketStatistics(
                                                        BucketStartUtc: g.Key,
                                                        TotalPlays: items.Count,
                                                        CompletedPlays: items.Count - abandoned,
                                                        TimedOutPlays: timedOut,
                                                        UnnamedPlays: unnamed,
                                                        AbandonmentRatePercent: CalculateRate(abandoned, items.Count),
                                                        AverageScore: Math.Round(items.Average(e => e.Score), 2));
                                        })
                        ];
                }

                private static List<GameStatistics> BuildGameStatistics(IReadOnlyList<HighscoreEntity> entries)
                {
                        return
                        [
                                .. entries
                                        .GroupBy(e => e.GameId)
                                        .Select(g =>
                                        {
                                                List<HighscoreEntity> items = [.. g];
                                                int[] sorted = [.. items.Select(e => e.Score).OrderBy(s => s)];
                                                int timedOut = items.Count(e => e.IsTimedOut);
                                                int unnamed = items.Count(e => e.Name is null);
                                                int abandoned = items.Count(IsAbandoned);
                                                return new GameStatistics(
                                                        GameId: g.Key,
                                                        TotalPlays: items.Count,
                                                        CompletedPlays: items.Count - abandoned,
                                                        TimedOutPlays: timedOut,
                                                        UnnamedPlays: unnamed,
                                                        AbandonmentRatePercent: CalculateRate(abandoned, items.Count),
                                                        AverageScore: Math.Round(items.Average(e => e.Score), 2),
                                                        MedianScore: CalculateMedian(sorted),
                                                        MaxScore: sorted[^1],
                                                        RankDistribution: BuildRankDistribution(items));
                                        })
                                        .OrderByDescending(g => g.TotalPlays)
                        ];
                }

                private static List<ScreenStatistics> BuildScreenStatistics(IReadOnlyList<HighscoreEntity> entries)
                {
                        return
                        [
                                .. entries
                                        .GroupBy(e => e.SenderMacAddress)
                                        .Select(g =>
                                        {
                                                List<HighscoreEntity> items = [.. g];
                                                int timedOut = items.Count(e => e.IsTimedOut);
                                                int abandoned = items.Count(IsAbandoned);
                                                return new ScreenStatistics(
                                                        SenderMacAddress: g.Key,
                                                        TotalPlays: items.Count,
                                                        CompletedPlays: items.Count - abandoned,
                                                        TimedOutPlays: timedOut,
                                                        AbandonmentRatePercent: CalculateRate(abandoned, items.Count),
                                                        AverageScore: Math.Round(items.Average(e => e.Score), 2),
                                                        LastPlayedAtUtc: items.Max(e => e.PlayedAtUtc));
                                        })
                                        .OrderByDescending(s => s.TotalPlays)
                        ];
                }

                private static bool IsAbandoned(HighscoreEntity entity)
                {
                        return entity.IsTimedOut || entity.Name is null;
                }

                private static bool MatchesAgeBracket(int? age, int? minAge, int? maxAge)
                {
                        if (!minAge.HasValue || !maxAge.HasValue)
                        {
                                return !age.HasValue;
                        }

                        return age.HasValue && age.Value >= minAge.Value && age.Value <= maxAge.Value;
                }

                private static DateTimeOffset TruncateToHourUtc(DateTimeOffset timestamp)
                {
                        DateTimeOffset utc = timestamp.ToUniversalTime();
                        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
                }

                private static double CalculateRate(int part, int total)
                {
                        if (total <= 0)
                        {
                                return 0.0;
                        }

                        return Math.Round((double)part / total * PERCENT_MULTIPLIER, 2);
                }

                private static double CalculateMedian(int[] sortedScores)
                {
                        int count = sortedScores.Length;
                        if (count == 0)
                        {
                                return 0.0;
                        }

                        int mid = count / 2;
                        if (count % 2 == 1)
                        {
                                return sortedScores[mid];
                        }

                        return Math.Round((sortedScores[mid - 1] + sortedScores[mid]) / 2.0, 2);
                }

                private static int CalculatePercentile(int[] sortedScores, double percentile)
                {
                        int index = (int)Math.Ceiling(percentile * sortedScores.Length) - 1;
                        int clampedIndex = Math.Clamp(index, 0, sortedScores.Length - 1);
                        return sortedScores[clampedIndex];
                }
        }
}

