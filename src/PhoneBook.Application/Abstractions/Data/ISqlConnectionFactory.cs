using System.Data.Common;

namespace PhoneBook.Application.Abstractions.Data;

/// <summary>Creates provider-specific connections for Dapper queries on the read side.</summary>
public interface ISqlConnectionFactory
{
    DbConnection CreateConnection();
}
