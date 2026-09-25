using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneBook.Application.Abstractions.Data.ReadModels;

namespace PhoneBook.Infrastructure.Persistence.Configurations;

/// <summary>Read-side mapping: the flat read model over the same <c>contacts</c> table.</summary>
internal sealed class ContactReadModelConfiguration : IEntityTypeConfiguration<ContactReadModel>
{
    public void Configure(EntityTypeBuilder<ContactReadModel> builder)
    {
        builder.ToTable(ContactConfiguration.TableName);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.FirstName).HasColumnName("first_name");
        builder.Property(c => c.LastName).HasColumnName("last_name");
        builder.Property(c => c.PhoneNumber).HasColumnName("phone_number");
        builder.Property(c => c.Tag).HasColumnName("tag");
        builder.Property(c => c.NormalizedTag).HasColumnName("normalized_tag");
        builder.Property(c => c.Version).HasColumnName("version");
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");
    }
}
