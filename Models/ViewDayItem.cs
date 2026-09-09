namespace WpfPlannerApp.Models
{
    public class ViewDayItem
    {
        public int AppointmentId { get; set; }
        public string Time { get; set; } = "";
        public string Address { get; set; } = "";
        public string WorkType { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "#CCCCCC";
        public string Workers { get; set; } = "";
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
    }
}