using System;
using System.Linq;
using System.Windows;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class EditRequestWindow : Window
    {
        private int _requestId;
        private int _currentUserId;
        private string _currentUserRole;
        private string _clientType = "individual";

        public EditRequestWindow(int requestId, string currentUserRole, int currentUserId)
        {
            InitializeComponent();
            _requestId = requestId;
            _currentUserRole = currentUserRole;
            _currentUserId = currentUserId;

            LoadStatuses();
            LoadData();
            ApplyRoleRestrictions();
        }

        private void ApplyRoleRestrictions()
        {
            bool canEdit = _currentUserRole == "admin" || _currentUserRole == "manager";

            if (!canEdit)
            {
                StatusBox.IsEnabled = false;
                DescriptionBox.IsReadOnly = true;

                var createButton = FindName("CreateAppointmentButton") as System.Windows.Controls.Button;
                if (createButton != null)
                    createButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadStatuses()
        {
            var list = Db.Query("SELECT status_id, status_name FROM request_statuses ORDER BY status_id");

            StatusBox.ItemsSource = list.Select(s => new
            {
                id = Convert.ToInt32(s["status_id"]),
                name = MapRequestStatus(s["status_name"]?.ToString())
            }).ToList();
        }

        private string MapRequestStatus(string? s) => s switch
        {
            "new" => "🆕 Новая",
            "in_progress" => "🔄 В работе",
            "appointment_created" => "📅 Создан выезд",
            "completed" => "✅ Завершена",
            "cancelled" => "❌ Отменена",
            _ => s ?? ""
        };

        private void LoadData()
        {
            var row = Db.Query(@"
                SELECT 
                    r.request_id,
                    r.client_id,
                    r.work_type_id,
                    r.preferred_date,
                    r.preferred_time,
                    r.description,
                    r.status_id,
                    r.created_at,
                    rs.status_name,
                    wt.type_name,
                    c.phone,
                    c.address,
                    ct.type_name AS client_type,
                    COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name
                FROM requests r
                JOIN clients c ON c.client_id = r.client_id
                JOIN client_types ct ON ct.type_id = c.client_type_id
                LEFT JOIN request_statuses rs ON rs.status_id = r.status_id
                LEFT JOIN work_types wt ON wt.type_id = r.work_type_id
                WHERE r.request_id = @id
            ", new() { ["id"] = _requestId }).FirstOrDefault();

            if (row == null)
            {
                MessageBox.Show("Заявка не найдена");
                Close();
                return;
            }

            _clientType = row["client_type"]?.ToString() ?? "individual";

            ClientText.Text = row["client_name"]?.ToString() ?? "—";
            PhoneText.Text = row["phone"]?.ToString() ?? "—";
            AddressText.Text = row["address"]?.ToString() ?? "—";
            WorkTypeText.Text = row["type_name"]?.ToString() ?? "—";

            DateText.Text = FormatDate(row["created_at"]);

            PrefDateText.Text = row["preferred_date"] == DBNull.Value || row["preferred_date"] == null
                ? "—"
                : FormatDate(row["preferred_date"]);

            PrefTimeText.Text = row["preferred_time"] == DBNull.Value || row["preferred_time"] == null
                ? "—"
                : row["preferred_time"] is TimeSpan ts
                    ? ts.ToString(@"hh\:mm")
                    : row["preferred_time"]?.ToString() ?? "—";

            DescriptionBox.Text = row["description"]?.ToString() ?? "";

            StatusBox.SelectedValue = Convert.ToInt32(row["status_id"]);
        }

        private string FormatDate(object? value)
        {
            return value switch
            {
                DateTime dt => dt.ToString("dd.MM.yyyy"),
                DateOnly d => d.ToString("dd.MM.yyyy"),
                _ => value?.ToString() ?? "—"
            };
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StatusBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите статус");
                    return;
                }

                Db.Execute(@"
                    UPDATE requests
                    SET description = @desc,
                        status_id = @status
                    WHERE request_id = @id
                ", new()
                {
                    ["desc"] = DescriptionBox.Text ?? "",
                    ["status"] = StatusBox.SelectedValue,
                    ["id"] = _requestId
                });

                MessageBox.Show("Сохранено");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message);
            }
        }

        private void CreateAppointment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clientType == "legal")
                {
                    var check = Db.Query(@"
                        SELECT c.client_id
                        FROM requests r
                        JOIN clients c ON c.client_id = r.client_id
                        WHERE r.request_id = @id
                    ", new() { ["id"] = _requestId }).FirstOrDefault();

                    if (check != null)
                    {
                        int clientId = Convert.ToInt32(check["client_id"]);

                        var contract = Db.Query(@"
                            SELECT contract_id 
                            FROM contracts
                            WHERE client_id = @client
                              AND status = 'active'
                            LIMIT 1
                        ", new() { ["client"] = clientId });

                        if (contract.Count == 0)
                        {
                            var result = MessageBox.Show(
                                "У клиента нет активного договора. Всё равно создать наряд?",
                                "Предупреждение",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result != MessageBoxResult.Yes)
                                return;
                        }
                    }
                }
                var win = new AddAppointmentWindow(_requestId, _currentUserId);
                if (win.ShowDialog() == true)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания наряда: " + ex.Message);
            }
        }
    }
}