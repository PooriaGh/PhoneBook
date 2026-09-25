using System.Diagnostics;

namespace PhoneBook.Api.Infrastructure;

/// <summary>
/// The <c>traceId</c> written into ProblemDetails (feature 002, FR-013, research R-04): the 32-character W3C trace id
/// that telemetry back ends index, so operators can look up the trace of any error response. Falls back to the
/// request identifier when no activity is running.
/// </summary>
internal static class TraceIds
{
    public static string Current(HttpContext context) =>
        Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
}
