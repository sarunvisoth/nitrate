using Microsoft.EntityFrameworkCore;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence.Entities;

namespace Prismedia.Infrastructure.Persistence;

internal static partial class PrismediaModelConfiguration {
    private static void ConfigureReaderPreferences(ModelBuilder modelBuilder) {
        modelBuilder.Entity<UserReaderPreferencesRow>(entity => {
            entity.ToTable("user_reader_preferences");
            entity.HasKey(row => row.UserId);
            entity.Property(row => row.UserId).HasColumnName("user_id").ValueGeneratedNever();
            entity.Property(row => row.FontFamily)
                .HasColumnName("font_family")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<ReaderFontFamily>());
            entity.Property(row => row.FontSizePercent).HasColumnName("font_size_percent");
            entity.Property(row => row.LineHeight).HasColumnName("line_height");
            entity.Property(row => row.MarginStep).HasColumnName("margin_step");
            entity.Property(row => row.MaxContentWidth).HasColumnName("max_content_width");
            entity.Property(row => row.Theme)
                .HasColumnName("theme")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<ReaderTheme>());
            entity.Property(row => row.Flow)
                .HasColumnName("flow")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<ReaderMode>());
            entity.Property(row => row.TranslationTargetLanguage)
                .HasColumnName("translation_target_language")
                .HasMaxLength(16);
            entity.Property(row => row.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne<UserRow>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VocabularyEntryRow>(entity => {
            entity.ToTable("vocabulary_entries");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.UserId).HasColumnName("user_id");
            entity.Property(row => row.Word).HasColumnName("word").HasMaxLength(256).IsRequired();
            entity.Property(row => row.Language).HasColumnName("language").HasMaxLength(16).IsRequired();
            entity.Property(row => row.Definition).HasColumnName("definition");
            entity.Property(row => row.BookId).HasColumnName("book_id");
            entity.Property(row => row.SourceLocator).HasColumnName("source_locator").HasMaxLength(4096);
            entity.Property(row => row.SourceSentence).HasColumnName("source_sentence").HasMaxLength(1000);
            entity.Property(row => row.Status)
                .HasColumnName("status")
                .HasMaxLength(32)
                .IsRequired()
                .HasConversion(value => value.ToCode(), value => value.DecodeAs<VocabularyStatus>());
            entity.Property(row => row.CreatedAt).HasColumnName("created_at");
            entity.Property(row => row.UpdatedAt).HasColumnName("updated_at");
            entity.Property(row => row.DeletedAt).HasColumnName("deleted_at");
            entity.HasIndex(row => new { row.UserId, row.UpdatedAt });
            entity.HasIndex(row => new { row.UserId, row.BookId });
            // Case-insensitive uniqueness per user among live rows only; enforced at the
            // application layer too, since a partial functional unique index needs raw SQL
            // that migrations review by hand (see the AddVocabulary migration).
            entity.HasOne<UserRow>().WithMany().HasForeignKey(row => row.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EntityRow>().WithMany().HasForeignKey(row => row.BookId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
