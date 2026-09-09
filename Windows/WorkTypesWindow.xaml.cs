using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class WorkTypesWindow : Window
    {
        private ObservableCollection<WorkTypeItem> _items = new();

        public WorkTypesWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            var rows = Db.Query(@"
                SELECT type_id, type_name, description, price_min, price_max, is_active
                FROM work_types
                ORDER BY type_id
            ");

            _items.Clear();
            foreach (var r in rows)
            {
                _items.Add(new WorkTypeItem
                {
                    TypeId = Convert.ToInt32(r["type_id"]),
                    TypeName = r["type_name"]?.ToString() ?? "",
                    Description = r["description"]?.ToString() ?? "",
                    PriceMin = r["price_min"] != DBNull.Value ? Convert.ToDecimal(r["price_min"]) : 0m,
                    PriceMax = r["price_max"] != DBNull.Value ? Convert.ToDecimal(r["price_max"]) : 0m,
                    IsActive = r["is_active"] != DBNull.Value && Convert.ToBoolean(r["is_active"])
                });
            }

            WorkTypesGrid.ItemsSource = _items;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _items)
                {
                    Db.Execute(@"
                        UPDATE work_types
                        SET type_name = @name,
                            description = @desc,
                            price_min = @min,
                            price_max = @max,
                            is_active = @active
                        WHERE type_id = @id
                    ", new()
                    {
                        ["id"] = item.TypeId,
                        ["name"] = item.TypeName,
                        ["desc"] = item.Description,
                        ["min"] = item.PriceMin,
                        ["max"] = item.PriceMax,
                        ["active"] = item.IsActive
                    });
                }

                MessageBox.Show("Изменения сохранены");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message);
            }
        }
    }
}