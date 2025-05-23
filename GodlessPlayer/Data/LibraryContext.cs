using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GodlessPlayer.Data
{
    public class LibraryContext : DbContext
    {
        public DbSet<Track> Tracks { get; set; }
        public DbSet<Artist> Artists { get; set; }
        public DbSet<Album> Albums { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public LibraryContext()
        {
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(GetConnectionStringSqlServer());
            }
        }
        public static string GetConnectionStringSqlServer()
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = "(local)",
                InitialCatalog = "MP3",
                UserID = "user",
                Password = "root",
                IntegratedSecurity = false,
                TrustServerCertificate = true
            }.ConnectionString;
        }
    }
}