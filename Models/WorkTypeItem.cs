namespace WpfPlannerApp.Models
{
    public class WorkTypeItem
    {
        public int TypeId { get; set; }
        public string TypeName { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal PriceMin { get; set; }
        public decimal PriceMax { get; set; }
        public bool IsActive { get; set; }
    }
}