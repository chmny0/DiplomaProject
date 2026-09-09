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
    public partial class RequestsView : UserControl
    {
        private readonly string _role;
        private readonly int _userId;

        public RequestsView(string role, int userId)
        {
            InitializeComponent();
            _role = role;
            _userId = userId;

            LoadFilters();
            LoadData();
        }

        private void LoadFilters()
        {
            ClientTypeFilter.Items.Clear();
            ClientTypeFilter.Items.Add("Все");
            ClientTypeFilter.Items.Add("individual");
            ClientTypeFilter.Items.Add("legal");
            ClientTypeFilter.SelectedIndex = 0;

            WorkTypeFilter.Items.Clear();
            WorkTypeFilter.Items.Add("Все");
            foreach (var t in Db.Query("SELECT type_name FROM work_types WHERE is_active = TRUE"))
                WorkTypeFilter.Items.Add(t["type_name"].ToString());
            WorkTypeFilter.SelectedIndex = 0;
        }

        public void LoadData()
        {
            var sql = @"
                SELECT 
                    r.request_id,
                    r.preferred_date,
                    r.preferred_time,
                    rs.status_name,
                    wt.type_name,
                    c.phone,
                    ct.type_name AS client_type,
                    COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name
                FROM requests r
                LEFT JOIN request_statuses rs ON rs.status_id = r.status_id
                LEFT JOIN clients c ON c.client_id = r.client_id
                LEFT JOIN client_types ct ON ct.type_id = c.client_type_id
                LEFT JOIN work_types wt ON wt.type_id = r.work_type_id
                WHERE rs.status_name = 'new'
            ";

            var param = new Dictionary<string, object?>();
            if (ClientTypeFilter.SelectedIndex > 0)
            {
                sql += " AND ct.type_name = @ctype";
                param["ctype"] = ClientTypeFilter.SelectedItem.ToString();
            }
            if (WorkTypeFilter.SelectedIndex > 0)
            {
                sql += " AND wt.type_name = @wt";
                param["wt"] = WorkTypeFilter.SelectedItem.ToString();
            }
            if (DateFrom.SelectedDate.HasValue)
            {
                sql += " AND r.preferred_date >= @from";
                param["from"] = DateFrom.SelectedDate.Value;
            }
            if (DateTo.SelectedDate.HasValue)
            {
                sql += " AND r.preferred_date <= @to";
                param["to"] = DateTo.SelectedDate.Value;
            }
            sql += " ORDER BY r.preferred_date, r.preferred_time";

            var rows = Db.Query(sql, param);

            List.ItemsSource = rows.Select(r => new RequestItem
            {
                Id = Convert.ToInt32(r["request_id"]),
                Client = r["client_name"]?.ToString() ?? "",
                Phone = r["phone"]?.ToString() ?? "",
                WorkType = r["type_name"]?.ToString() ?? "",
                Status = MapRequestStatus(r["status_name"]?.ToString()),
                Date = FormatDate(r["preferred_date"]),
                Time = FormatTime(r["preferred_time"]),
                ClientType = r["client_type"]?.ToString() ?? ""
            }).ToList();
        }

        private string FormatDate(object? value)
        {
            return value switch
            {
                DateTime dt => dt.ToString("dd.MM.yyyy"),
                DateOnly d => d.ToString("dd.MM.yyyy"),
                _ => value?.ToString() ?? ""
            };
        }

        private string FormatTime(object? value)
        {
            return value switch
            {
                TimeSpan ts => ts.ToString(@"hh\:mm"),
                DateTime dt => dt.ToString("HH:mm"),
                _ => value?.ToString() ?? ""
            };
        }

        private string MapRequestStatus(string? s) => s switch
        {
            "new" => "Новая",
            "in_progress" => "В работе",
            "appointment_created" => "Создан выезд",
            "completed" => "Завершена",
            "cancelled" => "Отменена",
            _ => s ?? ""
        };

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (IsLoaded)
                LoadData();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ClientTypeFilter.SelectedIndex = 0;
            WorkTypeFilter.SelectedIndex = 0;
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            LoadData();
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe && fe.DataContext is RequestItem item && item.Id > 0)
                {
                    var appointment = Db.Query(@"
                        SELECT appointment_id FROM appointments
                        WHERE request_id = @id
                        LIMIT 1
                    ", new() { ["id"] = item.Id }).FirstOrDefault();

                    if (appointment != null)
                    {
                        int appId = Convert.ToInt32(appointment["appointment_id"]);
                        new EditAppointmentWindow(appId, _role, _userId).ShowDialog();
                    }
                    else
                    {
                        new AddAppointmentWindow(item.Id, _userId).ShowDialog();
                    }

                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void CreateAppointment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe && fe.Tag is int requestId)
                {
                    var win = new AddAppointmentWindow(requestId, _userId);
                    if (win.ShowDialog() == true)
                        LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания выезда: " + ex.Message);
            }
        }
    }
}