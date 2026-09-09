using System;
using System.Windows;
using System.Windows.Controls;
using WpfPlannerApp.Services;

namespace WpfPlannerApp.Windows
{
    public partial class CalendarView : UserControl
    {
        private DateTime _month = DateTime.Today;
        private readonly int _currentUserId;
        private readonly string _role;

        public CalendarView(int currentUserId, string role)
        {
            InitializeComponent();
            _currentUserId = currentUserId;
            _role = role;
            DrawCalendar();
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

            foreach (var d in CalendarUtils.GenerateMonth(_month))
            {
                var btn = new Button
                {
                    Content = d.Day,
                    Tag = d,
                    Margin = new Thickness(3),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Opacity = d.Month == _month.Month ? 1 : 0.35,
                    IsEnabled = d.Month == _month.Month
                };

                btn.Click += (_, __) =>
                {
                    new ViewDayWindow(d, _currentUserId, _role).ShowDialog();
                };

                CalendarGrid.Children.Add(btn);
            }
        }
    }
}