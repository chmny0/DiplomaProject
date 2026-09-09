using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Helpers;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class CreateRequestWindow : Window
    {
        private string _currentClientType = "individual";

        public CreateRequestWindow()
        {
            InitializeComponent();
            LoadWorkTypes();
            SetupMasks();
            ClientTypeTabs.SelectedIndex = 0;
        }

        private void SetupMasks()
        {
            MaskedInputHelper.PhoneMask(IndPhoneBox);
            MaskedInputHelper.PhoneMask(LegPhoneBox);
            MaskedInputHelper.PassportMask(IndPassportBox);
            MaskedInputHelper.InnMask(LegInnBox, isLegal: true);
            MaskedInputHelper.KppMask(LegKppBox);
        }

        private void LoadWorkTypes()
        {
            var types = Db.Query(@"
                SELECT type_id, type_name
                FROM work_types
                WHERE is_active = TRUE
                ORDER BY type_name
            ");

            WorkTypeBox.ItemsSource = types.Select(t => new
            {
                type_id = Convert.ToInt32(t["type_id"]),
                type_name = t["type_name"]?.ToString() ?? ""
            }).ToList();
        }

        private void ClientTypeTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentClientType = ClientTypeTabs.SelectedIndex == 0 ? "individual" : "legal";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateForm()) return;

                string phone = MaskedInputHelper.CleanPhone(
                    _currentClientType == "individual" ? IndPhoneBox.Text : LegPhoneBox.Text
                );

                int clientId = FindOrCreateClient(phone);

                int statusId = Db.ExecuteScalarInt(
                    "SELECT status_id FROM request_statuses WHERE status_name = 'new'",
                    new());

                Db.Execute(@"
                    INSERT INTO requests
                    (client_id, work_type_id, preferred_date, preferred_time, description, status_id, created_at)
                    VALUES
                    (@client, @type, @pdate, @ptime, @desc, @status, NOW())
                ", new()
                {
                    ["client"] = clientId,
                    ["type"] = Convert.ToInt32(WorkTypeBox.SelectedValue),
                    ["pdate"] = DateBox.SelectedDate,
                    ["ptime"] = TimeSpan.TryParse(TimeBox.Text, out var t) ? t : (object)DBNull.Value,
                    ["desc"] = DescriptionBox.Text ?? "",
                    ["status"] = statusId
                });

                MessageBox.Show(_currentClientType == "legal"
                    ? "Заявка отправлена! Менеджер свяжется с вами для оформления договора."
                    : "Заявка отправлена! Ожидайте звонка.",
                    "Успешно");

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении:\n" + ex.Message, "Ошибка");
            }
        }

        private bool ValidateForm()
        {
            if (WorkTypeBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите тип услуги");
                return false;
            }

            if (!DateBox.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите предпочтительную дату");
                return false;
            }

            return _currentClientType == "individual" ? ValidateIndividual() : ValidateLegal();
        }

        private bool ValidateIndividual()
        {
            if (string.IsNullOrWhiteSpace(IndLastNameBox.Text))
            { MessageBox.Show("Введите фамилию"); return false; }

            if (string.IsNullOrWhiteSpace(IndFirstNameBox.Text))
            { MessageBox.Show("Введите имя"); return false; }

            if (string.IsNullOrWhiteSpace(IndPhoneBox.Text) || IndPhoneBox.Text.Length < 16)
            { MessageBox.Show("Введите полный номер телефона"); return false; }

            if (string.IsNullOrWhiteSpace(IndAddressBox.Text))
            { MessageBox.Show("Введите адрес"); return false; }

            if (!string.IsNullOrWhiteSpace(IndEmailBox.Text) && !MaskedInputHelper.IsValidEmail(IndEmailBox.Text))
            { MessageBox.Show("Некорректный email"); return false; }

            return true;
        }

        private bool ValidateLegal()
        {
            if (string.IsNullOrWhiteSpace(LegCompanyBox.Text))
            { MessageBox.Show("Введите название компании"); return false; }

            if (string.IsNullOrWhiteSpace(LegInnBox.Text) || LegInnBox.Text.Length != 10)
            { MessageBox.Show("ИНН должен содержать 10 цифр"); return false; }

            if (!string.IsNullOrWhiteSpace(LegKppBox.Text) && LegKppBox.Text.Length != 9)
            { MessageBox.Show("КПП должен содержать 9 цифр"); return false; }

            if (string.IsNullOrWhiteSpace(LegLastNameBox.Text))
            { MessageBox.Show("Введите фамилию контактного лица"); return false; }

            if (string.IsNullOrWhiteSpace(LegFirstNameBox.Text))
            { MessageBox.Show("Введите имя контактного лица"); return false; }

            if (string.IsNullOrWhiteSpace(LegPhoneBox.Text) || LegPhoneBox.Text.Length < 16)
            { MessageBox.Show("Введите полный номер телефона"); return false; }

            if (string.IsNullOrWhiteSpace(LegAddressBox.Text))
            { MessageBox.Show("Введите адрес для выезда"); return false; }

            if (!string.IsNullOrWhiteSpace(LegEmailBox.Text) && !MaskedInputHelper.IsValidEmail(LegEmailBox.Text))
            { MessageBox.Show("Некорректный email"); return false; }

            return true;
        }

        private int FindOrCreateClient(string phone)
        {
            var existing = Db.Query(@"
                SELECT client_id, client_type_id FROM clients
                WHERE phone = @phone
                LIMIT 1
            ", new() { ["phone"] = phone });

            int clientTypeId = Db.ExecuteScalarInt(
                "SELECT type_id FROM client_types WHERE type_name = @name",
                new() { ["name"] = _currentClientType });

            if (existing.Count > 0)
            {
                int id = Convert.ToInt32(existing[0]["client_id"]);
                UpdateClient(id);
                return id;
            }
            return InsertClient(clientTypeId, phone);
        }

        private int InsertClient(int clientTypeId, string phone)
        {
            if (_currentClientType == "individual")
            {
                return Db.ExecuteScalarInt(@"
                    INSERT INTO clients
                    (client_type_id, last_name, first_name, phone, email, address, passport_data, is_active, created_at)
                    VALUES
                    (@type, @lastName, @firstName, @phone, @email, @addr, @passport, TRUE, NOW())
                    RETURNING client_id
                ", new()
                {
                    ["type"] = clientTypeId,
                    ["lastName"] = IndLastNameBox.Text.Trim(),
                    ["firstName"] = (IndFirstNameBox.Text.Trim() + " " + IndMiddleNameBox.Text.Trim()).Trim(),
                    ["phone"] = phone,
                    ["email"] = NullIfEmpty(IndEmailBox.Text),
                    ["addr"] = IndAddressBox.Text.Trim(),
                    ["passport"] = NullIfEmpty(IndPassportBox.Text)
                });
            }
            else
            {
                return Db.ExecuteScalarInt(@"
                    INSERT INTO clients
                    (client_type_id, company_name, inn, kpp, legal_address, director_name,
                     last_name, first_name, phone, email, address, is_active, created_at)
                    VALUES
                    (@type, @company, @inn, @kpp, @legalAddr, @director,
                     @lastName, @firstName, @phone, @email, @addr, TRUE, NOW())
                    RETURNING client_id
                ", new()
                {
                    ["type"] = clientTypeId,
                    ["company"] = LegCompanyBox.Text.Trim(),
                    ["inn"] = LegInnBox.Text.Trim(),
                    ["kpp"] = NullIfEmpty(LegKppBox.Text),
                    ["legalAddr"] = NullIfEmpty(LegLegalAddressBox.Text),
                    ["director"] = NullIfEmpty(LegDirectorBox.Text),
                    ["lastName"] = LegLastNameBox.Text.Trim(),
                    ["firstName"] = (LegFirstNameBox.Text.Trim() + " " + LegMiddleNameBox.Text.Trim()).Trim(),
                    ["phone"] = phone,
                    ["email"] = NullIfEmpty(LegEmailBox.Text),
                    ["addr"] = LegAddressBox.Text.Trim()
                });
            }
        }

        private void UpdateClient(int clientId)
        {
            if (_currentClientType == "individual")
            {
                Db.Execute(@"
                    UPDATE clients SET
                        last_name = @lastName,
                        first_name = @firstName,
                        email = @email,
                        address = @addr,
                        passport_data = @passport
                    WHERE client_id = @id
                ", new()
                {
                    ["id"] = clientId,
                    ["lastName"] = IndLastNameBox.Text.Trim(),
                    ["firstName"] = (IndFirstNameBox.Text.Trim() + " " + IndMiddleNameBox.Text.Trim()).Trim(),
                    ["email"] = NullIfEmpty(IndEmailBox.Text),
                    ["addr"] = IndAddressBox.Text.Trim(),
                    ["passport"] = NullIfEmpty(IndPassportBox.Text)
                });
            }
            else
            {
                Db.Execute(@"
                    UPDATE clients SET
                        company_name = @company,
                        inn = @inn,
                        kpp = @kpp,
                        legal_address = @legalAddr,
                        director_name = @director,
                        last_name = @lastName,
                        first_name = @firstName,
                        email = @email,
                        address = @addr
                    WHERE client_id = @id
                ", new()
                {
                    ["id"] = clientId,
                    ["company"] = LegCompanyBox.Text.Trim(),
                    ["inn"] = LegInnBox.Text.Trim(),
                    ["kpp"] = NullIfEmpty(LegKppBox.Text),
                    ["legalAddr"] = NullIfEmpty(LegLegalAddressBox.Text),
                    ["director"] = NullIfEmpty(LegDirectorBox.Text),
                    ["lastName"] = LegLastNameBox.Text.Trim(),
                    ["firstName"] = (LegFirstNameBox.Text.Trim() + " " + LegMiddleNameBox.Text.Trim()).Trim(),
                    ["email"] = NullIfEmpty(LegEmailBox.Text),
                    ["addr"] = LegAddressBox.Text.Trim()
                });
            }
        }

        private object? NullIfEmpty(string? s)
        {
            return string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
        }
    }
}