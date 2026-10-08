namespace Models
{
    /// <summary>
    /// Status names as they appear in the API. Only <see cref="Completed"/> and
    /// <see cref="OnProgress"/> are ever stored; <see cref="Overdue"/> is derived from the
    /// end date, so it can never go stale.
    /// </summary>
    public static class ProjectStatus
    {
        public const string OnProgress = "On Progress";
        public const string Completed = "Completed";
        public const string Overdue = "Overdue";

        public const string Pattern = "^(On Progress|Completed|Overdue)$";
    }
}
