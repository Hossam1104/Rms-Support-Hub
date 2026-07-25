using System.Data;
using Microsoft.Data.SqlClient;

namespace OnlineOrderTool.Data;

public interface ISqlServerConnectionFactory
{
    IDbConnection CreateConnection(string connectionString);
}

public class SqlServerConnectionFactory : ISqlServerConnectionFactory
{
    public IDbConnection CreateConnection(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
