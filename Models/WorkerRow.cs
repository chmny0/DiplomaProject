namespace WpfPlannerApp.Models
{
    public class WorkerRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public int RoleId { get; set; }
        public string RoleName { get; set; } = "";
        public int StatusId { get; set; }
        public string StatusName { get; set; } = "";
        public string StatusDisplay => StatusName switch
        {
            "working" => "Работает",
            "vacation" => "В отпуске",
            "sick" => "На больничном",
            "fired" => "Уволен",
            _ => StatusName
        };
    }
}