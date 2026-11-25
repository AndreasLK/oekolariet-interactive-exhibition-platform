namespace ContentService.Infrastructure.Repositories;

/// <summary>
/// Normalizes repository pagination bounds so page clamping and skip calculation are shared across SQL Server repositories.
/// </summary>
internal static class PaginationNormalizer
{
        /// <summary>
        /// Clamps pagination bounds to at least 1 and calculates a non-overflowing <c>Skip</c> offset.
        /// </summary>
        /// <param name="page">The requested 1-based page number.</param>
        /// <param name="pageSize">The requested number of items per page.</param>
        /// <returns>The clamped page number, clamped page size, and safe skip offset.</returns>
        public static (int SafePage, int SafePageSize, int SkipCount) Normalize(int page, int pageSize)
        {
                // Clamp bounds to >= 1 so direct repository callers never emit negative SQL OFFSET/FETCH arguments.
                int safePage = Math.Max(val1: 1, val2: page);
                int safePageSize = Math.Max(val1: 1, val2: pageSize);

                // Multiply as long first so huge page numbers cap at int.MaxValue instead of overflowing into a negative skip count.
                int skipCount = (int)Math.Min(val1: int.MaxValue, val2: (long)(safePage - 1) * safePageSize);
                return (safePage, safePageSize, skipCount);
        }
}

