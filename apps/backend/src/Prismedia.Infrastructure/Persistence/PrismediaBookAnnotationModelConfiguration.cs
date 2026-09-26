using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Persistence;

internal static partial class PrismediaModelConfiguration {
    private static void ConfigureBookAnnotations(ModelBuilder modelBuilder) {
        modelBuilder.Entity<BookAnnotationRow>(entity => {
            entity.ToTable("book_annotations");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.LocatorKind)
                .HasColumnName("locator_kind")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationLocatorKind>());
            entity.Property(row => row.Locator).HasColumnName("locator").HasMaxLength(4096).IsRequired();
            entity.Property(row => row.SpineIndex).HasColumnName("spine_index");
            entity.Property(row => row.Position).HasColumnName("position");
            entity.Property(row => row.SelectedText).HasColumnName("selected_text").HasMaxLength(8000).IsRequired();
            entity.Property(row => row.TextBefore).HasColumnName("text_before").HasMaxLength(256);
            entity.Property(row => row.TextAfter).HasColumnName("text_after").HasMaxLength(256);
            entity.Property(row => row.Style)
                .HasColumnName("style")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationStyle>());
            entity.Property(row => row.Color)
                .HasColumnName("color")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<AnnotationColor>());
            entity.Property(row => row.Note).HasColumnName("note").HasMaxLength(20000);
            entity.Property(row => row.SourceSignature).HasColumnName("source_signature").HasMaxLength(512);
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.UpdatedAt).HasColumnName("updated_at");
            entity.Property(row => row.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(row => new { row.UserId, row.BookId });
            entity.HasIndex(row => new { row.UserId, row.UpdatedAt });
            entity.HasOne<EntityRow>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserRow>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
