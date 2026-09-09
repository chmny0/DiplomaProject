using Npgsql;
using System;
using System.Collections.Generic;

namespace WpfPlannerApp.Services
{
    public static class Db
    {
        private static readonly string _conn =
            "Host=;Port=;Database=;Username=;Password=";

        public static NpgsqlConnection GetConnection()
            => new NpgsqlConnection(_conn);

        public static void Execute(string sql, Dictionary<string, object?> p)
        {
            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            AddParams(cmd, p);

            cmd.ExecuteNonQuery();
        }

        public static int ExecuteScalarInt(string sql, Dictionary<string, object?> p)
        {
            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            AddParams(cmd, p);

            var result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                return 0;

            return Convert.ToInt32(result);
        }

        public static List<Dictionary<string, object?>> Query(
            string sql,
            Dictionary<string, object?>? p = null)
        {
            var result = new List<Dictionary<string, object?>>();

            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);

            if (p != null)
                AddParams(cmd, p);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object val = reader.GetValue(i);

                    if (val is DateOnly dateOnly)
                    {
                        val = dateOnly.ToDateTime(TimeOnly.MinValue);
                    }

                    row[reader.GetName(i)] =
                        val == DBNull.Value ? null : val;
                }

                result.Add(row);
            }

            return result;
        }

        private static void AddParams(NpgsqlCommand cmd, Dictionary<string, object?> p)
        {
            foreach (var kv in p)
            {
                cmd.Parameters.AddWithValue(
                    kv.Key,
                    kv.Value ?? DBNull.Value
                );
            }
        }

        public static T? Get<T>(object? value)
        {
            if (value == null || value == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static DateTime? GetDate(object? value)
        {
            if (value is DateTime dt) return dt;
            if (value is DateOnly d) return d.ToDateTime(TimeOnly.MinValue);
            if (value is null || value == DBNull.Value) return null;

            return DateTime.Parse(value.ToString()!);
        }

        public static TimeSpan? GetTime(object? value)
        {
            if (value is TimeSpan ts) return ts;
            if (value is DateTime dt) return dt.TimeOfDay;
            if (value is null || value == DBNull.Value) return null;

            return TimeSpan.Parse(value.ToString()!);
        }
    }
}