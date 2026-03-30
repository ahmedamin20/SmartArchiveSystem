using Microsoft.EntityFrameworkCore;
using SmartArchive.Models;

namespace SmartArchive.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Person> Persons => Set<Person>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>()
                .HasIndex(p => p.NationalId)
                .IsUnique();

            modelBuilder.Entity<Person>().Property(p => p.NationalId).IsRequired();
            modelBuilder.Entity<Person>().Property(p => p.FullName).IsRequired();
            base.OnModelCreating(modelBuilder);
        }
    }
}
