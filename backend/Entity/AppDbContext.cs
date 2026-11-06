using System.Data.Entity;

namespace Backend.Entity;

public class AppDbContext : DbContext
{
    public AppDbContext(string connectionString) : base(connectionString)
    {
        // Nonaktifkan initializer otomatis agar aman untuk testing koneksi
        Database.SetInitializer<AppDbContext>(null);
    }
}
