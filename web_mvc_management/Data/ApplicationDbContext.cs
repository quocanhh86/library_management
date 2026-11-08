using Microsoft.EntityFrameworkCore;
using web_mvc_management.Common;
using web_mvc_management.Entity;
using web_mvc_management.Infrastructure;

namespace web_mvc_management.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IUserContext? userContext = null,
    IDateTimeProvider? dateTimeProvider = null) : DbContext(options)
{
    private readonly IUserContext? _userContext = userContext;
    private readonly IDateTimeProvider? _dateTimeProvider = dateTimeProvider;

    public DbSet<Staff> Staff => Set<Staff>();
    public DbSet<ReaderType> ReaderTypes => Set<ReaderType>();
    public DbSet<Reader> Readers => Set<Reader>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<BookImage> BookImages => Set<BookImage>();
    public DbSet<BorrowSlip> BorrowSlips => Set<BorrowSlip>();
    public DbSet<BorrowDetail> BorrowDetails => Set<BorrowDetail>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<LibrarySetting> LibrarySettings => Set<LibrarySetting>();
    public DbSet<OtpEntry> Otps => Set<OtpEntry>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        var now = _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var isStaffUser = _userContext != null &&
            (_userContext.IsInRole(Constant.AppRoles.Admin) || _userContext.IsInRole(Constant.AppRoles.Librarian));
        var currentStaffId = isStaffUser ? _userContext?.CurrentUserId : null;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = now;
                    }
                    if (entry.Entity.UpdatedAt == default)
                    {
                        entry.Entity.UpdatedAt = now;
                    }
                    if (entry.Entity.CreatedBy == null && currentStaffId.HasValue)
                    {
                        entry.Entity.CreatedBy = currentStaffId;
                    }
                    if (entry.Entity.UpdatedBy == null && currentStaffId.HasValue)
                    {
                        entry.Entity.UpdatedBy = currentStaffId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    if (currentStaffId.HasValue)
                    {
                        entry.Entity.UpdatedBy = currentStaffId;
                    }
                    break;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Staff
        modelBuilder.Entity<Staff>(b =>
        {
            b.ToTable("staff", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.Role).HasColumnName("role").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            b.Property(x => x.LastLoginAttempt).HasColumnName("last_login_attempt");
            b.Property(x => x.LockedUntil).HasColumnName("locked_until");
            b.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(36).IsUnicode(false);
            b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        });

        // 2. ReaderType
        modelBuilder.Entity<ReaderType>(b =>
        {
            b.ToTable("reader_types", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            b.Property(x => x.MaxBorrowBooks).HasColumnName("max_borrow_books");
            b.Property(x => x.LoanDays).HasColumnName("loan_days");
            b.Property(x => x.MaxRenewals).HasColumnName("max_renewals");
            b.Property(x => x.RenewDays).HasColumnName("renew_days");
            b.Property(x => x.CardValidMonths).HasColumnName("card_valid_months");
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasIndex(x => x.Code).IsUnique();
        });

        // 3. Reader
        modelBuilder.Entity<Reader>(b =>
        {
            b.ToTable("readers", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReaderTypeId).HasColumnName("reader_type_id");
            b.Property(x => x.Username).HasColumnName("username").HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            b.Property(x => x.LastLoginAttempt).HasColumnName("last_login_attempt");
            b.Property(x => x.LockedUntil).HasColumnName("locked_until");
            b.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(36).IsUnicode(false);
            b.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            b.Property(x => x.CardNo).HasColumnName("card_no").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.MemberCode).HasColumnName("member_code").HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            b.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            b.Property(x => x.CardIssuedDate).HasColumnName("card_issued_date");
            b.Property(x => x.CardExpiryDate).HasColumnName("card_expiry_date");
            b.Property(x => x.CardStatus).HasColumnName("card_status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.LockedAt).HasColumnName("locked_at");
            b.Property(x => x.LockReason).HasColumnName("lock_reason").HasMaxLength(255);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasIndex(x => x.CardNo).IsUnique();

            b.HasOne(x => x.ReaderType)
                .WithMany(x => x.Readers)
                .HasForeignKey(x => x.ReaderTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Category
        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("categories", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        });

        // 5. Publisher
        modelBuilder.Entity<Publisher>(b =>
        {
            b.ToTable("publishers", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
            b.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsUnicode(false);
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.Website).HasColumnName("website").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        });

        // 6. Author
        modelBuilder.Entity<Author>(b =>
        {
            b.ToTable("authors", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150);
            b.Property(x => x.Biography).HasColumnName("biography");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        });

        // 7. Shelf
        modelBuilder.Entity<Shelf>(b =>
        {
            b.ToTable("shelves", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Code).HasColumnName("code").HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasIndex(x => x.Code).IsUnique();
        });

        // 8. Book
        modelBuilder.Entity<Book>(b =>
        {
            b.ToTable("books", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(300);
            b.Property(x => x.Isbn).HasColumnName("isbn").HasMaxLength(17).IsUnicode(false);
            b.Property(x => x.CategoryId).HasColumnName("category_id");
            b.Property(x => x.PublisherId).HasColumnName("publisher_id");
            b.Property(x => x.PublicationYear).HasColumnName("publication_year");
            b.Property(x => x.Edition).HasColumnName("edition").HasMaxLength(50);
            b.Property(x => x.Language).HasColumnName("language").HasMaxLength(50);
            b.Property(x => x.PageCount).HasColumnName("page_count");
            b.Property(x => x.Description).HasColumnName("description");
            b.Property(x => x.CoverUrl).HasColumnName("cover_url").HasMaxLength(500).IsUnicode(false);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.Category)
                .WithMany(x => x.Books)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Publisher)
                .WithMany(x => x.Books)
                .HasForeignKey(x => x.PublisherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 9. BookAuthor (Composite Key)
        modelBuilder.Entity<BookAuthor>(b =>
        {
            b.ToTable("book_authors", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => new { x.BookId, x.AuthorId });
            b.Property(x => x.BookId).HasColumnName("book_id");
            b.Property(x => x.AuthorId).HasColumnName("author_id");
            b.Property(x => x.AuthorOrder).HasColumnName("author_order");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.Book)
                .WithMany(x => x.BookAuthors)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Author)
                .WithMany(x => x.BookAuthors)
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. BookCopy
        modelBuilder.Entity<BookCopy>(b =>
        {
            b.ToTable("book_copies", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.BookId).HasColumnName("book_id");
            b.Property(x => x.ShelfId).HasColumnName("shelf_id");
            b.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.CopyStatus).HasColumnName("copy_status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.Price).HasColumnName("price").HasPrecision(18, 2);
            b.Property(x => x.AcquiredDate).HasColumnName("acquired_date");
            b.Property(x => x.DisposedAt).HasColumnName("disposed_at");
            b.Property(x => x.DisposalReason).HasColumnName("disposal_reason").HasMaxLength(255);
            b.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasIndex(x => x.Barcode).IsUnique();

            b.HasOne(x => x.Book)
                .WithMany(x => x.BookCopies)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Shelf)
                .WithMany(x => x.BookCopies)
                .HasForeignKey(x => x.ShelfId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 11. BorrowSlip
        modelBuilder.Entity<BorrowSlip>(b =>
        {
            b.ToTable("borrow_slips", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReaderId).HasColumnName("reader_id");
            b.Property(x => x.IssuedBy).HasColumnName("issued_by");
            b.Property(x => x.BorrowedAt).HasColumnName("borrowed_at");
            b.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.Reader)
                .WithMany(x => x.BorrowSlips)
                .HasForeignKey(x => x.ReaderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.IssuedByStaff)
                .WithMany(x => x.IssuedBorrowSlips)
                .HasForeignKey(x => x.IssuedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 12. BorrowDetail
        modelBuilder.Entity<BorrowDetail>(b =>
        {
            b.ToTable("borrow_details", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.SlipId).HasColumnName("slip_id");
            b.Property(x => x.CopyId).HasColumnName("copy_id");
            b.Property(x => x.DueDate).HasColumnName("due_date");
            b.Property(x => x.RenewCount).HasColumnName("renew_count");
            b.Property(x => x.ReturnedAt).HasColumnName("returned_at");
            b.Property(x => x.ReturnCondition).HasColumnName("return_condition").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.ReceivedBy).HasColumnName("received_by");
            b.Property(x => x.ReturnNote).HasColumnName("return_note").HasMaxLength(500);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.BorrowSlip)
                .WithMany(x => x.BorrowDetails)
                .HasForeignKey(x => x.SlipId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.BookCopy)
                .WithMany(x => x.BorrowDetails)
                .HasForeignKey(x => x.CopyId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.ReceivedByStaff)
                .WithMany(x => x.ReceivedBorrowDetails)
                .HasForeignKey(x => x.ReceivedBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 13. Fine
        modelBuilder.Entity<Fine>(b =>
        {
            b.ToTable("fines", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.BorrowDetailId).HasColumnName("borrow_detail_id");
            b.Property(x => x.FineType).HasColumnName("fine_type").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.OverdueDays).HasColumnName("overdue_days");
            b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            b.Property(x => x.FineStatus).HasColumnName("fine_status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.PaidAt).HasColumnName("paid_at");
            b.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.HandledBy).HasColumnName("handled_by");
            b.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.BorrowDetail)
                .WithMany(x => x.Fines)
                .HasForeignKey(x => x.BorrowDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.HandledByStaff)
                .WithMany(x => x.HandledFines)
                .HasForeignKey(x => x.HandledBy)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 14. Reservation
        modelBuilder.Entity<Reservation>(b =>
        {
            b.ToTable("reservations", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.ReaderId).HasColumnName("reader_id");
            b.Property(x => x.BookId).HasColumnName("book_id");
            b.Property(x => x.ReservationStatus).HasColumnName("reservation_status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.ReservedAt).HasColumnName("reserved_at");
            b.Property(x => x.AssignedCopyId).HasColumnName("assigned_copy_id");
            b.Property(x => x.ReadyAt).HasColumnName("ready_at");
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            b.Property(x => x.ClosedAt).HasColumnName("closed_at");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.Reader)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.ReaderId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Book)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.AssignedCopy)
                .WithMany(x => x.Reservations)
                .HasForeignKey(x => x.AssignedCopyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // 15. LibrarySetting
        modelBuilder.Entity<LibrarySetting>(b =>
        {
            b.ToTable("library_settings", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.OverdueFinePerDay).HasColumnName("overdue_fine_per_day").HasPrecision(18, 2);
            b.Property(x => x.DamagedFinePercent).HasColumnName("damaged_fine_percent");
            b.Property(x => x.LostFinePercent).HasColumnName("lost_fine_percent");
            b.Property(x => x.MaxUnpaidFine).HasColumnName("max_unpaid_fine").HasPrecision(18, 2);
            b.Property(x => x.ReservationHoldHours).HasColumnName("reservation_hold_hours");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        });

        // 16. OtpEntry (SEC-08, SEC-12)
        modelBuilder.Entity<OtpEntry>(b =>
        {
            b.ToTable("otps", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.OtpCode).HasColumnName("otp_code").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.Purpose).HasColumnName("purpose").HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.FailedAttempts).HasColumnName("failed_attempts");
            b.Property(x => x.IsUsed).HasColumnName("is_used");
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");

            b.HasIndex(x => new { x.Email, x.Purpose, x.IsUsed, x.ExpiresAt });
        });

        // 17. BookImage (1 Book - N BookImages: Cover, BackCover, TableOfContents, PreviewPage, Gallery)
        modelBuilder.Entity<BookImage>(b =>
        {
            b.ToTable("book_images", tb => tb.UseSqlOutputClause(false));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.BookId).HasColumnName("book_id");
            b.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(500).IsUnicode(false);
            b.Property(x => x.PublicId).HasColumnName("public_id").HasMaxLength(255).IsUnicode(false);
            b.Property(x => x.ImageType).HasColumnName("image_type").HasMaxLength(30).IsUnicode(false);
            b.Property(x => x.Caption).HasColumnName("caption").HasMaxLength(200);
            b.Property(x => x.DisplayOrder).HasColumnName("display_order");
            b.Property(x => x.IsPrimary).HasColumnName("is_primary");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            b.Property(x => x.CreatedBy).HasColumnName("created_by");
            b.Property(x => x.UpdatedBy).HasColumnName("updated_by");

            b.HasOne(x => x.Book)
                .WithMany(x => x.BookImages)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => new { x.BookId, x.Status, x.DisplayOrder });
        });
    }
}
