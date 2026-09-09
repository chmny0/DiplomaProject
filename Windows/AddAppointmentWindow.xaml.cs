using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Npgsql;
using ClosedXML.Excel;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;

namespace WpfPlannerApp.Windows
{
    public partial class AddAppointmentWindow : Window
    {
        private readonly int _requestId;
        private readonly int _currentUserId;

        private int _clientId;
        private string _clientType = "individual";
        private string _clientAddress = "";
        private readonly List<string> _uploadedFiles = new();
        private int? _generatedContractId = null;

        public AddAppointmentWindow(int requestId, int currentUserId)
        {
            InitializeComponent();
            _requestId = requestId;
            _currentUserId = currentUserId;

            LoadWorkTypes();
            LoadWorkers();
            LoadFromRequest();
        }

        private void LoadWorkTypes()
        {
            var types = Db.Query(@"
                SELECT type_id, type_name FROM work_types
                WHERE is_active = TRUE ORDER BY type_name
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
                WHERE r.role_name = 'worker' AND s.status_name = 'working'
                ORDER BY u.full_name
            ");
            WorkersList.ItemsSource = workers.Select(w => new WorkerItem
            {
                Id = Convert.ToInt32(w["user_id"]),
                FullName = w["full_name"]?.ToString() ?? ""
            }).ToList();
        }

        private void LoadFromRequest()
        {
            var row = Db.Query(@"
                SELECT 
                    r.request_id, r.client_id, r.work_type_id,
                    r.preferred_date, r.preferred_time, r.description,
                    ct.type_name AS client_type,
                    COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name,
                    c.phone, c.address
                FROM requests r
                JOIN clients c ON c.client_id = r.client_id
                JOIN client_types ct ON ct.type_id = c.client_type_id
                WHERE r.request_id = @id
            ", new() { ["id"] = _requestId }).FirstOrDefault();

            if (row == null) { MessageBox.Show("Заявка не найдена"); Close(); return; }

            _clientId = Convert.ToInt32(row["client_id"]);
            _clientType = row["client_type"]?.ToString() ?? "individual";
            _clientAddress = row["address"]?.ToString() ?? "";

            SubtitleText.Text = $"на основе заявки №{row["request_id"]}";
            ClientBox.Text = row["client_name"]?.ToString() ?? "—";

            if (row["work_type_id"] != DBNull.Value)
                WorkTypeBox.SelectedValue = Convert.ToInt32(row["work_type_id"]);

            if (row["preferred_date"] != DBNull.Value)
                DateBox.SelectedDate = Convert.ToDateTime(row["preferred_date"]);
            else
                DateBox.SelectedDate = DateTime.Today.AddDays(1);

            if (row["preferred_time"] != DBNull.Value)
                TimeBox.Text = row["preferred_time"] is TimeSpan ts
                    ? ts.ToString(@"hh\:mm") : row["preferred_time"]?.ToString() ?? "10:00";

            NotesBox.Text = row["description"]?.ToString() ?? "";
            AddressBox.Text = _clientAddress;

            if (_clientType == "legal") LoadContractInfo();
        }

        private void LoadContractInfo()
        {
            ContractSection.Visibility = Visibility.Visible;
            var contract = Db.Query(@"
                SELECT contract_id, contract_number, start_date, end_date FROM contracts
                WHERE client_id = @client AND status = 'active'
                ORDER BY start_date DESC LIMIT 1
            ", new() { ["client"] = _clientId }).FirstOrDefault();

            if (contract != null)
            {
                string number = contract["contract_number"]?.ToString() ?? "б/н";
                string start = Convert.ToDateTime(contract["start_date"]).ToString("dd.MM.yyyy");
                string end = contract["end_date"] != DBNull.Value
                    ? Convert.ToDateTime(contract["end_date"]).ToString("dd.MM.yyyy") : "бессрочный";
                ContractInfoText.Text = $"№{number} от {start}, действует до {end}";
                ContractWarningText.Visibility = Visibility.Collapsed;
                GenerateContractButton.Visibility = Visibility.Collapsed;
                GeneratedContractText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ContractInfoText.Text = "Активный договор отсутствует";
                ContractWarningText.Visibility = Visibility.Visible;
                GenerateContractButton.Visibility = Visibility.Visible;
                GeneratedContractText.Visibility = Visibility.Collapsed;
            }
        }

        private void GenerateContract_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var client = Db.Query(@"
                    SELECT company_name, inn, kpp, legal_address, director_name
                    FROM clients
                    WHERE client_id = @id
                ", new() { ["id"] = _clientId }).FirstOrDefault();

                if (client == null)
                {
                    MessageBox.Show("Клиент не найден");
                    return;
                }

                string companyName = client["company_name"]?.ToString() ?? "ООО «Не указано»";
                string inn = client["inn"]?.ToString() ?? "—";
                string kpp = client["kpp"]?.ToString() ?? "—";
                string legalAddr = client["legal_address"]?.ToString() ?? "—";
                string director = client["director_name"]?.ToString() ?? "Генеральный директор";

                string workTypeName = "не указана";
                if (WorkTypeBox.SelectedItem is ComboItem ci)
                    workTypeName = ci.Name;

                string appointmentDate = DateBox.SelectedDate?.ToString("dd.MM.yyyy") ?? "____";
                string appointmentTime = TimeBox.Text.Trim();
                string address = AddressBox.Text.Trim();
                string contractNumber = $"Д-{DateTime.Now:yyyyMMdd}-{_clientId:D4}";
                string fileName = $"Договор_{contractNumber}_{DateTime.Now:yyyyMMddHHmmss}.docx";
                string filePath = System.IO.Path.Combine(FileService.GetStorageFolder(), fileName);

                ContractGenerator.GenerateContract(filePath, contractNumber, companyName, director,
                    workTypeName, address, appointmentDate, appointmentTime, inn, kpp, legalAddr);

                int contractId = Db.ExecuteScalarInt(@"
                    INSERT INTO contracts (client_id, contract_number, start_date, status, created_at, created_by)
                    VALUES (@client, @num, CURRENT_DATE, 'active', NOW(), @by)
                    RETURNING contract_id
                ", new()
                {
                    ["client"] = _clientId,
                    ["num"] = contractNumber,
                    ["by"] = _currentUserId
                });

                int docTypeId = Db.ExecuteScalarInt(
                    "SELECT type_id FROM document_types WHERE type_name = 'contract'", new());

                Db.Execute(@"
                    INSERT INTO documents
                        (document_type_id, contract_id, document_number, document_date, file_path, created_at, created_by)
                    VALUES (@type, @contract, @num, CURRENT_DATE, @path, NOW(), @by)
                ", new()
                {
                    ["type"] = docTypeId,
                    ["contract"] = contractId,
                    ["num"] = contractNumber,
                    ["path"] = filePath,
                    ["by"] = _currentUserId
                });

                _generatedContractId = contractId;

                ContractInfoText.Text = $"Договор №{contractNumber} от {DateTime.Now:dd.MM.yyyy} (создан)";
                ContractWarningText.Visibility = Visibility.Collapsed;
                GenerateContractButton.Visibility = Visibility.Collapsed;
                GeneratedContractText.Text = $"Файл: {System.IO.Path.GetFileName(filePath)}";
                GeneratedContractText.Visibility = Visibility.Visible;

                MessageBox.Show($"Договор №{contractNumber} успешно создан!\n\nФайл сохранён:\n{filePath}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка генерации договора:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UploadDoc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? filePath = FileService.PickAndSaveFile();
                if (filePath != null)
                {
                    _uploadedFiles.Add(filePath);
                    MessageBox.Show($"Файл добавлен: {System.IO.Path.GetFileName(filePath)}\n"
                        + $"Всего файлов: {_uploadedFiles.Count}", "Готово");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WorkTypeBox.SelectedValue == null)
                { MessageBox.Show("Выберите тип услуги"); return; }
                if (!DateBox.SelectedDate.HasValue)
                { MessageBox.Show("Выберите дату"); return; }
                if (!TimeSpan.TryParse(TimeBox.Text.Trim(), out var time))
                { MessageBox.Show("Неверное время"); return; }

                var workers = WorkersList.SelectedItems.Cast<WorkerItem>().ToList();
                if (workers.Count == 0)
                { MessageBox.Show("Выберите сотрудников"); return; }

                if (_clientType == "legal")
                {
                    int cc = Db.ExecuteScalarInt(
                        "SELECT COUNT(*) FROM contracts WHERE client_id = @c AND status = 'active'",
                        new() { ["c"] = _clientId });
                    if (cc == 0 && MessageBox.Show("Нет активного договора. Создать наряд?",
                        "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                }

                foreach (var w in workers)
                {
                    int conflict = Db.ExecuteScalarInt(@"
                        SELECT COUNT(*) FROM appointment_workers aw
                        JOIN appointments a ON a.appointment_id = aw.appointment_id
                        JOIN appointment_statuses s ON s.status_id = a.status_id
                        WHERE aw.user_id = @uid AND a.appointment_date = @date
                          AND a.appointment_time = @time
                          AND s.status_name IN ('scheduled','in_progress')
                    ", new() { ["uid"] = w.Id, ["date"] = DateBox.SelectedDate.Value, ["time"] = time });

                    if (conflict > 0)
                    { MessageBox.Show($"Сотрудник «{w.FullName}» уже занят"); return; }
                }

                int statusId = Db.ExecuteScalarInt(
                    "SELECT status_id FROM appointment_statuses WHERE status_name = 'scheduled'", new());

                using var conn = Db.GetConnection();
                conn.Open();
                using var tx = conn.BeginTransaction();

                int appointmentId;
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO appointments
                        (request_id, client_id, work_type_id, appointment_date,
                         appointment_time, address, notes, status_id, created_at, created_by)
                    VALUES (@req, @client, @type, @date, @time, @addr, @notes, @status, NOW(), @by)
                    RETURNING appointment_id
                ", conn, tx))
                {
                    cmd.Parameters.AddWithValue("req", _requestId);
                    cmd.Parameters.AddWithValue("client", _clientId);
                    cmd.Parameters.AddWithValue("type", (int)WorkTypeBox.SelectedValue);
                    cmd.Parameters.AddWithValue("date", DateBox.SelectedDate.Value);
                    cmd.Parameters.AddWithValue("time", time);
                    cmd.Parameters.AddWithValue("addr", (object?)AddressBox.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("notes", (object?)NotesBox.Text.Trim() ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("status", statusId);
                    cmd.Parameters.AddWithValue("by", _currentUserId);
                    appointmentId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                foreach (var w in workers)
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO appointment_workers(appointment_id,user_id) VALUES(@a,@u)", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("a", appointmentId);
                        cmd.Parameters.AddWithValue("u", w.Id);
                        cmd.ExecuteNonQuery();
                    }

                int docTypeId = Db.ExecuteScalarInt(
                    "SELECT type_id FROM document_types WHERE type_name = 'completion_act'", new());

                foreach (var filePath in _uploadedFiles)
                {
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO documents
                            (document_type_id, appointment_id, document_number, document_date, file_path, created_at, created_by)
                        VALUES (@type, @app, @num, CURRENT_DATE, @path, NOW(), @by)
                    ", conn, tx);
                    cmd.Parameters.AddWithValue("type", docTypeId);
                    cmd.Parameters.AddWithValue("app", appointmentId);
                    cmd.Parameters.AddWithValue("num", "ДОК-" + DateTime.Now.ToString("yyyyMMddHHmm"));
                    cmd.Parameters.AddWithValue("path", filePath);
                    cmd.Parameters.AddWithValue("by", _currentUserId);
                    cmd.ExecuteNonQuery();
                }

                int reqStatusId = Db.ExecuteScalarInt(
                    "SELECT status_id FROM request_statuses WHERE status_name = 'appointment_created'", new());
                using (var cmd = new NpgsqlCommand(
                    "UPDATE requests SET status_id = @s WHERE request_id = @id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("s", reqStatusId);
                    cmd.Parameters.AddWithValue("id", _requestId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();

                MessageBox.Show($"Наряд №{appointmentId} создан", "Успешно");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}