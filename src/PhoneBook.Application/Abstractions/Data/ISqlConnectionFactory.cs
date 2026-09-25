using System.Data.Common;

namespace PhoneBook.Application.Abstractions.Data;

/// <summary>Creates provider-specific connections for Dapper queries on the read side.</summary>
public interface ISqlConnectionFactory
{
    DbConnection CreateConnection();

    /// <summary>The telemetry <c>db.system</c> name of the configured provider: <c>sqlite</c> or <c>postgresql</c>.</summary>
    string ProviderName { get; }
}
