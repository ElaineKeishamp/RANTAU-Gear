# Integrasi Supabase

## Status implementasi

Migrasi dan transport HTTP async sudah disiapkan dan diuji pada PostgreSQL lokal. Belum diterapkan ke Supabase pengguna: Project URL, proyek tujuan, serta akses akun belum tersedia. Aplikasi utama tetap memakai DummyRepository dan aturan demo lama; transport baru belum dipasang ke form maupun menggantikan IRentalRepository.

Pekerjaan selanjutnya: hubungkan proyek Supabase, terapkan migrasi melalui tool/CLI, buat akun staff dan profil, implementasikan adapter repository async beserta DTO, hubungkan form, lalu uji end-to-end dengan akun cloud. Jangan menyambungkan RPC secara blocking ke interface sinkron yang lama. Evolusikan IRentalRepository dan pemanggilnya bersama agar batas akses data tetap satu.

## Yang diperlukan dari pemilik

- Link dashboard proyek: https://supabase.com/dashboard/project/PROJECT_REF dan konfirmasi database kosong atau sudah berisi data.
- Hubungkan akses Supabase melalui integrasi akun yang tersedia. Link saja tidak memberikan izin admin.
- Project URL dan publishable key sb_publishable_... untuk aplikasi. Tidak memerlukan password database, secret key, service_role key, atau personal access token di source/percakapan.
- Pemilik login sendiri pada proses yang meminta autentikasi. Jangan menyimpan password/token di README, git remote, screenshot, output terminal, atau source.

## Migrasi

Urutan berkas di supabase/migrations:

1. 202611200001_schema.sql: tabel, relasi, RLS, audit dan proteksi history.
2. 202611200002_reservations.sql: pelanggan, kapasitas, reservasi, DP, pembatalan, hangus, denda.
3. 202611200003_rental_lifecycle.sql: pengambilan, perpanjangan, QC, persetujuan dan settlement.
4. 202611200004_rental_overview.sql: status efektif terlambat/hangus dengan security_invoker dan RLS tabel asal.

Target awal adalah proyek khusus RANTAU Gear yang kosong. Jangan menjalankan migrasi awal ini membabi buta pada proyek dengan tabel bernama sama. Jangan menerapkan ulang migrasi yang sudah tercatat. Tidak ada DROP tabel atau penghapusan data dalam migrasi.

Jalur utama menggunakan migrasi Supabase, bukan mengedit schema satu per satu melalui Table Editor. Jika CLI tersedia, pemilik menjalankan supabase login sendiri, lalu supabase link --project-ref PROJECT_REF. Review dengan supabase db push --dry-run sebelum supabase db push. Token/password bila diminta diisi sendiri pada terminal; jangan dipasang dalam argumen perintah atau source. SQL Editor hanya fallback terarah; perlu rekonsiliasi migration history sebelum beralih ke CLI, bukan menjalankan kedua jalur berulang.

## Akun pertama

Tidak ada akun/password produksi yang dibuat oleh migrasi. Buat pengguna melalui Supabase Authentication secara administratif, lalu isi profil berdasarkan UUID Auth yang benar. Contoh SQL berikut memakai placeholder, bukan akun siap pakai:

```sql
insert into public.staff_profiles(id,display_name,role)
values ('UUID_DARI_AUTH_USERS', 'Nama administrator', 'super_admin');
```

Role lain: front_office dan warehouse_qc. Jangan menyalin password demo ke database. Staff_profiles hanya menyimpan profil dan peran; password ditangani Supabase Auth. Self-signup publik sebaiknya dinonaktifkan untuk aplikasi petugas toko. Pembuatan/pengubahan profil dilakukan melalui kanal admin tepercaya; tidak ada hak UPDATE role dari klien desktop.

## Konfigurasi aplikasi

Salin supabase.example.json menjadi supabase.local.json dan isi sendiri Project URL serta publishable key. File lokal diabaikan Git. SupabaseOptions.Load menerima path eksplisit; konfigurasi belum diaktifkan otomatis oleh Program.vb. Gateway menolak secret key dan hanya menerima sb_publishable_... serta URL HTTPS tanpa kredensial/path tambahan.

Gateway menyediakan SignInAsync, RefreshSessionAsync, RpcAsync, SignOutAsync dengan CancellationToken. Token hanya berupa objek sesi di memori; tidak ditulis ke file. Pemanggil bertanggung jawab menyimpan hasil refresh terbaru, menghapus sesi saat logout, serta menangani HttpRequestException/timeout. Tidak ada retry otomatis transaksi; gunakan request UUID yang sama dengan payload identik untuk retry tindakan yang sama, dan UUID baru untuk tindakan lain.

Header apikey berisi publishable key. Header Authorization pada RPC berisi access token pengguna, bukan publishable key atau secret key. Pesan error HTTP tidak menampilkan body mentah server.

## Kontrak RPC

| RPC | Akses | Fungsi |
| --- | --- | --- |
| check_availability | Staff aktif | Kapasitas jenis alat untuk jadwal dan buffer |
| save_customer | Admin/front office | Tambah/edit pelanggan |
| create_reservation | Admin/front office | Reservasi dan pencatatan DP atomik |
| cancel_reservation | Admin/front office | Pembatalan pelanggan tanpa refund DP |
| expire_reservations | Admin/front office | Mencatat reservasi lewat batas ambil |
| calculate_late_fine | Staff aktif | Estimasi/final denda berdasarkan snapshot |
| pickup_reservation | Admin/front office | Alokasi unit, sisa sewa dan deposit |
| extend_rental | Admin/front office | Jadwal dan tambahan pembayaran, sebelum jatuh tempo |
| inspect_return | Admin/QC | Semua unit sekaligus, waktu kembali aktual, usulan denda |
| approve_return_charges | Admin | Menyetujui tagihan rusak/hilang dan denda terlambat |
| settle_return | Admin/front office | Pemotongan deposit, refund, tambahan pembayaran |

p_lines: [{"type_id": 1, "quantity": 2}]. p_checks: [{"unit_id": 1,"condition":"good","charge":0,"notes":""}]. Kondisi lain damaged/lost wajib charge positif serta catatan. Metode cash/transfer. Semua waktu dikirim ISO 8601 dengan offset atau UTC, ditampilkan dalam WIB.

Pemeriksaan kapasitas di layar bukan jaminan sampai disimpan. RPC reservasi/perpanjangan mengunci baris jenis alat dengan urutan ID, lalu memeriksa ulang sebelum menulis. Endpoint/data master inventaris berikutnya wajib memakai pola penguncian yang sama; jangan membuka UPDATE status unit langsung ke klien. CRUD unit, pengaturan profil admin, koreksi ledger dan refund kesalahan toko masih perlu endpoint terkontrol sebelum UI online lengkap.

## Pengujian

Jalankan dari root proyek pada Windows dengan PostgreSQL 18 terpasang:

```powershell
.\supabase\tests\Test-Database.ps1
```

Runner membuat cluster baru, hanya mendengar 127.0.0.1 pada port acak, memasang stand-in Auth khusus tes, menerapkan seluruh migrasi, menjalankan assertions serta dua koneksi bersamaan. Server dihentikan di finally. Semua output/data/log di D:\elaine\SEM 5\Pemrograman Visual-backup. Auth tiruan dan fixture di supabase/tests tidak boleh diterapkan ke Supabase cloud. Pengujian ini tidak membuktikan konfigurasi Auth/PostgREST cloud sudah benar; pengujian cloud tetap wajib.

```powershell
dotnet build RANTAUGear.slnx --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation'
dotnet run --project tests/RantauGear.Checks.vbproj --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation' -- --check
```

Referensi: https://supabase.com/docs/guides/getting-started/api-keys ; https://supabase.com/docs/guides/database/postgres/row-level-security ; https://supabase.com/docs/guides/database/functions ; https://supabase.com/docs/guides/deployment/database-migrations
