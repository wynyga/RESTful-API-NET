using Data.Repositories;
using Models;
using Models.Entities;

namespace API
{
    /// <summary>
    /// Optional start-up data, driven entirely by configuration so nothing is created unless asked for:
    /// SEED_ADMIN_EMAIL + SEED_ADMIN_PASSWORD create the first Admin; SEED_SAMPLE_DATA=true adds sample projects.
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            var config = services.GetRequiredService<IConfiguration>();

            var email = config["SEED_ADMIN_EMAIL"];
            var password = config["SEED_ADMIN_PASSWORD"];
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                var users = services.GetRequiredService<IUserRepository>();
                var normalized = email.Trim().ToLowerInvariant();
                if (await users.GetUserByEmailAsync(normalized) is null)
                {
                    await users.AddUserAsync(new User
                    {
                        Email = normalized,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = "Admin",
                    });
                }
            }

            if (config.GetValue<bool>("SEED_SAMPLE_DATA"))
            {
                var projects = services.GetRequiredService<IProjectRepository>();
                if (!await projects.AnyAsync(ct))
                {
                    var today = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.Date;
                    foreach (var p in Sample(today))
                    {
                        await projects.AddAsync(p, ct);
                    }
                }
            }
        }

        // Dates are relative to today, so the sample set always shows a mix of on-time, finished and late work.
        private static IEnumerable<Project> Sample(DateTime today) => new[]
        {
            Make("Renovasi Gedung Kantor Pusat", "Perbaikan atap dan instalasi listrik gedung utama.", today, -75, -10, 100, true),
            Make("Pembangunan Gudang Logistik", "Gudang 1.200 m2 dengan sistem rak bertingkat.", today, -90, 20, 72),
            Make("Migrasi Server ke Cloud", "Pemindahan aplikasi internal ke infrastruktur cloud.", today, -60, 35, 55),
            Make("Instalasi Panel Surya", "Panel surya 50 kWp untuk kantor cabang.", today, -120, -15, 80),
            Make("Pembangunan Jalan Akses Proyek", "Jalan akses sepanjang 3 km menuju lokasi proyek.", today, -45, 60, 30),
            Make("Digitalisasi Arsip Dokumen", "Pemindaian dan pengindeksan arsip fisik.", today, -150, -30, 100, true),
            Make("Pengadaan Peralatan Laboratorium", "Pengadaan dan kalibrasi peralatan uji.", today, -30, 14, 45),
            Make("Audit Keselamatan Kerja Tahunan", "Audit K3 di seluruh lokasi operasional.", today, -20, 40, 15),
            Make("Pembangunan Pagar dan Pos Jaga", "Pagar keliling dan dua pos jaga baru.", today, -100, -5, 90),
            Make("Upgrade Jaringan Kantor", "Penggantian switch dan penataan ulang kabel jaringan.", today, -50, 25, 65),
            Make("Pelatihan Operator Alat Berat", "Program sertifikasi untuk 40 operator.", today, -80, -40, 100, true),
            Make("Pembangunan Mess Karyawan", "Mess dua lantai untuk 60 karyawan.", today, -10, 150, 8),
        };

        private static Project Make(string name, string description, DateTime today, int startOffset, int endOffset, int progress, bool completed = false) => new()
        {
            ProjectName = name,
            Description = description,
            Status = completed ? ProjectStatus.Completed : ProjectStatus.OnProgress,
            StartDate = today.AddDays(startOffset),
            EndDate = today.AddDays(endOffset),
            Progress = progress,
        };
    }
}
