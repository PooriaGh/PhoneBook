using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhoneBook.Domain.Contacts;

namespace PhoneBook.Infrastructure.Persistence.Configurations;

/// <summary>Write-side mapping of the <see cref="Contact"/> aggregate to the <c>contacts</c> table (data-model §2).</summary>
internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public const string TableName = "contacts";

    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new ContactId(value))
            .ValueGeneratedNever();

        builder.ComplexProperty(c => c.Name, name =>
        {
            name.Property(n => n.FirstName).HasColumnName("first_name").HasMaxLength(PersonName.MaxLength).IsRequired();
            name.Property(n => n.LastName).HasColumnName("last_name").HasMaxLength(PersonName.MaxLength).IsRequired();
        });

        builder.Property(c => c.Phone)
            .HasColumnName("phone_number")
            .HasConversion(phone => phone.Value, value => PhoneNumber.FromPersisted(value))
            .HasMaxLength(PhoneNumber.MaxDigits + 1)
            .IsRequired();

        builder.ComplexProperty(c => c.Tag, tag =>
        {
            tag.Property(t => t.Value).HasColumnName("tag").HasMaxLength(Tag.MaxLength).IsRequired();
            tag.Property(t => t.NormalizedValue).HasColumnName("normalized_tag").HasMaxLength(Tag.MaxLength).IsRequired();
        });

        builder.Property(c => c.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(c => c.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.Ignore(c => c.DomainEvents);
    }
}
