namespace WpfPlannerApp.Models
{
    public class DocumentItem
    {
        public int DocumentId { get; set; }
        public string DisplayText { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string? FilePath { get; set; }
    }
}