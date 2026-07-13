using Npgsql;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Infrastructure.Database
{
    public static class VehicleDataAccess
    {
        public const string DefaultModelName = "Неизвестная модель";

        public static async Task<int> EnsureDefaultModelIdAsync(
            NpgsqlConnection connection, NpgsqlTransaction? transaction = null)
        {
            using var insert = new NpgsqlCommand(@"
                INSERT INTO vehicle_models (name, seats, capacity, description)
                VALUES (@name, 40, 60, 'Автоматически создана при миграции или импорте')
                ON CONFLICT (name) DO NOTHING", connection, transaction);
            insert.Parameters.AddWithValue("@name", DefaultModelName);
            await insert.ExecuteNonQueryAsync();

            using var select = new NpgsqlCommand(
                "SELECT id FROM vehicle_models WHERE name = @name", connection, transaction);
            select.Parameters.AddWithValue("@name", DefaultModelName);
            return (int)(await select.ExecuteScalarAsync())!;
        }

        public static async Task<int> EnsureVehicleIdAsync(
            NpgsqlConnection connection, string unitName, NpgsqlTransaction? transaction = null)
        {
            var modelId = await EnsureDefaultModelIdAsync(connection, transaction);

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO vehicles (unit_name, vehicle_model_id)
                VALUES (@unit, @modelId)
                ON CONFLICT (unit_name) DO UPDATE SET unit_name = EXCLUDED.unit_name
                RETURNING id", connection, transaction);
            cmd.Parameters.AddWithValue("@unit", unitName);
            cmd.Parameters.AddWithValue("@modelId", modelId);
            return (int)(await cmd.ExecuteScalarAsync())!;
        }
    }
}
