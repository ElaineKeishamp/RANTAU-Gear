# RANTAU Gear Rental

Aplikasi desktop VB.NET Windows Forms, .NET 10 di Windows, untuk petugas toko rental outdoor. Login utama menggunakan Supabase Auth dan profil staf online. Tidak ada akun pelanggan atau upload foto KTP.

## Menjalankan

Buka `RANTAUGear.slnx` di Visual Studio dan jalankan proyek `RANTAUGear`. Dari terminal:

```powershell
dotnet run --project RANTAUGear/RANTAUGear.vbproj --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation'
```

Siapkan `supabase.local.json` di root proyek berdasarkan `supabase.example.json`. Gunakan Project URL dan publishable key, bukan secret/service-role key atau password database. File lokal diabaikan Git dan disalin ke output debug; tidak ikut publish. Alternatif: tetapkan `RANTAUGEAR_CONFIG_PATH` ke path konfigurasi sendiri. Jika konfigurasi hilang atau salah, aplikasi menampilkan error dan tidak beralih ke demo.

Masuk menggunakan email/password akun Supabase yang sudah memiliki `staff_profiles` aktif. Password diketik sendiri oleh pengguna dan tidak disimpan ke file.

## Yang tersedia saat ini

- Login async dengan validasi identitas server, profil, akun aktif, dan peran.
- Menu berdasarkan role Super Admin, front office, atau gudang/QC.
- Pembacaan data online melalui `IRentalRepository.ReadPageAsync`, 25 baris per halaman, jumlah total dari server, muat ulang, dan pembatalan.
- Tambah/edit/nonaktifkan kategori, jenis alat, paket, dan pelanggan; tambah/edit kondisi unit fisik. Admin mengelola inventaris, front office juga dapat mengelola pelanggan.
- Daftar transaksi, unit, paket, dan profil staf sesuai hak akses. Tanggal ditampilkan dalam WIB.
- Loading, empty state, koneksi gagal, sesi berakhir, dan penanda data sebelumnya ketika refresh gagal.
- Refresh token terkoordinasi di memori. Logout membersihkan sesi lokal dan meminta pencabutan sesi server.

Login, pembacaan, dan pengelolaan data master sudah menggunakan Supabase. Form reservasi, pembayaran, dan pengembalian/QC belum tersedia di jalur online. Nilai sewa/deposit pada daftar bukan laporan penerimaan kas. Database dan fungsi transaksinya sudah terpasang, tetapi form transaksi akan dihubungkan pada tahap berikutnya.

Tidak dilakukan konversi ke Visual Studio Designer. UI tetap disusun dalam kode sesuai keputusan pemilik.

## Database Supabase

Proyek `RANTAUGear` sudah memiliki enam migrasi, RLS, audit, serta fungsi transaksi. File migrasi lokal memakai versi yang sama dengan cloud; jangan menjalankan ulang migrasi awal pada proyek yang sudah terpasang.

- [Aturan bisnis](docs/BUSINESS_RULES.md): DP 25%, deposit 100%, buffer dua jam, batas ambil 12 jam, denda harian 25% setelah toleransi satu jam.
- [Setup dan kontrak integrasi](docs/SUPABASE_SETUP.md).
- [Pemeriksaan login](docs/ONLINE_LOGIN_QA.md) dan [data master](docs/MASTER_DATA_QA.md).

## Pemeriksaan

```powershell
dotnet build RANTAUGear.slnx --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation'
dotnet run --project tests/RantauGear.Checks.vbproj --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation' -- --check
dotnet run --project tests/RantauGear.Checks.vbproj --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation' -- --ui-check
.\supabase\tests\Test-Database.ps1
```

`--ui-check` menjalankan form dengan server HTTP tiruan dan menyimpan screenshot QA ke folder backup. Tidak memakai password asli atau menanam data contoh ke cloud. Runner database memakai cluster PostgreSQL lokal terisolasi, kemudian menghentikannya.

Tanpa argumen, proyek `tests` juga dapat membuka pratinjau demo lokal. `DummyRepository`, akun contoh, dan service rental sinkron dipertahankan untuk pengujian regresi; tidak digunakan sebagai fallback login atau transaksi pada aplikasi utama.

Installer/distribusi EXE tetap ditunda. Hasil build debug digunakan untuk pengujian.
