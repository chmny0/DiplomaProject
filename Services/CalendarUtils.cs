using System;
using System.Collections.Generic;

namespace WpfPlannerApp.Services
{
    public static class CalendarUtils
    {
        public static List<DateTime> GenerateMonth(DateTime month)
        {
            var days = new List<DateTime>();

            var first = new DateTime(month.Year, month.Month, 1);

            // Понедельник = 0
            int shift = ((int)first.DayOfWeek + 6) % 7;

            var start = first.AddDays(-shift);

            for (int i = 0; i < 42; i++)
                days.Add(start.AddDays(i));

            return days;
        }
    }
}
