# RANTAU Gear

Aplikasi desktop penyewaan perlengkapan outdoor berbasis VB.NET Windows Forms dan .NET 10, dengan database online Supabase.

## Memakai aplikasi

1. Ekstrak seluruh paket Windows x64, lalu jalankan RANTAUGear.exe.
2. Pastikan komputer tersambung ke internet.
3. Login menggunakan akun staf aktif yang dibuat Super Admin melalui Kelola User.

Seluruh file pendukung harus tetap bersama EXE. Paket mandiri tidak memerlukan Visual Studio atau instalasi .NET terpisah. Hasil ekspor CSV berada di subfolder Laporan di sebelah aplikasi.

## Menjalankan dari kode

Diperlukan Windows, .NET SDK 10, dan Visual Studio yang mendukung VB.NET Windows Forms pada .NET 10, atau terminal dengan dotnet CLI.

1. Clone repository dan gunakan branch main.
2. Salin supabase.example.json menjadi supabase.local.json pada root proyek.
3. Isi projectUrl dan publishableKey untuk database aplikasi.
4. Buka RANTAUGear.slnx dan jalankan proyek RANTAUGear, atau gunakan:

    dotnet run --project RANTAUGear/RANTAUGear.vbproj

Konfigurasi lokal diabaikan Git. Gunakan publishable key publik; password database dan secret/service_role key tidak boleh dimasukkan ke aplikasi. Akun login harus mempunyai profil staf aktif pada database yang sama.

## Fitur

- Tiga peran: Super Admin, Front Office, dan Gudang/QC.
- Inventaris, paket bundling, dan data pelanggan.
- Reservasi beserta DP, pengambilan, pelunasan, dan deposit.
- Pengembalian/QC, denda, serta penyelesaian deposit.
- Perpanjangan, pembatalan, dan riwayat transaksi.
- Laporan penyewaan/kas dan ekspor CSV.

[Aturan penyewaan](docs/BUSINESS_RULES.md) · [Kebutuhan database](docs/SUPABASE_SETUP.md)

## Struktur proyek

- RANTAUGear/: kode aplikasi.
- supabase/migrations/: struktur dan fungsi database.
- supabase/functions/: fungsi server pengelolaan akun staf.
- supabase/seeds/: katalog camping awal, opsional.
- tests/ dan supabase/tests/: pengujian aplikasi serta database.

Untuk memakai database yang sudah disiapkan, tidak perlu menjalankan ulang migrasi atau seed.
