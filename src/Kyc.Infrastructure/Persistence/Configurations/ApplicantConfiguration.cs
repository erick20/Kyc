using Kyc.Domain.Entities;
using Kyc.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace Kyc.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Applicant entity.
/// </summary>
internal sealed class ApplicantConfiguration : IEntityTypeConfiguration<Applicant>
{
    public void Configure(EntityTypeBuilder<Applicant> builder)
    {
        builder.ToTable("applicants", KycDbContext.DefaultSchema);

        // Primary key with strongly-typed ID
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => new ApplicantId(value))
            .ValueGeneratedNever();

        // Personal information
        builder.Property(x => x.ExternalReference)
            .HasColumnName("external_reference")
            .HasMaxLength(255);

        builder.Property(x => x.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        // Email value object conversion
        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(50);

        builder.Property(x => x.DateOfBirth)
            .HasColumnName("date_of_birth");

        builder.Property(x => x.Nationality)
            .HasColumnName("nationality")
            .HasMaxLength(2);

        // Address as owned entity (value object)
        builder.OwnsOne(x => x.Address, address =>
        {
            address.Property(a => a.Line1)
                .HasColumnName("address_line1")
                .HasMaxLength(255);

            address.Property(a => a.Line2)
                .HasColumnName("address_line2")
                .HasMaxLength(255);

            address.Property(a => a.City)
                .HasColumnName("address_city")
                .HasMaxLength(100);

            address.Property(a => a.State)
                .HasColumnName("address_state")
                .HasMaxLength(100);

            address.Property(a => a.PostalCode)
                .HasColumnName("address_postal_code")
                .HasMaxLength(20);

            address.Property(a => a.Country)
                .HasColumnName("address_country")
                .HasMaxLength(2);
        });

        // Metadata as JSONB
        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)
                     ?? new Dictionary<string, string>())
            .IsRequired();

        // Audit columns
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        // Soft delete query filter
        builder.HasQueryFilter(x => x.DeletedAt == null);

        // Indexes
        builder.HasIndex(x => x.Email)
            .HasDatabaseName("idx_applicants_email");

        builder.HasIndex(x => x.ExternalReference)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("idx_applicants_external_reference");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("idx_applicants_created_at");

        // Ignore domain events (handled separately)
        builder.Ignore(x => x.DomainEvents);
    }
}
