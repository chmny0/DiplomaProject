using Npgsql;
using System;
using System.Collections.Generic;

namespace WpfPlannerApp.Services
{
    public static class Db
    {
        private static readonly string _conn =
            "Host=localhost;Port=5432;Database=planner_db;Username=postgres;Password=1234";

        public static NpgsqlConnection GetConnection()
            => new NpgsqlConnection(_conn);

        public static int ExecuteScalarInt(string sql, Dictionary<string, object> p)
        {
            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var kv in p)
                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void Execute(string sql, Dictionary<string, object> p)
        {
            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var kv in p)
                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public static List<Dictionary<string, object>> Query(string sql, Dictionary<string, object> p = null)
        {
            var result = new List<Dictionary<string, object>>();

            using var conn = GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);

            if (p != null)
                foreach (var kv in p)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);

                result.Add(row);
            }

            return result;
        }
    }
}