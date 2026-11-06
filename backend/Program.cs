using Backend.Services;

// Muat variabel lingkungan dari file .env
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Registrasi Service
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddOpenApi();

// Konfigurasi CORS agar frontend (Flutter dsb) dapat mengakses API
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Endpoint 1: API JSON untuk test koneksi
app.MapGet("/api/test-db", async (DatabaseService dbService, CancellationToken ct) =>
{
    var result = await dbService.TestConnectionAsync(ct);
    return Results.Json(result, statusCode: result.Success ? 200 : 503);
});

// Alias endpoint singkat
app.MapGet("/test-db", async (DatabaseService dbService, CancellationToken ct) =>
{
    var result = await dbService.TestConnectionAsync(ct);
    return Results.Json(result, statusCode: result.Success ? 200 : 503);
});

// Endpoint Health Check
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "Backend API",
    time = DateTime.UtcNow
}));

// Endpoint Root: Tampilan Web Dashboard Interaktif untuk Test Koneksi
app.MapGet("/", (DatabaseService dbService) =>
{
    var connStr = dbService.GetConnectionString();
    var html = $$"""
    <!DOCTYPE html>
    <html lang="id">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Database Connection Tester - .NET Backend</title>
        <style>
            :root {
                --bg: #0f172a;
                --surface: #1e293b;
                --border: #334155;
                --text: #f8fafc;
                --text-muted: #94a3b8;
                --success: #10b981;
                --danger: #ef4444;
                --primary: #3b82f6;
                --primary-hover: #2563eb;
            }
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
                font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                background-color: var(--bg);
                color: var(--text);
                min-height: 100vh;
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                padding: 24px;
            }
            .card {
                background-color: var(--surface);
                border: 1px solid var(--border);
                border-radius: 12px;
                padding: 32px;
                max-width: 680px;
                width: 100%;
                box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4);
            }
            .header {
                display: flex;
                align-items: center;
                gap: 12px;
                margin-bottom: 20px;
            }
            .badge-dot {
                width: 12px;
                height: 12px;
                border-radius: 50%;
                background-color: var(--primary);
            }
            h1 { font-size: 1.5rem; font-weight: 700; }
            p.sub { color: var(--text-muted); font-size: 0.925rem; margin-bottom: 24px; }
            .info-grid {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
                gap: 12px;
                margin-bottom: 24px;
            }
            .info-box {
                background: #0f172a80;
                padding: 12px;
                border-radius: 8px;
                border: 1px solid var(--border);
            }
            .info-box span { display: block; font-size: 0.75rem; color: var(--text-muted); }
            .info-box strong { font-size: 0.95rem; word-break: break-all; }
            .btn-action {
                background-color: var(--primary);
                color: white;
                border: none;
                padding: 12px 24px;
                border-radius: 8px;
                font-size: 1rem;
                font-weight: 600;
                cursor: pointer;
                width: 100%;
                transition: background 0.2s ease;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 8px;
            }
            .btn-action:hover { background-color: var(--primary-hover); }
            .btn-action:disabled { opacity: 0.6; cursor: not-allowed; }
            .result-area {
                margin-top: 24px;
                display: none;
            }
            .status-banner {
                padding: 16px;
                border-radius: 8px;
                margin-bottom: 16px;
                display: flex;
                align-items: center;
                gap: 12px;
            }
            .status-banner.success {
                background-color: rgba(16, 185, 129, 0.15);
                border: 1px solid rgba(16, 185, 129, 0.3);
                color: #34d399;
            }
            .status-banner.error {
                background-color: rgba(239, 68, 68, 0.15);
                border: 1px solid rgba(239, 68, 68, 0.3);
                color: #f87171;
            }
            pre {
                background: #090d16;
                padding: 14px;
                border-radius: 8px;
                border: 1px solid var(--border);
                font-size: 0.85rem;
                overflow-x: auto;
                color: #e2e8f0;
            }
            .endpoints {
                margin-top: 24px;
                padding-top: 20px;
                border-top: 1px solid var(--border);
                font-size: 0.85rem;
                color: var(--text-muted);
            }
            .endpoints code {
                background: #0f172a;
                padding: 2px 6px;
                border-radius: 4px;
                color: #38bdf8;
            }
            .spinner {
                width: 16px;
                height: 16px;
                border: 2px solid white;
                border-top-color: transparent;
                border-radius: 50%;
                animation: spin 0.8s linear infinite;
                display: none;
            }
            @keyframes spin { to { transform: rotate(360deg); } }
        </style>
    </head>
    <body>
        <div class="card">
            <div class="header">
                <div class="badge-dot" id="dot"></div>
                <h1>Database Connection Tester</h1>
            </div>
            <p class="sub">Menguji koneksi langsung antara aplikasi .NET dan SQL Server menggunakan Entity Framework.</p>

            <div class="info-grid">
                <div class="info-box">
                    <span>HOST & PORT</span>
                    <strong id="cfg-host">localhost:1433</strong>
                </div>
                <div class="info-box">
                    <span>DATABASE</span>
                    <strong id="cfg-db">master</strong>
                </div>
                <div class="info-box">
                    <span>USER</span>
                    <strong id="cfg-user">sa</strong>
                </div>
                <div class="info-box">
                    <span>ORM / DRIVER</span>
                    <strong>Entity Framework</strong>
                </div>
            </div>

            <button class="btn-action" id="btnTest" onclick="testConnection()">
                <div class="spinner" id="spinner"></div>
                <span id="btnText">Uji Koneksi Database Sekarang</span>
            </button>

            <div class="result-area" id="resultArea">
                <div class="status-banner" id="statusBanner">
                    <strong id="bannerText"></strong>
                </div>
                <pre id="jsonResult"></pre>
            </div>

            <div class="endpoints">
                API Endpoint: <a href="/api/test-db" target="_blank" style="color: #38bdf8; text-decoration: none;"><code>GET /api/test-db</code></a>
                &nbsp;|&nbsp;
                <a href="/api/health" target="_blank" style="color: #38bdf8; text-decoration: none;"><code>GET /api/health</code></a>
            </div>
        </div>

        <script>
            async function testConnection() {
                const btn = document.getElementById('btnTest');
                const btnText = document.getElementById('btnText');
                const spinner = document.getElementById('spinner');
                const resultArea = document.getElementById('resultArea');
                const statusBanner = document.getElementById('statusBanner');
                const bannerText = document.getElementById('bannerText');
                const jsonResult = document.getElementById('jsonResult');
                const dot = document.getElementById('dot');

                btn.disabled = true;
                btnText.textContent = "Sedang Menghubungkan...";
                spinner.style.display = "inline-block";

                try {
                    const response = await fetch('/api/test-db');
                    const data = await response.json();

                    resultArea.style.display = "block";
                    jsonResult.textContent = JSON.stringify(data, null, 2);

                    if (data.server) {
                        document.getElementById('cfg-host').textContent = data.server;
                    }
                    if (data.database) {
                        document.getElementById('cfg-db').textContent = data.database;
                    }

                    if (data.success) {
                        statusBanner.className = "status-banner success";
                        bannerText.textContent = "✅ " + data.message + " (" + data.latencyMs + " ms)";
                        dot.style.backgroundColor = "var(--success)";
                    } else {
                        statusBanner.className = "status-banner error";
                        bannerText.textContent = "❌ " + data.message;
                        dot.style.backgroundColor = "var(--danger)";
                    }
                } catch (err) {
                    resultArea.style.display = "block";
                    statusBanner.className = "status-banner error";
                    bannerText.textContent = "❌ Terjadi error saat memanggil API backend: " + err.message;
                    jsonResult.textContent = String(err);
                    dot.style.backgroundColor = "var(--danger)";
                } finally {
                    btn.disabled = false;
                    btnText.textContent = "Uji Ulang Koneksi";
                    spinner.style.display = "none";
                }
            }

            // Jalankan otomatis saat halaman dibuka
            window.addEventListener('DOMContentLoaded', () => {
                testConnection();
            });
        </script>
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

// Background test saat aplikasi pertama kali menyala (ditampilkan di konsol)
_ = Task.Run(async () =>
{
    await Task.Delay(1500); // beri jeda sebentar agar log web host selesai
    var dbService = app.Services.GetRequiredService<DatabaseService>();
    var test = await dbService.TestConnectionAsync();

    Console.WriteLine();
    Console.WriteLine("=================================================================");
    Console.WriteLine("          [PMS BACKEND] PEMERIKSAAN KONEKSI DATABASE             ");
    Console.WriteLine("=================================================================");
    Console.WriteLine($" Server   : {test.Server}");
    Console.WriteLine($" Database : {test.Database}");
    if (test.Success)
    {
        Console.WriteLine($" Status   : [BERHASIL] Terhubung ke database dalam {test.LatencyMs}ms");
        Console.WriteLine($" Versi DB : {test.SqlVersion?.Split('\n').FirstOrDefault()?.Trim()}");
    }
    else
    {
        Console.WriteLine($" Status   : [GAGAL] Tidak dapat terhubung ke database");
        Console.WriteLine($" Error    : {test.ErrorMessage}");
        Console.WriteLine($" Tips     : {test.Tip}");
    }
    Console.WriteLine(" Web UI   : Buka http://localhost:5277 di browser untuk test UI");
    Console.WriteLine(" API URL  : GET http://localhost:5277/api/test-db");
    Console.WriteLine("=================================================================");
    Console.WriteLine();
});

app.Run();
