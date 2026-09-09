namespace WpfPlannerApp.Models
{
    public class JournalItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Date { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "#666666";
        public string ItemType { get; set; } = "";
    }
}