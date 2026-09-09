using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class ViewDayWindow : Window
    {
        private readonly DateTime _date;
        private readonly int _currentUserId;
        private readonly string _currentUserRole;

        public ViewDayWindow(DateTime date, int currentUserId, string currentUserRole = "")
        {
            InitializeComponent();
            _date = date;
            _currentUserId = currentUserId;
            _currentUserRole = currentUserRole;
            Load();
        }

        private void Load()
        {
            DayTitle.Text = $"Выезды за {_date:dd.MM.yyyy}";

            try
            {
                var rows = Db.Query(@"
                    SELECT
                        a.appointment_id,
                        a.appointment_time,
                        a.address,
                        wt.type_name,
                        a.notes,
                        s.status_name
                    FROM appointments a
                    LEFT JOIN work_types wt ON wt.type_id = a.work_type_id
                    JOIN appointment_statuses s ON s.status_id = a.status_id
                    WHERE a.appointment_date = @d
                    ORDER BY a.appointment_time
                ", new() { ["d"] = _date.Date });

                var items = new List<ViewDayItem>();

                foreach (var r in rows)
                {
                    int appId = Convert.ToInt32(r["appointment_id"]);
                    var time = Db.GetTime(r["appointment_time"]);
                    string status = r["status_name"]?.ToString() ?? "";

                    items.Add(new ViewDayItem
                    {
                        AppointmentId = appId,
                        Time = time?.ToString(@"hh\:mm") ?? "",
                        Address = r["address"]?.ToString() ?? "—",
                        WorkType = r["type_name"]?.ToString() ?? "—",
                        Notes = r["notes"]?.ToString() ?? "",
                        Status = MapStatus(status),
                        StatusColor = MapStatusColor(status),
                        Workers = GetWorkersString(appId)
                    });
                }

                List.ItemsSource = items;
                CountText.Text = items.Count > 0
                    ? $"Найдено выездов: {items.Count}"
                    : "Выездов на эту дату нет";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message);
            }
        }

        private string GetWorkersString(int appointmentId)
        {
            var workers = Db.Query(@"
                SELECT u.full_name
                FROM appointment_workers aw
                JOIN users u ON u.user_id = aw.user_id
                WHERE aw.appointment_id = @id
                ORDER BY u.full_name
            ", new() { ["id"] = appointmentId });

            if (workers.Count == 0) return "";

            var names = workers.Select(w => w["full_name"]?.ToString() ?? "").ToList();
            return "👥 " + string.Join(", ", names);
        }

        private string MapStatus(string status) => status switch
        {
            "scheduled" => "Запланирован",
            "in_progress" => "В работе",
            "completed" => "Завершён",
            "cancelled" => "Отменён",
            _ => status
        };

        private string MapStatusColor(string status) => status switch
        {
            "scheduled" => "#7B1FA2",
            "in_progress" => "#FF7400",
            "completed" => "#2E7D32",
            "cancelled" => "#C62828",
            _ => "#CCCCCC"
        };

        private void Item_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe && fe.DataContext is ViewDayItem item)
                {
                    var win = new EditAppointmentWindow(item.AppointmentId, _currentUserRole, _currentUserId);
                    if (win.ShowDialog() == true)
                    {
                        Load();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка открытия: " + ex.Message);
            }
        }
    }
}