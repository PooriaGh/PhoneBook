using Microsoft.EntityFrameworkCore;

namespace PhoneBook.Identity.Data;

/// <summary>OpenIddict EF Core stores (applications, scopes, tokens, authorizations) in in-memory SQLite.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.UseOpenIddict();
}
