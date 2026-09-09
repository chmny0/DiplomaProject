using System;
using System.Windows;
using Npgsql;
using WpfPlannerApp.Helpers;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DebugHelper.Log("Открыто окно авторизации");
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = LoginBox.Text?.Trim() ?? "";
                string pass = PasswordBox.Password?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
                {
                    DebugHelper.Show("Введите логин и пароль");
                    return;
                }
                using var conn = Db.GetConnection();
                conn.Open();

                var sql = @"
                    SELECT u.user_id,u.password_hash,u.full_name,r.role_name,es.status_name
                    FROM users u JOIN roles r ON r.role_id = u.role_id JOIN employee_statuses es ON es.status_id = u.status_id
                    WHERE u.username = @u LIMIT 1";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("u", login);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    DebugHelper.Show("Пользователь не найден");
                    return;
                }
                int userId = Convert.ToInt32(reader["user_id"]);
                string hash = reader["password_hash"]?.ToString() ?? "";
                string role = reader["role_name"]?.ToString()?.Trim().ToLowerInvariant() ?? "";
                string name = reader["full_name"]?.ToString() ?? "Пользователь";
                string status = reader["status_name"]?.ToString()?.Trim().ToLowerInvariant() ?? "";
                if (status != "working")
                {
                    DebugHelper.Show("Пользователь не активен (статус: " + status + ")");
                    return;
                }
                if (string.IsNullOrWhiteSpace(hash))
                {
                    DebugHelper.Show("Ошибка: пароль отсутствует");
                    return;
                }
                if (!PasswordHelper.Verify(pass, hash))
                {
                    DebugHelper.Show("Неверный пароль");
                    return;
                }
                if (string.IsNullOrWhiteSpace(role))
                {
                    DebugHelper.Show("Ошибка: роль не назначена");
                    return;
                }
                DebugHelper.Log($"LOGIN OK | {login} | role={role}");
                Window nextWindow = role switch
                {
                    "worker" => new WorkerWindow(userId, name),
                    "admin" => new DashboardWindow(role, name, userId),
                    "manager" => new DashboardWindow(role, name, userId),
                    _ => new DashboardWindow(role, name, userId)
                };

                nextWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "LOGIN ERROR");
                DebugHelper.LogError(ex);
            }
        }

        private void CreateRequest_Click(object sender, RoutedEventArgs e)
        {
            new CreateRequestWindow().ShowDialog();
        }
    }
}