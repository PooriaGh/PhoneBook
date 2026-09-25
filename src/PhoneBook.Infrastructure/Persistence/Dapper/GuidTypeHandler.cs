using System.Data;
using Dapper;

namespace PhoneBook.Infrastructure.Persistence.Dapper;

/// <summary>
/// SQLite returns GUIDs as TEXT (EF Core stores them that way); PostgreSQL returns native <c>uuid</c>.
/// This handler lets Dapper map both to <see cref="Guid"/>.
/// </summary>
internal sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value) => value switch
    {
        Guid guid => guid,
        string text => Guid.Parse(text),
        byte[] bytes => new Guid(bytes),
        _ => Guid.Parse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!),
    };

    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value;
}
