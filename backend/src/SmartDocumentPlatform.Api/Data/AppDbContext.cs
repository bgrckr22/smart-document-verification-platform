using Microsoft.EntityFrameworkCore;
using SmartDocumentPlatform.Api.Entities;

namespace SmartDocumentPlatform.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<DocumentRecord>();
        document.ToTable("documents");
        document.HasKey(item => item.Id);
        document.Property(item => item.Id).HasColumnName("id");
        document.Property(item => item.OriginalFileName)
            .HasColumnName("original_filename")
            .HasMaxLength(255)
            .IsRequired();
        document.Property(item => item.UploadedAt).HasColumnName("uploaded_at").IsRequired();
        document.Property(item => item.Status)
            .HasColumnName("processing_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        document.Property(item => item.QualityScore).HasColumnName("quality_score");
        document.Property(item => item.BlurDetected).HasColumnName("blur_detected");
        document.Property(item => item.DocumentDetected).HasColumnName("document_detected");
        document.Property(item => item.ProcessingTimeMs).HasColumnName("processing_time_ms");
        document.Property(item => item.OriginalImagePath)
            .HasColumnName("original_image_path")
            .HasMaxLength(500)
            .IsRequired();
        document.Property(item => item.ProcessedImagePath)
            .HasColumnName("processed_image_path")
            .HasMaxLength(500);
        document.Property(item => item.RotationDegrees).HasColumnName("rotation_degrees");
        document.Property(item => item.Orientation)
            .HasColumnName("orientation")
            .HasMaxLength(24);
        document.Property(item => item.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000);

        document.HasIndex(item => new { item.UploadedAt, item.Id })
            .HasDatabaseName("ix_documents_uploaded_at_id")
            .IsDescending();
    }
}
