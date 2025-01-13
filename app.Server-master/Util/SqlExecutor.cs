using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public class SqlExecutor
{
    private readonly string _connectionString;

    public SqlExecutor(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object> parameters = null)
    {
        if (string.IsNullOrEmpty(query))
        {
            throw new ArgumentNullException(nameof(query), "The query cannot be null or empty.");
        }

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                try
                {
                    return await command.ExecuteNonQueryAsync();
                }
                catch (SqlException ex)
                {
                    throw;
                }
            }
        }
    }

    public async Task<object> ExecuteScalarAsync(string query, Dictionary<string, object> parameters = null)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                return await command.ExecuteScalarAsync();
            }
        }
    }

    public async Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object> parameters = null)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.StoredProcedure;

                if (parameters != null)
                {
                    AddParameters(command, parameters);
                }

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var dataTable = new DataTable();
                    dataTable.Load(reader);
                    return dataTable;
                }
            }
        }
    }

    private void AddParameters(SqlCommand command, Dictionary<string, object> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            var sqlParam = new SqlParameter(parameter.Key, parameter.Value ?? DBNull.Value);
            command.Parameters.Add(sqlParam);
        }
    }
}
