namespace WpfPlannerApp.Models
{
    public class RequestItem
    {
        public int Id { get; set; }
        public string Client { get; set; } = "";
        public string Phone { get; set; } = "";
        public string WorkType { get; set; } = "";
        public string Status { get; set; } = "";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string ClientType { get; set; } = "";
    }
}