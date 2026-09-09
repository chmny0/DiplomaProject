using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Helpers;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class AdminWorkersWindow : Window
    {
        private List<WorkerRow> _allWorkers = new();
        private WorkerRow? _current;
        private Button? _activeFilter;

        public AdminWorkersWindow()
        {
            InitializeComponent();
            LoadRolesAndStatuses();
            LoadWorkers();
            _activeFilter = FilterAll;
            ApplyFilter("all");
        }

        private class ComboItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        private class LeaveItem
        {
            public string DisplayText { get; set; } = "";
        }

        private void LoadRolesAndStatuses()
        {
            RoleBox.ItemsSource = Db.Query("SELECT role_id, role_name FROM roles ORDER BY role_name")
                .Select(r => new ComboItem
                {
                    Id = Convert.ToInt32(r["role_id"]),
                    Name = MapRole(r["role_name"]?.ToString())
                }).ToList();

            StatusBox.ItemsSource = Db.Query("SELECT status_id, status_name FROM employee_statuses ORDER BY status_id")
                .Select(s => new ComboItem
                {
                    Id = Convert.ToInt32(s["status_id"]),
                    Name = MapWorkerStatus(s["status_name"]?.ToString())
                }).ToList();
        }

        private string MapRole(string? r) => r switch
        {
            "admin" => "Администратор",
            "manager" => "Менеджер",
            "worker" => "Работник",
            _ => r ?? ""
        };

        private string MapWorkerStatus(string? s) => s switch
        {
            "working" => "Работает",
            "vacation" => "В отпуске",
            "sick" => "На больничном",
            "fired" => "Уволен",
            _ => s ?? ""
        };

        private void LoadWorkers()
        {
            var rows = Db.Query(@"
                SELECT u.user_id, u.username, u.full_name,
                       u.role_id, r.role_name,
                       u.status_id, s.status_name
                FROM users u
                JOIN roles r ON r.role_id = u.role_id
                JOIN employee_statuses s ON s.status_id = u.status_id
                ORDER BY u.full_name
            ");

            _allWorkers = rows.Select(r => new WorkerRow
            {
                UserId = Convert.ToInt32(r["user_id"]),
                Username = r["username"]?.ToString() ?? "",
                FullName = r["full_name"]?.ToString() ?? "",
                RoleId = Convert.ToInt32(r["role_id"]),
                RoleName = r["role_name"]?.ToString() ?? "",
                StatusId = Convert.ToInt32(r["status_id"]),
                StatusName = r["status_name"]?.ToString() ?? ""
            }).ToList();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            if (_activeFilter != null)
                _activeFilter.Style = (Style)FindResource("FilterButtonStyle");

            btn.Style = (Style)FindResource("ActiveFilterButtonStyle");
            _activeFilter = btn;

            string filter = btn.Name switch
            {
                "FilterWorking" => "working",
                "FilterLeave" => "leave",
                "FilterFired" => "fired",
                _ => "all"
            };

            ApplyFilter(filter);
        }

        private void ApplyFilter(string filter)
        {
            var filtered = filter switch
            {
                "working" => _allWorkers.Where(w => w.StatusName == "working"),
                "leave" => _allWorkers.Where(w => w.StatusName == "vacation" || w.StatusName == "sick"),
                "fired" => _allWorkers.Where(w => w.StatusName == "fired"),
                _ => _allWorkers.AsEnumerable()
            };

            WorkersGrid.ItemsSource = new ObservableCollection<WorkerRow>(filtered);
            ClearCard();
        }

        private void WorkersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WorkersGrid.SelectedItem is not WorkerRow w)
            {
                ClearCard();
                return;
            }

            _current = w;
            ShowCard(w);
        }

        private void ShowCard(WorkerRow w)
        {
            NameBox.Text = w.FullName;
            LoginBox.Text = w.Username;
            RoleBox.SelectedValue = w.RoleId;
            StatusBox.SelectedValue = w.StatusId;
            PasswordBox.Password = "";

            bool isOnLeave = w.StatusName == "vacation" || w.StatusName == "sick";
            bool isFired = w.StatusName == "fired";

            ReturnButton.Visibility = isOnLeave ? Visibility.Visible : Visibility.Collapsed;
            FireButton.Visibility = isFired ? Visibility.Collapsed : Visibility.Visible;

            LoadLeaves(w.UserId);

            CardPanel.Visibility = Visibility.Visible;
            EmptyPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void ClearCard()
        {
            _current = null;
            CardPanel.Visibility = Visibility.Collapsed;
            EmptyPlaceholder.Visibility = Visibility.Visible;
            AddLeavePanel.Visibility = Visibility.Collapsed;
        }

        private void LoadLeaves(int userId)
        {
            var rows = Db.Query(@"
                SELECT leave_type, start_date, end_date
                FROM vacations
                WHERE user_id = @id
                ORDER BY start_date DESC
            ", new() { ["id"] = userId });

            var items = new ObservableCollection<LeaveItem>();
            foreach (var r in rows)
            {
                string type = r["leave_type"]?.ToString() == "sick" ? "Больничный" : "Отпуск";
                string start = Convert.ToDateTime(r["start_date"]).ToString("dd.MM.yyyy");
                string end = r["end_date"] == DBNull.Value
                    ? "по н.в."
                    : Convert.ToDateTime(r["end_date"]).ToString("dd.MM.yyyy");

                items.Add(new LeaveItem { DisplayText = $"{type}: {start} – {end}" });
            }

            if (items.Count == 0)
                items.Add(new LeaveItem { DisplayText = "Нет записей" });

            LeaveList.ItemsSource = items;
        }

        private void ShowLeavePanel_Click(object sender, RoutedEventArgs e)
        {
            AddLeavePanel.Visibility = Visibility.Visible;
            LeaveStartBox.SelectedDate = null;
            LeaveEndBox.SelectedDate = null;
        }

        private void HideLeavePanel_Click(object sender, RoutedEventArgs e)
        {
            AddLeavePanel.Visibility = Visibility.Collapsed;
        }

        private void AddLeave_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            if (LeaveStartBox.SelectedDate == null)
            {
                MessageBox.Show("Выберите дату начала");
                return;
            }

            var start = LeaveStartBox.SelectedDate.Value;
            var end = LeaveEndBox.SelectedDate;

            if (end.HasValue && start > end.Value)
            {
                MessageBox.Show("Дата начала позже даты окончания");
                return;
            }

            string type = ((ComboBoxItem)LeaveTypeBox.SelectedItem)?.Tag?.ToString() ?? "vacation";

            int conflict = Db.ExecuteScalarInt(@"
                SELECT COUNT(*) FROM vacations
                WHERE user_id = @u
                  AND start_date <= COALESCE(@end, start_date)
                  AND (end_date IS NULL OR end_date >= @start)
            ", new()
            {
                ["u"] = _current.UserId,
                ["start"] = start,
                ["end"] = (object?)end ?? DBNull.Value
            });

            if (conflict > 0)
            {
                MessageBox.Show("Даты пересекаются с существующим отпуском");
                return;
            }

            Db.Execute(@"
                INSERT INTO vacations (user_id, leave_type, start_date, end_date, created_at)
                VALUES (@u, @t, @s, @e, NOW())
            ", new()
            {
                ["u"] = _current.UserId,
                ["t"] = type,
                ["s"] = start,
                ["e"] = (object?)end ?? DBNull.Value
            });

            if (start <= DateTime.Today && (end == null || end >= DateTime.Today))
            {
                string newStatus = type == "sick" ? "sick" : "vacation";
                int statusId = Db.ExecuteScalarInt(
                    "SELECT status_id FROM employee_statuses WHERE status_name = @n",
                    new() { ["n"] = newStatus });

                Db.Execute("UPDATE users SET status_id = @s WHERE user_id = @id",
                    new() { ["s"] = statusId, ["id"] = _current.UserId });
            }

            AddLeavePanel.Visibility = Visibility.Collapsed;
            LoadWorkers();
            ApplyFilter(GetCurrentFilter());
            RefreshCurrentCard();
        }

        private void ReturnToWork_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            string message = _current.StatusName == "sick"
                ? "Закрыть больничный и вернуть сотрудника к работе?"
                : "Прервать отпуск и вернуть сотрудника к работе?";

            if (MessageBox.Show(message, "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                Db.Execute(@"
                    UPDATE vacations
                    SET end_date = CURRENT_DATE - 1
                    WHERE user_id = @uid
                      AND start_date <= CURRENT_DATE
                      AND (end_date IS NULL OR end_date >= CURRENT_DATE)
                ", new() { ["uid"] = _current.UserId });

                int workingId = Db.ExecuteScalarInt(
                    "SELECT status_id FROM employee_statuses WHERE status_name = 'working'", new());

                Db.Execute("UPDATE users SET status_id = @s WHERE user_id = @id",
                    new() { ["s"] = workingId, ["id"] = _current.UserId });

                LoadWorkers();
                ApplyFilter(GetCurrentFilter());
                RefreshCurrentCard();

                MessageBox.Show("Сотрудник возвращён к работе");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void NewWorker_Click(object sender, RoutedEventArgs e)
        {
            WorkersGrid.SelectedItem = null;
            _current = null;

            NameBox.Text = "";
            LoginBox.Text = "";
            PasswordBox.Password = "";
            RoleBox.SelectedIndex = 0;
            StatusBox.SelectedIndex = 0;
            LeaveList.ItemsSource = new ObservableCollection<LeaveItem> { new() { DisplayText = "Нет записей" } };
            AddLeavePanel.Visibility = Visibility.Collapsed;
            ReturnButton.Visibility = Visibility.Collapsed;
            FireButton.Visibility = Visibility.Collapsed;

            CardPanel.Visibility = Visibility.Visible;
            EmptyPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginBox.Text) ||
                string.IsNullOrWhiteSpace(NameBox.Text) ||
                RoleBox.SelectedValue == null ||
                StatusBox.SelectedValue == null)
            {
                MessageBox.Show("Заполните все поля");
                return;
            }

            try
            {
                if (_current == null)
                {
                    if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                    {
                        MessageBox.Show("Введите пароль для нового сотрудника");
                        return;
                    }

                    int exists = Db.ExecuteScalarInt(
                        "SELECT COUNT(*) FROM users WHERE username = @u",
                        new() { ["u"] = LoginBox.Text.Trim() });
                    if (exists > 0)
                    {
                        MessageBox.Show("Логин уже занят");
                        return;
                    }

                    Db.Execute(@"
                        INSERT INTO users (username, password_hash, role_id, full_name, status_id, created_at)
                        VALUES (@u, @p, @r, @f, @s, NOW())
                    ", new()
                    {
                        ["u"] = LoginBox.Text.Trim(),
                        ["p"] = PasswordHelper.Hash(PasswordBox.Password),
                        ["r"] = RoleBox.SelectedValue,
                        ["f"] = NameBox.Text.Trim(),
                        ["s"] = StatusBox.SelectedValue
                    });
                }
                else
                {
                    Db.Execute(@"
                        UPDATE users
                        SET username = @u, full_name = @f,
                            role_id = @r, status_id = @s
                        WHERE user_id = @id
                    ", new()
                    {
                        ["id"] = _current.UserId,
                        ["u"] = LoginBox.Text.Trim(),
                        ["f"] = NameBox.Text.Trim(),
                        ["r"] = RoleBox.SelectedValue,
                        ["s"] = StatusBox.SelectedValue
                    });

                    if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                    {
                        Db.Execute("UPDATE users SET password_hash = @p WHERE user_id = @id",
                            new() { ["id"] = _current.UserId, ["p"] = PasswordHelper.Hash(PasswordBox.Password) });
                    }
                }

                LoadWorkers();
                ApplyFilter(GetCurrentFilter());
                RefreshCurrentCard();

                MessageBox.Show("Сохранено");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Fire_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            int active = Db.ExecuteScalarInt(@"
                SELECT COUNT(*) FROM appointment_workers aw
                JOIN appointments a ON a.appointment_id = aw.appointment_id
                JOIN appointment_statuses s ON s.status_id = a.status_id
                WHERE aw.user_id = @id
                  AND s.status_name IN ('scheduled', 'in_progress')
            ", new() { ["id"] = _current.UserId });

            if (active > 0)
            {
                MessageBox.Show("Сотрудник участвует в активных выездах. Сначала завершите их.");
                return;
            }

            if (MessageBox.Show($"Уволить {_current.FullName}?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            int firedId = Db.ExecuteScalarInt(
                "SELECT status_id FROM employee_statuses WHERE status_name = 'fired'", new());

            Db.Execute("UPDATE users SET status_id = @s WHERE user_id = @id",
                new() { ["s"] = firedId, ["id"] = _current.UserId });

            LoadWorkers();
            ApplyFilter(GetCurrentFilter());
            ClearCard();
        }

        private string GetCurrentFilter() => _activeFilter?.Name switch
        {
            "FilterWorking" => "working",
            "FilterLeave" => "leave",
            "FilterFired" => "fired",
            _ => "all"
        };

        private void RefreshCurrentCard()
        {
            if (_current == null) return;
            var updated = _allWorkers.FirstOrDefault(w => w.UserId == _current.UserId);
            if (updated != null)
            {
                _current = updated;
                ShowCard(updated);
            }
        }
    }
}