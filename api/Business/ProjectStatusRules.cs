using Models;

namespace Business
{
    /// <summary>
    /// Single source of truth for what a project's status means. "Overdue" is never
    /// stored: it follows from the stored status and the end date.
    /// </summary>
    public static class ProjectStatusRules
    {
        /// <summary>The status to show: Completed stays Completed; otherwise Overdue once the end date has passed.</summary>
        public static string Effective(string storedStatus, DateTime endDate, DateTime today)
        {
            if (storedStatus == ProjectStatus.Completed) return ProjectStatus.Completed;
            return endDate < today ? ProjectStatus.Overdue : ProjectStatus.OnProgress;
        }

        /// <summary>What to persist for a requested status ("Overdue" is derived, so it is stored as "On Progress").</summary>
        public static string ToStored(string requestedStatus) =>
            requestedStatus == ProjectStatus.Completed ? ProjectStatus.Completed : ProjectStatus.OnProgress;
    }
}
