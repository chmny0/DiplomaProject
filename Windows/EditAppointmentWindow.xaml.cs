using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class EditAppointmentWindow : Window
    {
        private readonly int _appointmentId;

        public EditAppointmentWindow(int appointmentId)
        {
            InitializeComponent();
            _appointmentId = appointmentId;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var rows = Db.Query(@"
                    SELECT appointment_date, appointment_time, address, notes, status
                    FROM appointments 
                    WHERE appointment_id = @id
                ", new() { { "id", _appointmentId } });

                if (rows.Count == 0) return;

                var row = rows[0];

                DateBox.SelectedDate = ConvertToDateTime(row["appointment_date"]);
                TimeBox.Text = row["appointment_time"].ToString().Substring(0, 5);
                AddressBox.Text = row["address"].ToString();
                NotesBox.Text = row["notes"]?.ToString() ?? "";

                string status = row["status"].ToString();
                StatusBox.SelectedIndex = status switch
                {
                    "scheduled" => 0,
                    "in_progress" => 1,
                    "completed" => 2,
                    "cancelled" => 3,
                    _ => 0
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
        }

        private DateTime ConvertToDateTime(object dateObj)
        {
            if (dateObj is DateOnly d) return d.ToDateTime(TimeOnly.MinValue);
            if (dateObj is DateTime dt) return dt;
            return DateTime.Parse(dateObj.ToString());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
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

            try
            {
                Db.Execute(@"
                    UPDATE appointments 
                    SET appointment_date = @date,
                        appointment_time = @time,
                        address = @address,
                        notes = @notes,
                        status = @status
                    WHERE appointment_id = @id
                ", new()
                {
                    { "id", _appointmentId },
                    { "date", DateBox.SelectedDate.Value },
                    { "time", time },
                    { "address", AddressBox.Text.Trim() },
                    { "notes", NotesBox.Text?.Trim() ?? "" },
                    { "status", StatusBox.SelectedIndex switch
                        {
                            0 => "scheduled",
                            1 => "in_progress",
                            2 => "completed",
                            3 => "cancelled",
                            _ => "scheduled"
                        }
                    }
                });

                MessageBox.Show("Сохранено!");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить выезд?", "Подтвердите",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                Db.Execute("DELETE FROM appointment_workers WHERE appointment_id = @id",
                    new() { { "id", _appointmentId } });

                Db.Execute("DELETE FROM appointments WHERE appointment_id = @id",
                    new() { { "id", _appointmentId } });

                MessageBox.Show("Удалено!");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}");
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
