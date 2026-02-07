using System.Collections.ObjectModel;
using System.Windows;
using WpfPlannerApp.Helpers;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class AdminWorkersWindow : Window
    {
        private ObservableCollection<WorkerRow> _list = new();
        private WorkerRow _current;

        public AdminWorkersWindow()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            _list.Clear();

            var rows = Db.Query(@"
                SELECT u.user_id, u.username, u.full_name, u.role, u.is_active
                FROM users u
                WHERE u.role = 'worker'
                ORDER BY u.full_name
            ");

            foreach (var r in rows)
            {
                _list.Add(new WorkerRow
                {
                    UserId = (int)r["user_id"],
                    Username = r["username"].ToString(),
                    FullName = r["full_name"].ToString(),
                    Role = r["role"].ToString(),
                    IsActive = (bool)r["is_active"]
                });
            }

            WorkersGrid.ItemsSource = _list;
        }

        private void WorkersGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _current = WorkersGrid.SelectedItem as WorkerRow;
            if (_current == null) return;

            LoginBox.Text = _current.Username;
            NameBox.Text = _current.FullName;
            ActiveBox.IsChecked = _current.IsActive;
            PasswordBox.Password = "";
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            _current = null;
            LoginBox.Text = "";
            NameBox.Text = "";
            PasswordBox.Password = "";
            ActiveBox.IsChecked = true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                // INSERT
                var hash = PasswordHelper.Hash(PasswordBox.Password);

                int id = Db.ExecuteScalarInt(@"
                    INSERT INTO users(username,password_hash,role,full_name,is_active)
                    VALUES(@u,@p,'worker',@f,@a)
                    RETURNING user_id
                ", new()
                {
                    {"u", LoginBox.Text},
                    {"p", hash},
                    {"f", NameBox.Text},
                    {"a", ActiveBox.IsChecked == true}
                });

                Db.Execute("INSERT INTO workers(user_id) VALUES(@u)", new() { { "u", id } });
            }
            else
            {

                Db.Execute(@"
                    UPDATE users
                    SET username=@u, full_name=@f, is_active=@a
                    WHERE user_id=@id
                ", new()
                {
                    {"id", _current.UserId},
                    {"u", LoginBox.Text},
                    {"f", NameBox.Text},
                    {"a", ActiveBox.IsChecked == true}
                });

                if (!string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    Db.Execute("UPDATE users SET password_hash=@p WHERE user_id=@id",
                        new() {
                            {"id", _current.UserId},
                            {"p", PasswordHelper.Hash(PasswordBox.Password)}
                        });
                }
            }

            Load();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null) return;

            if (MessageBox.Show("Удалить сотрудника?", "Подтверждение",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Db.Execute("DELETE FROM workers WHERE user_id=@id", new() { { "id", _current.UserId } });
                Db.Execute("DELETE FROM users WHERE user_id=@id", new() { { "id", _current.UserId } });

                Load();
            }
        }
    }

    public class WorkerRow
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
    }
}
