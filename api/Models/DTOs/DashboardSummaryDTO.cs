namespace Models.DTOs
{
    public class DashboardSummaryDTO
    {
        public int TotalProject { get; set; }
        public int OnProgress { get; set; }
        public int Completed { get; set; }
        public int Overdue { get; set; }
        public double ProgressPercentage { get; set; }
    }
}
