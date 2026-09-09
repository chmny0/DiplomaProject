using System;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Windows;

namespace WpfPlannerApp
{
    public partial class DashboardWindow : Window
    {
        private readonly string _role;
        private readonly int _currentUserId;
        private readonly string _userName;
        private Button? _activeMenuButton;

        public DashboardWindow(string role, string userName, int currentUserId)
        {
            InitializeComponent();
            _role = role;
            _currentUserId = currentUserId;
            _userName = userName;

            UserNameText.Text = $"{userName}\n({GetRoleDisplay(role)})";

            bool isAdmin = (role == "admin");
            BtnWorkers.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            BtnServices.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            _activeMenuButton = BtnRequests;
            ShowRequests();
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button clickedButton) return;

            if (_activeMenuButton != null)
                _activeMenuButton.Style = (Style)FindResource("MenuButtonStyle");

            clickedButton.Style = (Style)FindResource("ActiveMenuButtonStyle");
            _activeMenuButton = clickedButton;

            if (clickedButton == BtnRequests)
                ShowRequests();
            else if (clickedButton == BtnCalendar)
                ShowCalendar();
            else if (clickedButton == BtnWorkers)
                ShowWorkers();
            else if (clickedButton == BtnServices)
                ShowServices();
            else if (clickedButton == BtnJournals)
                ShowJournals();
        }

        private void ShowJournals()
        {
            ContentArea.Content = new JournalView(_role, _currentUserId);
        }

        private void ShowRequests()
        {
            ContentArea.Content = new RequestsView(_role, _currentUserId);
        }

        private void ShowCalendar()
        {
            ContentArea.Content = new CalendarView(_currentUserId, _role);
        }

        private void ShowWorkers()
        {
            new AdminWorkersWindow().ShowDialog();
        }

        private void ShowServices()
        {
            new WorkTypesWindow().ShowDialog();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            Close();
        }

        private string GetRoleDisplay(string role) => role switch
        {
            "admin" => "Администратор",
            "manager" => "Менеджер",
            "worker" => "Работник",
            _ => role
        };
    }
}