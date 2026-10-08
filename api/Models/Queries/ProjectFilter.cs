namespace Models.Queries
{
    /// <summary>A validated project query, ready for the repository.</summary>
    /// <param name="Today">Start of the current day (UTC); an unfinished project ending before it is overdue.</param>
    public sealed record ProjectFilter(
        string? Search,
        string? Status,
        string SortBy,
        bool Descending,
        int Page,
        int PageSize,
        DateTime Today);

    /// <summary>Counts computed in the database for the dashboard.</summary>
    public sealed record ProjectSummary(
        int Total,
        int Completed,
        int Overdue,
        double AverageProgress)
    {
        public int OnProgress => Total - Completed - Overdue;
    }
}
