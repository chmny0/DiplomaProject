using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WpfPlannerApp
{
    public partial class MainWindow : Window
    {
        private DateTime _month = DateTime.Today;
        private readonly string _role;
        private bool _sidebarVisible = false;

        public MainWindow(string role, string userName)
        {
            InitializeComponent();

            _role = role;
            UserNameText.Text = userName;

            AddWorkerButton.Visibility =
                role == "admin"
                ? Visibility.Visible
                : Visibility.Collapsed;

            DrawCalendar();
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarVisible = !_sidebarVisible;

            if (_sidebarVisible)
            {
                // Показать боковую панель с анимацией
                SidebarColumn.Width = new GridLength(260);
                Sidebar.Visibility = Visibility.Visible;

                // Анимация появления
                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 260,
                    Duration = TimeSpan.FromSeconds(0.2)
                };
                Sidebar.BeginAnimation(Border.WidthProperty, animation);
            }
            else
            {
                // Скрыть боковую панель с анимацией
                var animation = new DoubleAnimation
                {
                    From = 260,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.2)
                };
                animation.Completed += (s, args) =>
                {
                    SidebarColumn.Width = new GridLength(0);
                    Sidebar.Visibility = Visibility.Collapsed;
                };
                Sidebar.BeginAnimation(Border.WidthProperty, animation);
            }
        }

        private void AddAppointment_Click(object sender, RoutedEventArgs e)
        {
            new Windows.AddAppointmentWindow(DateTime.Today).ShowDialog();
            DrawCalendar();
        }

        private void AddWorker_Click(object sender, RoutedEventArgs e)
        {
            new Windows.AdminWorkersWindow().ShowDialog();
        }   

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            new Windows.LoginWindow().Show();
            Close();
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _month = _month.AddMonths(-1);
            DrawCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _month = _month.AddMonths(1);
            DrawCalendar();
        }

        private void DrawCalendar()
        {
            MonthText.Text = _month.ToString("MMMM yyyy");
            CalendarGrid.Children.Clear();

            foreach (var d in Services.CalendarUtils.GenerateMonth(_month))
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = d.Day,
                    Tag = d,
                    Margin = new Thickness(3),
                    Opacity = d.Month == _month.Month ? 1 : 0.35,
                    IsEnabled = d.Month == _month.Month
                };

                btn.Click += (_, __) =>
                    new Windows.ViewDayWindow(d).ShowDialog();

                CalendarGrid.Children.Add(btn);
            }
        }
    }
}