using System.Diagnostics;
using Backend.Entity;
using System.Data.SqlClient;

namespace Backend.Services;

public class DatabaseTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Server { get; set; }
    public string? Database { get; set; }
    public string? SqlVersion { get; set; }
    public long LatencyMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public string? Tip { get; set; }
}

public class DatabaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GetConnectionString()
    {
        // 1. Ambil dari environment variables (.env / system env)
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "sa";
        var pass = Environment.GetEnvironmentVariable("DB_PASS");
        var db = Environment.GetEnvironmentVariable("DB_NAME") ?? "master";

        host = host.Trim('"', '\'', ' ');
        port = port.Trim('"', '\'', ' ');
        user = user.Trim('"', '\'', ' ');
        pass = pass?.Trim('"', '\'', ' ');
        db = db.Trim('"', '\'', ' ');

        if (!string.IsNullOrWhiteSpace(pass))
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"{host},{port}",
                InitialCatalog = db,
                UserID = user,
                Password = pass,
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };
            return builder.ConnectionString;
        }

        // 2. Ambil dari appsettings.json jika ada
        var configConn = _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configConn))
        {
            return configConn;
        }

        // Fallback default
        return "success";
    }

    public async Task<DatabaseTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connStr = GetConnectionString();
        var builder = new SqlConnectionStringBuilder(connStr);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Menggunakan EntityFramework DbContext untuk menguji koneksi
            using var dbContext = new AppDbContext(connStr);

            if (dbContext.Database.Connection.State != System.Data.ConnectionState.Open)
            {
                await dbContext.Database.Connection.OpenAsync(cancellationToken);
            }

            var versionQuery = dbContext.Database.SqlQuery<string>("SELECT @@VERSION;");
            var version = await versionQuery.FirstOrDefaultAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("Database connection successful via EntityFramework to {DataSource}/{Database} in {ElapsedMs}ms",
                builder.DataSource, builder.InitialCatalog, stopwatch.ElapsedMilliseconds);

            return new DatabaseTestResult
            {
                Success = true,
                Message = "Koneksi ke database SQL Server berhasil via Entity Framework!",
                Server = builder.DataSource,
                Database = builder.InitialCatalog,
                SqlVersion = version?.Trim(),
                LatencyMs = stopwatch.ElapsedMilliseconds,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to connect to database at {DataSource}", builder.DataSource);

            return new DatabaseTestResult
            {
                Success = false,
                Message = "Gagal terhubung ke database SQL Server.",
                Server = builder.DataSource,
                Database = builder.InitialCatalog,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                CheckedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message,
                Tip = "Pastikan container SQL Server sudah berjalan (jalankan 'docker compose up -d' di folder backend) dan DB_PASS di .env sudah sesuai."
            };
        }
    }
}
