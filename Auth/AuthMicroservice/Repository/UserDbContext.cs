using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuthMicroservice.Repository
{
    public class UserDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        //public UserDbContext(IConfiguration configuration)
        //{
        //    _configuration = configuration;
        //}
        public UserDbContext(DbContextOptions<UserDbContext> options, IConfiguration configuration)
       : base(options)
        {
            _configuration = configuration;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<UserRole>userRoles { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Subscriber> Subscribers { get; set; }
        public DbSet<EmailHistory> EmailHistories { get; set; }
        public DbSet<SmtpConfig> SmtpConfigs { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<ContactActivity> ContactActivities { get; set; }


        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    var connectionString = _configuration.GetConnectionString("DefaultConnection");
        //    optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
        //}
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);
        //}
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Ensure OnConfiguring is not used if options are passed in constructor
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
            }
        }

        // MySQL's DATETIME columns carry no timezone. Pomelo materializes them with
        // DateTimeKind.Unspecified, so System.Text.Json serializes them without a "Z"
        // suffix — the frontend then misreads a true UTC instant as local time. These
        // fields are always populated with DateTime.UtcNow (or a UTC-converted value
        // from the client), so it's safe to stamp Kind=Utc on read. BaseEntity's
        // CreatedDate/UpdatedBy use DateTime.Now (local) and are intentionally left alone.
        private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
            new ValueConverter<DateTime, DateTime>(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        private static readonly ValueConverter<DateTime?, DateTime?> UtcNullableDateTimeConverter =
            new ValueConverter<DateTime?, DateTime?>(
                v => v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Campaign>().Property(c => c.ScheduledDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Campaign>().Property(c => c.SentDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<EmailHistory>().Property(e => e.SentDate).HasConversion(UtcDateTimeConverter);
            modelBuilder.Entity<Contact>().Property(c => c.VerifiedDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Contact>().Property(c => c.VerificationTokenExpiry).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Contact>().Property(c => c.NextContactDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Contact>().Property(c => c.LastEmailDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Contact>().Property(c => c.LastSmsDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Subscriber>().Property(s => s.VerifiedDate).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<Subscriber>().Property(s => s.VerificationTokenExpiry).HasConversion(UtcNullableDateTimeConverter);
            modelBuilder.Entity<User>().Property(u => u.EmailConfirmationTokenExpiry).HasConversion(UtcNullableDateTimeConverter);
        }
        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                }
            }

            return base.SaveChanges();
        }
    }
}
