using System.Data;
using System.Data.Common;
using Dapper;

namespace Products.Persistence.Dapper;

// Przekazuje Dapperowi parametry przygotowane przez SqlDialect (typy, rozmiary, precyzja dobrane do kolumn)
internal sealed class DbParameters(DbParameter[] parameters) : SqlMapper.IDynamicParameters
{
    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter);
    }
}
