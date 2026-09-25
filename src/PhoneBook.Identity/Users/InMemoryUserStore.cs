using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PhoneBook.Identity.Users;

/// <summary>
/// End users from configuration, held in memory with hashed passwords only (feature 002, research R-05, R-08).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Passwords are hashed with ASP.NET Core <see cref="PasswordHasher{TUser}"/> (v3: PBKDF2 with HMAC-SHA512,
/// 100,000 iterations, a salt per hash). Plain text is never stored.</item>
/// <item>Users are loaded <b>only in Development</b>. Elsewhere any configured users are ignored with a warning that
/// names no user (FR-027).</item>
/// <item>Validation never throws and costs one hash verification whether or not the account exists, so neither the
/// message nor the timing reveals which accounts exist (FR-021).</item>
/// </list>
/// </remarks>
public sealed partial class InMemoryUserStore
{
    private readonly PasswordHasher<IdentityUser> _hasher = new();
    private readonly Lazy<Dictionary<string, IdentityUser>> _users;
    private readonly Lazy<string> _dummyHash;

    public InMemoryUserStore(IOptions<IdentitySettings> settings, IHostEnvironment environment, ILogger<InMemoryUserStore> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(environment);

        _users = new Lazy<Dictionary<string, IdentityUser>>(
            () => Load(settings.Value.Users, environment.IsDevelopment(), logger), LazyThreadSafetyMode.ExecutionAndPublication);
        _dummyHash = new Lazy<string>(() => _hasher.HashPassword(Placeholder, Guid.NewGuid().ToString("N")));
    }

    private static IdentityUser Placeholder { get; } = new(Guid.Empty, string.Empty, string.Empty, string.Empty, []);

    /// <summary>
    /// Builds the user map now: hashes the configured passwords, or outside Development logs that configured users
    /// are ignored. Called once at start-up by the seeder (data-model §5, research R-08). Returns the number of users.
    /// </summary>
    public int Initialize() => _users.Value.Count;

    public IdentityUser? ValidateCredentials(string? userName, string? password)
    {
        if (string.IsNullOrEmpty(userName) || password is null
            || !_users.Value.TryGetValue(userName, out var user))
        {
            // Same work as a real check, so an unknown account is not faster (FR-021).
            _hasher.VerifyHashedPassword(Placeholder, _dummyHash.Value, password ?? string.Empty);
            return null;
        }

        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed
            ? null
            : user;
    }

    public IdentityUser? FindById(Guid id) => _users.Value.Values.FirstOrDefault(u => u.Id == id);

    private Dictionary<string, IdentityUser> Load(IEnumerable<IdentityUserOptions> configured, bool isDevelopment, ILogger logger)
    {
        var users = new Dictionary<string, IdentityUser>(StringComparer.OrdinalIgnoreCase);
        var entries = configured.Where(u => !string.IsNullOrWhiteSpace(u.UserName)).ToList();
        if (!isDevelopment)
        {
            if (entries.Count > 0)
            {
                LogUsersIgnored(logger, entries.Count);
            }

            return users;
        }

        foreach (var entry in entries)
        {
            var id = StableUserId.For(entry.UserName);
            var hash = _hasher.HashPassword(Placeholder, entry.Password);
            users[entry.UserName] = new IdentityUser(id, entry.UserName, hash, entry.DisplayName, [.. entry.Scopes]);
        }

        return users;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configured users ignored outside Development ({Count} entries)")]
    private static partial void LogUsersIgnored(ILogger logger, int count);
}
