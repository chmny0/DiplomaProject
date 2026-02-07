using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Services;
using System.Text.RegularExpressions;

namespace WpfPlannerApp.Windows
{
    public partial class AddAppointmentWindow : Window
    {
        private readonly int _currentUserId;

        public AddAppointmentWindow(DateTime date, int currentUserId = 1)
        {
            InitializeComponent();
            DateBox.SelectedDate = date;
            _currentUserId = currentUserId;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var workTypesRaw = Db.Query("SELECT type_id, type_name FROM work_types ORDER BY type_name");
                var workTypes = new List<dynamic>();

                foreach (var row in workTypesRaw)
                {
                    workTypes.Add(new
                    {
                        type_id = Convert.ToInt32(row["type_id"]),
                        type_name = row["type_name"].ToString()
                    });
                }

                WorkTypeBox.ItemsSource = workTypes;
                WorkTypeBox.DisplayMemberPath = "type_name";
                WorkTypeBox.SelectedValuePath = "type_id";

                var workersRaw = Db.Query(@"
                    SELECT w.worker_id, u.full_name
                    FROM workers w
                    JOIN users u ON u.user_id = w.user_id
                    WHERE u.is_active = TRUE
                    ORDER BY u.full_name
                ");

                var workers = new List<dynamic>();
                foreach (var row in workersRaw)
                {
                    workers.Add(new
                    {
                        worker_id = Convert.ToInt32(row["worker_id"]),
                        full_name = row["full_name"].ToString()
                    });
                }

                WorkersList.ItemsSource = workers;
                WorkersList.DisplayMemberPath = "full_name";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!DateBox.SelectedDate.HasValue)
                {
                    MessageBox.Show("Выберите дату");
                    return;
                }

                if (!TimeSpan.TryParse(TimeBox.Text, out TimeSpan time))
                {
                    MessageBox.Show("Введите время в формате ЧЧ:мм");
                    return;
                }

                if (string.IsNullOrWhiteSpace(AddressBox.Text))
                {
                    MessageBox.Show("Введите адрес");
                    return;
                }

                if (WorkTypeBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите тип работ");
                    return;
                }

                if (WorkersList.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы одного работника");
                    return;
                }

                int appointmentId = Db.ExecuteScalarInt(@"
                    INSERT INTO appointments
                    (appointment_date, appointment_time, address, work_type_id, notes, status, created_by)
                    VALUES (@date, @time, @addr, @type, @notes, 'scheduled', @creator)
                    RETURNING appointment_id
                ", new()
                {
                    { "date", DateBox.SelectedDate.Value.Date },
                    { "time", time },
                    { "addr", AddressBox.Text.Trim() },
                    { "type", Convert.ToInt32(WorkTypeBox.SelectedValue) },
                    { "notes", NotesBox.Text?.Trim() ?? "" },
                    { "creator", _currentUserId }
                });

                int leaderId = Convert.ToInt32(((dynamic)WorkersList.SelectedItems[0]).worker_id);

                foreach (dynamic worker in WorkersList.SelectedItems)
                {
                    int workerId = Convert.ToInt32(worker.worker_id);

                    Db.Execute(@"
                        INSERT INTO appointment_workers
                            (appointment_id, worker_id, is_team_lead)
                        VALUES
                            (@aid, @wid, @lead)
                    ", new()
                    {
                        { "aid", appointmentId },
                        { "wid", workerId },
                        { "lead", workerId == leaderId }
                    });
                }

                MessageBox.Show("Выезд успешно создан");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void TimeBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"[0-9:]");
        }

        private void TimeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TimeBox.Text.Length == 2 && !TimeBox.Text.Contains(":"))
                TimeBox.Text += ":";

            TimeBox.CaretIndex = TimeBox.Text.Length;
        }
    }
}
