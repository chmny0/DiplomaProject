using System;
using System.Windows;
using Npgsql;
using WpfPlannerApp.Helpers;

namespace WpfPlannerApp.Windows
{
    public partial class LoginWindow : Window
    {
        private readonly string _conn =
            "Host=localhost;Port=5432;Database=planner_db;Username=postgres;Password=1234";

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var login = LoginBox.Text.Trim();
            var pass = PasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                MessageBox.Show("Введите логин и пароль");
                return;
            }

            using var conn = new NpgsqlConnection(_conn);
            conn.Open();

            var sql = @"
                SELECT user_id, password_hash, role, full_name
                FROM users
                WHERE username = @u AND is_active = TRUE
            ";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("u", login);

            using var r = cmd.ExecuteReader();

            if (!r.Read())
            {
                MessageBox.Show("Пользователь не найден");
                return;
            }

            if (!PasswordHelper.Verify(pass, r["password_hash"].ToString()))
            {
                MessageBox.Show("Неверный пароль");
                return;
            }

            int userId = Convert.ToInt32(r["user_id"]);
            string role = r["role"].ToString();
            string name = r["full_name"].ToString();

            if (role == "worker")
                new WorkerWindow(userId, name).Show();
            else
                new MainWindow(role, name).Show();

            Close();
        }
    }
}
