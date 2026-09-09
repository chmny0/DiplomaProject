using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfPlannerApp.Services;
using WpfPlannerApp.Models;
using ClosedXML.Excel;

namespace WpfPlannerApp.Windows
{
    public partial class JournalView : UserControl
    {
        private readonly string _role;
        private readonly int _userId;
        private string _currentTab = "requests";

        public JournalView(string role, int userId)
        {
            InitializeComponent();
            _role = role;
            _userId = userId;
            JournalTabs.SelectedIndex = 0;
            LoadTab("requests");
        }

        private void JournalTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string tab = JournalTabs.SelectedIndex switch
            {
                0 => "requests",
                1 => "appointments",
                2 => "documents",
                _ => "requests"
            };
            LoadTab(tab);
        }

        private void LoadTab(string tab)
        {
            _currentTab = tab;
            FiltersPanel.Visibility = (tab == "requests" || tab == "appointments")
                ? Visibility.Visible
                : Visibility.Collapsed;
            LoadFilters();
            LoadData();
        }

        private void LoadFilters()
        {
            StatusFilter.Items.Clear();
            StatusFilter.Items.Add("Все");
            WorkTypeFilter.Items.Clear();
            WorkTypeFilter.Items.Add("Все");
            ClientTypeFilter.Items.Clear();
            ClientTypeFilter.Items.Add("Все");

            if (_currentTab == "requests")
            {
                foreach (var s in Db.Query("SELECT status_name FROM request_statuses"))
                    StatusFilter.Items.Add(s["status_name"]?.ToString() ?? "");
                foreach (var t in Db.Query("SELECT type_name FROM work_types WHERE is_active = TRUE"))
                    WorkTypeFilter.Items.Add(t["type_name"].ToString());
                ClientTypeFilter.Items.Add("individual");
                ClientTypeFilter.Items.Add("legal");
            }
            else if (_currentTab == "appointments")
            {
                foreach (var s in Db.Query("SELECT status_name FROM appointment_statuses"))
                    StatusFilter.Items.Add(s["status_name"]?.ToString() ?? "");
                foreach (var t in Db.Query("SELECT type_name FROM work_types WHERE is_active = TRUE"))
                    WorkTypeFilter.Items.Add(t["type_name"].ToString());
                ClientTypeFilter.Items.Add("individual");
                ClientTypeFilter.Items.Add("legal");
            }

            StatusFilter.SelectedIndex = 0;
            WorkTypeFilter.SelectedIndex = 0;
            ClientTypeFilter.SelectedIndex = 0;
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (IsLoaded) LoadData();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            StatusFilter.SelectedIndex = 0;
            WorkTypeFilter.SelectedIndex = 0;
            ClientTypeFilter.SelectedIndex = 0;
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            LoadData();
        }

        private void LoadData()
        {
            var items = _currentTab switch
            {
                "requests" => LoadRequests(),
                "appointments" => LoadAppointments(),
                "documents" => LoadDocuments(),
                _ => new List<JournalItem>()
            };
            CardsList.ItemsSource = items;
        }

        private List<JournalItem> LoadRequests()
        {
            var sql = @"
                SELECT r.request_id, r.created_at, rs.status_name,
                       wt.type_name,
                       COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name
                FROM requests r
                JOIN clients c ON c.client_id = r.client_id
                JOIN client_types ct ON ct.type_id = c.client_type_id
                LEFT JOIN request_statuses rs ON rs.status_id = r.status_id
                LEFT JOIN work_types wt ON wt.type_id = r.work_type_id
                WHERE 1=1
            ";
            var param = new Dictionary<string, object?>();

            if (StatusFilter.SelectedIndex > 0)
            { sql += " AND rs.status_name = @status"; param["status"] = StatusFilter.SelectedItem.ToString(); }
            if (WorkTypeFilter.SelectedIndex > 0)
            { sql += " AND wt.type_name = @wt"; param["wt"] = WorkTypeFilter.SelectedItem.ToString(); }
            if (ClientTypeFilter.SelectedIndex > 0)
            { sql += " AND ct.type_name = @ctype"; param["ctype"] = ClientTypeFilter.SelectedItem.ToString(); }
            if (DateFrom.SelectedDate.HasValue)
            { sql += " AND r.created_at::date >= @from"; param["from"] = DateFrom.SelectedDate.Value; }
            if (DateTo.SelectedDate.HasValue)
            { sql += " AND r.created_at::date <= @to"; param["to"] = DateTo.SelectedDate.Value; }

            sql += " ORDER BY r.created_at DESC";

            return Db.Query(sql, param).Select(r => new JournalItem
            {
                Id = Convert.ToInt32(r["request_id"]),
                Title = r["client_name"]?.ToString() ?? "—",
                Subtitle = r["type_name"]?.ToString() ?? "—",
                Date = FormatDate(r["created_at"]),
                Status = MapStatus(r["status_name"]?.ToString(), "request"),
                StatusColor = GetStatusColor(r["status_name"]?.ToString(), "request"),
                ItemType = "request"
            }).ToList();
        }

        private List<JournalItem> LoadAppointments()
        {
            var sql = @"
                SELECT a.appointment_id, a.appointment_date, a.address,
                       s.status_name, wt.type_name,
                       COALESCE(c.first_name || ' ' || c.last_name, c.company_name) AS client_name
                FROM appointments a
                JOIN clients c ON c.client_id = a.client_id
                JOIN client_types ct ON ct.type_id = c.client_type_id
                JOIN appointment_statuses s ON s.status_id = a.status_id
                LEFT JOIN work_types wt ON wt.type_id = a.work_type_id
                WHERE 1=1
            ";
            var param = new Dictionary<string, object?>();

            if (StatusFilter.SelectedIndex > 0)
            { sql += " AND s.status_name = @status"; param["status"] = StatusFilter.SelectedItem.ToString(); }
            if (WorkTypeFilter.SelectedIndex > 0)
            { sql += " AND wt.type_name = @wt"; param["wt"] = WorkTypeFilter.SelectedItem.ToString(); }
            if (ClientTypeFilter.SelectedIndex > 0)
            { sql += " AND ct.type_name = @ctype"; param["ctype"] = ClientTypeFilter.SelectedItem.ToString(); }
            if (DateFrom.SelectedDate.HasValue)
            { sql += " AND a.appointment_date >= @from"; param["from"] = DateFrom.SelectedDate.Value; }
            if (DateTo.SelectedDate.HasValue)
            { sql += " AND a.appointment_date <= @to"; param["to"] = DateTo.SelectedDate.Value; }

            sql += " ORDER BY a.appointment_date DESC";

            return Db.Query(sql, param).Select(r => new JournalItem
            {
                Id = Convert.ToInt32(r["appointment_id"]),
                Title = r["client_name"]?.ToString() ?? "—",
                Subtitle = $"Адрес: {r["address"]} — {r["type_name"]}",
                Date = FormatDate(r["appointment_date"]),
                Status = MapStatus(r["status_name"]?.ToString(), "appointment"),
                StatusColor = GetStatusColor(r["status_name"]?.ToString(), "appointment"),
                ItemType = "appointment"
            }).ToList();
        }

        private List<JournalItem> LoadDocuments()
        {
            var sql = @"
                SELECT d.document_id, d.document_number, d.document_date, d.file_path,
                       dt.type_name,
                       COALESCE(cl.first_name || ' ' || cl.last_name, cl.company_name) AS client_name
                FROM documents d
                JOIN document_types dt ON dt.type_id = d.document_type_id
                LEFT JOIN appointments a ON a.appointment_id = d.appointment_id
                LEFT JOIN clients cl ON cl.client_id = a.client_id
                ORDER BY d.document_date DESC
            ";

            return Db.Query(sql).Select(r => new JournalItem
            {
                Id = Convert.ToInt32(r["document_id"]),
                Title = $"{r["type_name"]} №{r["document_number"]}",
                Subtitle = r["client_name"]?.ToString() ?? "—",
                Date = FormatDate(r["document_date"]),
                Status = string.IsNullOrEmpty(r["file_path"]?.ToString()) ? "Без файла" : "Файл прикреплён",
                ItemType = "document"
            }).ToList();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CardsList.ItemsSource is not IEnumerable<JournalItem> items || !items.Any())
                {
                    MessageBox.Show("Нет данных для выгрузки. Примените фильтры и нажмите «Показать».",
                        "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var itemsList = items.ToList();
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"{_currentTab}_отчёт_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };
                if (dialog.ShowDialog() != true) return;
                using var wb = new XLWorkbook();
                var summarySheet = wb.Worksheets.Add("Сводка");
                BuildSummarySheet(summarySheet, itemsList);
                var dataSheet = wb.Worksheets.Add("Данные");
                BuildDataSheet(dataSheet, itemsList);
                wb.SaveAs(dialog.FileName);
                MessageBox.Show(
                    $"Отчёт сформирован!\n\n" +
                    $"Лист «Сводка» — аналитика по выгрузке\n" +
                    $"Лист «Данные» — {itemsList.Count} записей\n\n" +
                    $"Файл сохранён:\n{dialog.FileName}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка экспорта:\n" + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildSummarySheet(IXLWorksheet ws, List<JournalItem> items)
        {
            ws.Cell(1, 1).Value = "СВОДНЫЙ ОТЧЁТ ПО ЗАЯВКАМ КОМПАНИИ";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range("A1:D1").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 3;
            ws.Cell(row, 1).Value = "Раздел:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = _currentTab switch
            {
                "requests" => "Заявки",
                "appointments" => "Наряды (выезды)",
                "documents" => "Документы",
                _ => _currentTab
            };
            row++;

            ws.Cell(row, 1).Value = "Дата формирования:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            row++;

            string period = "весь период";
            if (DateFrom.SelectedDate.HasValue || DateTo.SelectedDate.HasValue)
            {
                string from = DateFrom.SelectedDate?.ToString("dd.MM.yyyy") ?? "...";
                string to = DateTo.SelectedDate?.ToString("dd.MM.yyyy") ?? "...";
                period = $"с {from} по {to}";
            }
            ws.Cell(row, 1).Value = "Период:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = period;
            row += 2;

            ws.Cell(row, 1).Value = "ВСЕГО ЗАПИСЕЙ:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 13;
            ws.Cell(row, 2).Value = items.Count;
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontSize = 14;
            row += 2;

            var statusGroups = items
                .GroupBy(i => i.Status)
                .OrderByDescending(g => g.Count())
                .ToList();

            ws.Cell(row, 1).Value = "ДЕТАЛИЗАЦИЯ ПО СТАТУСАМ:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 13;
            ws.Range(row, 1, row, 4).Merge();
            row++;

            var headerRow = ws.Row(row);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Cell(row, 1).Value = "Дата";
            ws.Cell(row, 2).Value = "Клиент / Документ";
            ws.Cell(row, 3).Value = "Детали";
            ws.Cell(row, 4).Value = "Статус";
            row++;

            foreach (var group in statusGroups)
            {
                ws.Cell(row, 1).Value = group.Key.ToUpper();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 12;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
                ws.Range(row, 1, row, 4).Merge();
                row++;

                foreach (var item in group)
                {
                    ws.Cell(row, 1).Value = item.Date;
                    ws.Cell(row, 2).Value = item.Title;
                    ws.Cell(row, 3).Value = item.Subtitle;
                    ws.Cell(row, 4).Value = item.Status;
                    row++;
                }

                ws.Cell(row, 1).Value = $"ИТОГО: {group.Count()}";
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.Italic = true;
                ws.Range(row, 1, row, 4).Merge();
                ws.Range(row, 1, row, 4).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                row += 2;
            }

            ws.Cell(row, 1).Value = $"ВСЕГО ЗАПИСЕЙ: {items.Count}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Range(row, 1, row, 4).Merge();
            ws.Range(row, 1, row, 4).Style.Border.TopBorder = XLBorderStyleValues.Medium;

            ws.Columns().AdjustToContents();
            ws.Column(1).Width = 18;
            ws.Column(2).Width = 35;
            ws.Column(3).Width = 40;
            ws.Column(4).Width = 20;
        }

        private void BuildDataSheet(IXLWorksheet ws, List<JournalItem> items)
        {
            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            ws.Cell(1, 1).Value = "ID";
            ws.Cell(1, 2).Value = "Клиент / Документ";
            ws.Cell(1, 3).Value = "Детали";
            ws.Cell(1, 4).Value = "Дата";
            ws.Cell(1, 5).Value = "Статус";

            int row = 2;
            foreach (var item in items)
            {
                ws.Cell(row, 1).Value = item.Id;
                ws.Cell(row, 2).Value = item.Title;
                ws.Cell(row, 3).Value = item.Subtitle;
                ws.Cell(row, 4).Value = item.Date;
                ws.Cell(row, 5).Value = item.Status;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not JournalItem item) return;

            try
            {
                switch (item.ItemType)
                {
                    case "request": OpenRequest(item.Id); break;
                    case "appointment": new EditAppointmentWindow(item.Id, _role, _userId).ShowDialog(); break;
                    case "document": OpenDocumentFile(item.Id); break;
                }
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void OpenRequest(int requestId)
        {
            var request = Db.Query(@"
                SELECT rs.status_name 
                FROM requests r
                JOIN request_statuses rs ON rs.status_id = r.status_id
                WHERE r.request_id = @id
            ", new() { ["id"] = requestId }).FirstOrDefault();

            if (request == null)
            {
                MessageBox.Show("Заявка не найдена");
                return;
            }

            string status = request["status_name"]?.ToString() ?? "";

            var app = Db.Query(@"
                SELECT appointment_id FROM appointments WHERE request_id = @id LIMIT 1
            ", new() { ["id"] = requestId }).FirstOrDefault();

            if (app != null)
            {
                new EditAppointmentWindow(Convert.ToInt32(app["appointment_id"]), _role, _userId).ShowDialog();
            }
            else if (status == "new")
            {
                new AddAppointmentWindow(requestId, _userId).ShowDialog();
            }
            else
            {
                MessageBox.Show(
                    $"Заявка №{requestId} имеет статус «{status}», но наряд не найден.\n" +
                    "Обратитесь к администратору для проверки базы данных.",
                    "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenDocumentFile(int documentId)
        {
            var doc = Db.Query(@"
                SELECT file_path, document_number FROM documents WHERE document_id = @id
            ", new() { ["id"] = documentId }).FirstOrDefault();

            if (doc == null) return;

            string? path = doc["file_path"]?.ToString();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show($"Документ №{doc["document_number"]}\nФайл не прикреплён.",
                    "Документ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            FileService.OpenFile(path);
        }

        private string FormatDate(object? value) => value switch
        {
            DateTime dt => dt.ToString("dd.MM.yyyy"),
            DateOnly d => d.ToString("dd.MM.yyyy"),
            _ => value?.ToString() ?? "—"
        };

        private string MapStatus(string? status, string type)
        {
            if (type == "request")
            {
                return status switch
                {
                    "new" => "Новая",
                    "in_progress" => "В работе",
                    "appointment_created" => "Запланирован выезд",
                    "completed" => "Завершена",
                    "cancelled" => "Отменена",
                    _ => status ?? ""
                };
            }
            else
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
        }

        private string GetStatusColor(string? statusName, string itemType)
        {
            if (itemType == "request")
            {
                return statusName switch
                {
                    "new" => "#2196F3",
                    "in_progress" => "#FF9800",
                    "appointment_created" => "#FF5722",
                    "completed" => "#4CAF50",
                    "cancelled" => "#F44336",
                    _ => "#666666"
                };
            }
            else
            {
                return statusName switch
                {
                    "scheduled" => "#9C27B0",
                    "in_progress" => "#FF9800",
                    "completed" => "#4CAF50",
                    "cancelled" => "#F44336",
                    _ => "#666666"
                };
            }
        }
    }
}