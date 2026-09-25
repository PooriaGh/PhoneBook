using System.Globalization;
using PhoneBook.SharedKernel.Results;

namespace PhoneBook.Api.Infrastructure;

/// <summary>HTTP ETag / If-Match support over the contact <c>version</c> (research R-10).</summary>
internal static class ETagExtensions
{
    public static string ToETag(int version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    public static void SetETag(this HttpResponse response, int version) => response.Headers.ETag = ToETag(version);

    /// <summary>Missing or <c>*</c> → <c>null</c> (no precondition); <c>"n"</c> → n; anything else → validation error.</summary>
    public static Result<int?> TryParseIfMatch(HttpRequest request)
    {
        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length == 0 || header == "*")
        {
            return Result<int?>.Success(null);
        }

        var value = header.StartsWith("W/", StringComparison.Ordinal) ? header[2..] : header;
        value = value.Trim('"');

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version) && version > 0
            ? Result<int?>.Success(version)
            : Result<int?>.Failure(ValidationError.For(
                "If-Match", "Request.IfMatch.Invalid", "If-Match must be a quoted positive version, e.g. \"3\"."));
    }
}
