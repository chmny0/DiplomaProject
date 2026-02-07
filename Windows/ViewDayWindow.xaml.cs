using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class ViewDayWindow : Window
    {
        private readonly DateTime _date;

        public ViewDayWindow(DateTime date)
        {
            InitializeComponent();
            _date = date;
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
                        a.status
                    FROM appointments a
                    LEFT JOIN work_types wt ON wt.type_id = a.work_type_id
                    WHERE a.appointment_date = @d
                    ORDER BY a.appointment_time
                ", new()
                {
                    { "d", _date.Date }
                });

                var items = new List<ViewDayItem>();

                foreach (var r in rows)
                {
                    string status = r["status"].ToString();

                    items.Add(new ViewDayItem
                    {
                        AppointmentId = (int)r["appointment_id"],
                        Time = ((TimeOnly)r["appointment_time"]).ToString("HH:mm"),
                        Address = r["address"].ToString(),
                        WorkType = r["type_name"]?.ToString() ?? "—",
                        Notes = r["notes"]?.ToString() ?? "",
                        Status = status switch
                        {
                            "scheduled" => "🟣 Запланирован",
                            "in_progress" => "🟠 В работе",
                            "completed" => "✅ Завершён",
                            "cancelled" => "❌ Отменён",
                            _ => status
                        },
                        StatusCode = status
                    });
                }

                if (items.Count == 0)
                {
                    items.Add(new ViewDayItem
                    {
                        AppointmentId = 0,
                        Time = "—",
                        Address = "На эту дату нет выездов",
                        WorkType = "",
                        Notes = "",
                        Status = "",
                        StatusCode = ""
                    });
                }

                List.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            new AddAppointmentWindow(_date).ShowDialog();
            Load();
        }

        // ОБРАБОТЧИК КЛИКА ПО ЗАПИСИ
        private void Item_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Получаем Border который кликнули
            if (sender is Border border)
            {
                // Получаем данные из DataContext
                if (border.DataContext is ViewDayItem item)
                {
                    // Проверяем что это реальная запись (не заглушка)
                    if (item.AppointmentId > 0)
                    {
                        // Открываем окно редактирования
                        var editWindow = new EditAppointmentWindow(item.AppointmentId);

                        // Подписываемся на закрытие окна
                        editWindow.Closed += (s, args) =>
                        {
                            // Перезагружаем список после закрытия окна редактирования
                            Load();
                        };

                        editWindow.ShowDialog();
                    }
                }
            }
        }

        public class ViewDayItem
        {
            public int AppointmentId { get; set; }
            public string Time { get; set; }
            public string Address { get; set; }
            public string WorkType { get; set; }
            public string Notes { get; set; }
            public string Status { get; set; }
            public string StatusCode { get; set; }
        }
    }
}