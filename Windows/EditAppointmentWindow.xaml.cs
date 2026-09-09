using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Npgsql;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class EditAppointmentWindow : Window
    {
        private readonly int _appointmentId;
        private readonly string _currentUserRole;
        private readonly int _currentUserId;

        private int _clientId;
        private string _clientType = "individual";

        public EditAppointmentWindow(int appointmentId, string currentUserRole, int currentUserId)
        {
            InitializeComponent();
            _appointmentId = appointmentId;
            _currentUserRole = currentUserRole;
            _currentUserId = currentUserId;

            LoadWorkTypes();
            LoadWorkers();
            LoadStatuses();
            LoadData();
            LoadDocuments();

            ApplyRoleRestrictions();
        }

        private void ApplyRoleRestrictions()
        {
            bool canEdit = _currentUserRole == "admin" || _currentUserRole == "manager";

            WorkTypeBox.IsEnabled = canEdit;
            DateBox.IsEnabled = canEdit;
            TimeBox.IsEnabled = canEdit;
            AddressBox.IsEnabled = canEdit;
            NotesBox.IsEnabled = canEdit;
            StatusBox.IsEnabled = canEdit;
            WorkersList.IsEnabled = canEdit;
            AddActButton.IsEnabled = canEdit;
            UploadDocButton.IsEnabled = canEdit;

            if (!canEdit)
            {
                TitleText.Text = "Просмотр наряда";
                Title = "Просмотр наряда";
                UploadDocButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadWorkTypes()
        {
            var types = Db.Query(@"
                SELECT type_id, type_name FROM work_types
                WHERE is_active = TRUE
                ORDER BY type_name
            ");
            WorkTypeBox.ItemsSource = types.Select(t => new ComboItem
            {
                Id = Convert.ToInt32(t["type_id"]),
                Name = t["type_name"]?.ToString() ?? ""
            }).ToList();
        }

        private void LoadWorkers()
        {
            var workers = Db.Query(@"
                SELECT u.user_id, u.full_name
                FROM users u
                JOIN roles r ON r.role_id = u.role_id
                JOIN employee_statuses s ON s.status_id = u.status_id
                WHERE r.role_name = 'worker'
                  AND s.status_name = 'working'
                ORDER BY u.full_name
            ");
            WorkersList.ItemsSource = workers.Select(w => new WorkerItem
            {
                Id = Convert.ToInt32(w["user_id"]),
                FullName = w["full_name"]?.ToString() ?? ""
            }).ToList();
        }

        private void LoadStatuses()
        {
            var statuses = Db.Query(@"
                SELECT status_id, status_name FROM appointment_statuses
                ORDER BY status_id
            ");
            StatusBox.ItemsSource = statuses.Select(s => new ComboItem
            {
                Id = Convert.ToInt32(s["status_id"]),
                Name = MapAppointmentStatus(s["status_name"]?.ToString())
            }).ToList();
        }

        private string MapAppointmentStatus(string? s) => s switch
        {
            "scheduled" => "Запланирован",
            "in_progress" => "В работе",
            "completed" => "Завершён",
            "cancelled" => "Отменён",
            _ => s ?? ""
        };

        private void LoadData()
        {
            var row = Db.Query(@"
                SELECT 
                    a.request_id,
                    a.client_id,
                    a.work_type_id,
                    a.appointment_date,
                    a.appointment_time,
                    a.address,
                    a.notes,
                    a.status_id,
                    ct.type_name AS client_type,
                    COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name,
                    c.phone,
                    c.email
                FROM appointments a
                JOIN clients c ON c.client_id = a.client_id
                JOIN client_types ct ON ct.type_id = c.client_type_id
                WHERE a.appointment_id = @id
            ", new() { ["id"] = _appointmentId }).FirstOrDefault();

            if (row == null)
            {
                MessageBox.Show("Наряд не найден");
                Close();
                return;
            }

            _clientId = Convert.ToInt32(row["client_id"]);
            _clientType = row["client_type"]?.ToString() ?? "individual";

            ClientBox.Text = row["client_name"]?.ToString() ?? "—";
            PhoneBox.Text = row["phone"]?.ToString() ?? "—";
            EmailBox.Text = row["email"]?.ToString() ?? "—";

            if (row["work_type_id"] != DBNull.Value)
                WorkTypeBox.SelectedValue = Convert.ToInt32(row["work_type_id"]);

            DateBox.SelectedDate = Db.GetDate(row["appointment_date"]);
            var time = Db.GetTime(row["appointment_time"]);
            TimeBox.Text = time?.ToString(@"hh\:mm") ?? "10:00";

            AddressBox.Text = row["address"]?.ToString() ?? "";
            NotesBox.Text = row["notes"]?.ToString() ?? "";

            if (row["status_id"] != DBNull.Value)
                StatusBox.SelectedValue = Convert.ToInt32(row["status_id"]);

            var workerIds = Db.Query(@"
                SELECT user_id FROM appointment_workers
                WHERE appointment_id = @id
            ", new() { ["id"] = _appointmentId })
            .Select(r => Convert.ToInt32(r["user_id"]))
            .ToHashSet();

            foreach (WorkerItem item in WorkersList.Items)
            {
                if (workerIds.Contains(item.Id))
                    WorkersList.SelectedItems.Add(item);
            }

            if (_clientType == "legal")
                LoadContractInfo();
        }

        private void LoadContractInfo()
        {
            var contract = Db.Query(@"
                SELECT contract_number, start_date, end_date
                FROM contracts
                WHERE client_id = @client AND status = 'active'
                ORDER BY start_date DESC
                LIMIT 1
            ", new() { ["client"] = _clientId }).FirstOrDefault();

            if (contract != null)
            {
                string number = contract["contract_number"]?.ToString() ?? "б/н";
                string start = Convert.ToDateTime(contract["start_date"]).ToString("dd.MM.yyyy");
                string end = contract["end_date"] != DBNull.Value
                    ? Convert.ToDateTime(contract["end_date"]).ToString("dd.MM.yyyy")
                    : "бессрочный";

                ContractInfoText.Text = $"№{number} от {start}, действует до {end}";
                ContractSection.Visibility = Visibility.Visible;
            }
            else
            {
                ContractSection.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadDocuments()
        {
            var docs = Db.Query(@"
                SELECT d.document_id, dt.type_name, d.document_number, d.document_date, d.file_path
                FROM documents d
                JOIN document_types dt ON dt.type_id = d.document_type_id
                WHERE d.appointment_id = @appId
                   OR d.contract_id IN (
                       SELECT contract_id FROM contracts 
                       WHERE client_id = @clientId AND status = 'active'
                   )
                ORDER BY d.document_date DESC
            ", new()
            {
                ["appId"] = _appointmentId,
                ["clientId"] = _clientId
            });

            var items = new ObservableCollection<DocumentItem>();

            foreach (var d in docs)
            {
                string typeName = d["type_name"]?.ToString() ?? "";
                string number = d["document_number"]?.ToString() ?? "б/н";
                string date = d["document_date"] is DateTime dt
                    ? dt.ToString("dd.MM.yyyy")
                    : d["document_date"]?.ToString() ?? "";
                string? filePath = d["file_path"]?.ToString();
                string fileName = FileService.GetFileName(filePath);

                string display = string.IsNullOrWhiteSpace(fileName)
                    ? $"{typeName} №{number} от {date}"
                    : $"{typeName} №{number} от {date}\nФайл: {fileName}";

                items.Add(new DocumentItem
                {
                    DocumentId = Convert.ToInt32(d["document_id"]),
                    DisplayText = display,
                    TypeName = typeName,
                    FilePath = filePath
                });
            }

            DocumentsList.ItemsSource = null;
            DocumentsList.ItemsSource = items;

            UpdateAddActButtonVisibility();
        }

        private void UpdateAddActButtonVisibility()
        {
            int? currentStatusId = StatusBox.SelectedValue as int?;
            bool isCompleted = currentStatusId != null
                && GetStatusName(currentStatusId.Value) == "completed";

            var items = DocumentsList.ItemsSource as ObservableCollection<DocumentItem>;
            bool hasAct = items?.Any(i => i.TypeName == "completion_act") ?? false;

            AddActButton.Visibility = (isCompleted && !hasAct)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private string GetStatusName(int statusId)
        {
            var row = Db.Query(@"
                SELECT status_name FROM appointment_statuses WHERE status_id = @id
            ", new() { ["id"] = statusId }).FirstOrDefault();

            return row?["status_name"]?.ToString() ?? "";
        }

        private void StatusBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                UpdateAddActButtonVisibility();
        }

        private void UploadDoc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = FileService.PickAndSaveFile();
                if (filePath == null) return;

                int docTypeId = Db.ExecuteScalarInt(
                    "SELECT type_id FROM document_types WHERE type_name = 'completion_act'",
                    new());

                string docNumber = $"ДОК-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 20);

                Db.Execute(@"
                    INSERT INTO documents
                    (document_type_id, appointment_id, document_number, document_date, file_path, created_at, created_by)
                    VALUES (@type, @app, @num, CURRENT_DATE, @path, NOW(), @by)
                ", new()
                {
                    ["type"] = docTypeId,
                    ["app"] = _appointmentId,
                    ["num"] = docNumber,
                    ["path"] = filePath,
                    ["by"] = _currentUserId
                });

                LoadDocuments();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки файла: " + ex.Message);
            }
        }

        private void AddAct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = Db.Query(@"
                    SELECT 
                        a.appointment_date,
                        a.address,
                        a.notes,
                        wt.type_name,
                        COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name
                    FROM appointments a
                    JOIN clients c ON c.client_id = a.client_id
                    LEFT JOIN work_types wt ON wt.type_id = a.work_type_id
                    WHERE a.appointment_id = @id
                ", new() { ["id"] = _appointmentId }).FirstOrDefault();

                if (data == null)
                {
                    MessageBox.Show("Наряд не найден");
                    return;
                }

                string clientName = data["client_name"]?.ToString() ?? "Не указан";
                string workType = data["type_name"]?.ToString() ?? "Не указана";
                string address = data["address"]?.ToString() ?? "—";
                string date = data["appointment_date"] is DateTime dt
                    ? dt.ToString("dd.MM.yyyy")
                    : data["appointment_date"]?.ToString() ?? "—";
                string notes = data["notes"]?.ToString() ?? "";

                string actNumber = $"АКТ-{_appointmentId:D5}";
                string fileName = $"Акт_{actNumber}_{DateTime.Now:yyyyMMddHHmmss}.docx";
                string filePath = System.IO.Path.Combine(FileService.GetStorageFolder(), fileName);

                ContractGenerator.GenerateAct(filePath, actNumber, clientName, workType, address, date, notes);

                int actTypeId = Db.ExecuteScalarInt(
                    "SELECT type_id FROM document_types WHERE type_name = 'completion_act'", new());

                Db.Execute(@"
                    INSERT INTO documents
                    (document_type_id, appointment_id, document_number, document_date, file_path, created_at, created_by)
                    VALUES (@type, @app, @num, CURRENT_DATE, @path, NOW(), @by)
                ", new()
                {
                    ["type"] = actTypeId,
                    ["app"] = _appointmentId,
                    ["num"] = actNumber,
                    ["path"] = filePath,
                    ["by"] = _currentUserId
                });

                LoadDocuments();

                MessageBox.Show($"Акт №{actNumber} создан!\nФайл: {fileName}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания акта: " + ex.Message);
            }
        }

        private void ViewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is int docId)
            {
                var doc = Db.Query(@"
                    SELECT file_path, document_number
                    FROM documents
                    WHERE document_id = @id
                ", new() { ["id"] = docId }).FirstOrDefault();

                if (doc == null) return;

                string? filePath = doc["file_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    MessageBox.Show(
                        $"Документ №{doc["document_number"]}\nФайл не прикреплён.",
                        "Документ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                FileService.OpenFile(filePath);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUserRole != "admin" && _currentUserRole != "manager")
            {
                MessageBox.Show("Недостаточно прав");
                return;
            }

            try
            {
                if (WorkTypeBox.SelectedValue == null)
                { MessageBox.Show("Выберите услугу"); return; }

                if (!DateBox.SelectedDate.HasValue)
                { MessageBox.Show("Выберите дату"); return; }

                if (!TimeSpan.TryParse(TimeBox.Text, out var time))
                { MessageBox.Show("Неверное время"); return; }

                if (StatusBox.SelectedValue == null)
                { MessageBox.Show("Выберите статус"); return; }

                var workers = WorkersList.SelectedItems.Cast<WorkerItem>().ToList();
                if (workers.Count == 0)
                { MessageBox.Show("Выберите бригаду"); return; }

                using var conn = Db.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                using (var cmd = new NpgsqlCommand(@"
                    UPDATE appointments
                    SET work_type_id = @type,
                        appointment_date = @date,
                        appointment_time = @time,
                        address = @addr,
                        notes = @notes,
                        status_id = @status
                    WHERE appointment_id = @id
                ", conn, tx))
                {
                    cmd.Parameters.AddWithValue("type", (int)WorkTypeBox.SelectedValue);
                    cmd.Parameters.AddWithValue("date", DateBox.SelectedDate.Value);
                    cmd.Parameters.AddWithValue("time", time);
                    cmd.Parameters.AddWithValue("addr", (object?)AddressBox.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("notes", (object?)NotesBox.Text.Trim() ?? DBNull.Value);
                    int statusId = StatusBox.SelectedValue switch
                    {
                        ComboItem ci => ci.Id,
                        int i => i,
                        _ => Convert.ToInt32(StatusBox.SelectedValue)
                    };
                    cmd.Parameters.AddWithValue("status", statusId);
                    cmd.Parameters.AddWithValue("id", _appointmentId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new NpgsqlCommand(
                    "DELETE FROM appointment_workers WHERE appointment_id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("id", _appointmentId);
                    cmd.ExecuteNonQuery();
                }

                foreach (var w in workers)
                {
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO appointment_workers (appointment_id, user_id)
                        VALUES (@a, @u)
                    ", conn, tx);
                    cmd.Parameters.AddWithValue("a", _appointmentId);
                    cmd.Parameters.AddWithValue("u", w.Id);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                int savedStatusId = StatusBox.SelectedValue switch
                {
                    ComboItem ci => ci.Id,
                    int i => i,
                    _ => Convert.ToInt32(StatusBox.SelectedValue)
                };

                string savedStatusName = GetStatusName(savedStatusId);

                if (savedStatusName == "in_progress")
                {
                    var reqId = Db.ExecuteScalarInt(
                        "SELECT request_id FROM appointments WHERE appointment_id = @id",
                        new() { ["id"] = _appointmentId });

                    int inProgressStatusId = Db.ExecuteScalarInt(
                        "SELECT status_id FROM request_statuses WHERE status_name = 'in_progress'", new());

                    Db.Execute("UPDATE requests SET status_id = @s WHERE request_id = @id",
                        new() { ["s"] = inProgressStatusId, ["id"] = reqId });
                }

                if (savedStatusName == "completed")
                {
                    var reqId = Db.ExecuteScalarInt(
                        "SELECT request_id FROM appointments WHERE appointment_id = @id",
                        new() { ["id"] = _appointmentId });

                    int completedStatusId = Db.ExecuteScalarInt(
                        "SELECT status_id FROM request_statuses WHERE status_name = 'completed'", new());

                    Db.Execute("UPDATE requests SET status_id = @s WHERE request_id = @id",
                        new() { ["s"] = completedStatusId, ["id"] = reqId });
                }

                UpdateAddActButtonVisibility();

                MessageBox.Show("Наряд сохранён");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}