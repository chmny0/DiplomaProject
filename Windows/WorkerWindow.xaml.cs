using System;
using System.Collections.ObjectModel;
using System.Windows;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class WorkerWindow : Window
    {
        private readonly int _userId;

        public WorkerWindow(int userId, string name)
        {
            InitializeComponent();
            _userId = userId;
            WorkerNameText.Text = $"Работник: {name}";
            Load();
        }

        private void Load()
        {
            var list = new ObservableCollection<WorkerAppointment>();

            var rows = Db.Query(@"
                SELECT 
                    a.appointment_date,
                    a.appointment_time,
                    a.address,
                    wt.type_name,
                    aps.status_name          
                FROM appointments a
                JOIN appointment_workers aw ON aw.appointment_id = a.appointment_id
                LEFT JOIN work_types wt ON wt.type_id = a.work_type_id
                LEFT JOIN appointment_statuses aps ON aps.status_id = a.status_id 
                WHERE aw.user_id = @u
                ORDER BY a.appointment_date, a.appointment_time
            ", new() { ["u"] = _userId });

            foreach (var r in rows)
            {
                string dateStr = r["appointment_date"] switch
                {
                    DateTime dt => dt.ToString("dd.MM.yyyy"),
                    DateOnly d => d.ToString("dd.MM.yyyy"),
                    _ => r["appointment_date"]?.ToString() ?? ""
                };

                string timeStr = r["appointment_time"] switch
                {
                    TimeSpan ts => ts.ToString(@"hh\:mm"),
                    DateTime dt => dt.ToString("HH:mm"),
                    _ => r["appointment_time"]?.ToString() ?? ""
                };

                list.Add(new WorkerAppointment
                {
                    Date = dateStr,
                    Time = timeStr,
                    Address = r["address"]?.ToString() ?? "",
                    WorkType = r["type_name"]?.ToString() ?? "—",
                    Status = MapStatus(r["status_name"]?.ToString())
                });
            }

            AppointmentsGrid.ItemsSource = list;
        }

        private string MapStatus(string? status)
        {
            return status switch
            {
                "scheduled" => "Запланирован",
                "in_progress" => "В работе",
                "completed" => "Завершён",
                "cancelled" => "Отменён",
                _ => status ?? ""
            };
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Hide();
        }
    }
}