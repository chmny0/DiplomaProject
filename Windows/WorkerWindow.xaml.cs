using Npgsql;
using System.Collections.ObjectModel;
using System.Windows;

namespace WpfPlannerApp.Windows
{
    public partial class WorkerWindow : Window
    {
        private readonly int _userId;
        private readonly string _conn =
            "Host=localhost;Port=5432;Database=planner_db;Username=postgres;Password=1234";

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

            using var conn = new NpgsqlConnection(_conn);
            conn.Open();

            var sql = @"
                SELECT a.appointment_date, a.appointment_time,
                       a.address, wt.type_name, a.status
                FROM appointments a
                JOIN appointment_workers aw ON aw.appointment_id = a.appointment_id
                JOIN workers w ON w.worker_id = aw.worker_id
                JOIN work_types wt ON wt.type_id = a.work_type_id
                WHERE w.user_id = @u
                ORDER BY a.appointment_date, a.appointment_time
            ";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", _userId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new WorkerAppointment
                {
                    Date = r.GetDateTime(0).ToString("dd.MM.yyyy"),
                    Time = r.GetTimeSpan(1).ToString(@"hh\:mm"),
                    Address = r.GetString(2),
                    WorkType = r.GetString(3),
                    Status = r.GetString(4)
                });
            }

            AppointmentsGrid.ItemsSource = list;
        }
    }

    public class WorkerAppointment
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string Address { get; set; }
        public string WorkType { get; set; }
        public string Status { get; set; }
    }
}
