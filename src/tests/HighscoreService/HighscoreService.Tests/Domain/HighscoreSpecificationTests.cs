using HighscoreService.Domain.Specifications;

namespace HighscoreService.Tests.Domain
{
        /// <summary>
        /// Unit tests for <see cref="HighscoreSpecification"/> paging clamping and time-window resolution.
        /// </summary>
        public sealed class HighscoreSpecificationTests
        {
                /// <summary>
                /// Verifies out-of-range page and page-size inputs clamp cleanly to safe defaults and bounds.
                /// </summary>
                [Fact]
                public void NormalizePaging_ClampsPageAndPageSizeToSafeBounds()
                {
                        // Exercise lower-bound default fallback and upper-bound MAX_PAGE_SIZE clamping.
                        HighscoreSpecification low = new() { Page = 0, PageSize = 0 };
                        HighscoreSpecification high = new() { Page = 3, PageSize = HighscoreSpecification.MAX_PAGE_SIZE + 100 };

                        HighscoreSpecification normalizedLow = low.NormalizePaging();
                        HighscoreSpecification normalizedHigh = high.NormalizePaging();

                        Assert.Equal(HighscoreSpecification.DEFAULT_PAGE, normalizedLow.Page);
                        Assert.Equal(HighscoreSpecification.DEFAULT_PAGE_SIZE, normalizedLow.PageSize);
                        Assert.Equal(3, normalizedHigh.Page);
                        Assert.Equal(HighscoreSpecification.MAX_PAGE_SIZE, normalizedHigh.PageSize);
                }

                /// <summary>
                /// Verifies explicit <see cref="HighscoreSpecification.FromUtc"/> overrides relative <see cref="HighscoreSpecification.LastHours"/>.
                /// </summary>
                [Fact]
                public void ResolveEffectiveFromUtc_PrefersExplicitFromUtcAndIgnoresNonPositiveLastHours()
                {
                        // Confirm explicit FromUtc precedence and non-positive LastHours filtering.
                        DateTimeOffset now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
                        DateTimeOffset explicitFrom = now.AddDays(-2);

                        HighscoreSpecification withBoth = new() { FromUtc = explicitFrom, LastHours = 5 };
                        HighscoreSpecification withZeroHours = new() { LastHours = 0 };
                        HighscoreSpecification withLastHours = new() { LastHours = 12 };

                        Assert.Equal(explicitFrom, withBoth.ResolveEffectiveFromUtc(now));
                        Assert.Null(withZeroHours.ResolveEffectiveFromUtc(now));
                        Assert.Equal(now.AddHours(-12), withLastHours.ResolveEffectiveFromUtc(now));
                }
        }
}
