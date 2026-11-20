# Integrasi Supabase

## Status

Lima migrasi telah diterapkan ke proyek RANTAUGear `zlwkpiuozuxxynyenhww`, PostgreSQL 17. Login utama sekarang memakai Supabase Auth. Profil/peran staf dibaca dari server; repository async dan halaman baca online sudah terhubung. Form transaksi dan pengelolaan data master belum diaktifkan. Tidak ada fallback ke login demo.

UI tetap berbasis kode; konversi Designer tidak dikerjakan.

## Konfigurasi

1. Salin `supabase.example.json` menjadi `supabase.local.json` di root.
2. Isi `projectUrl` dan publishable key aktif `sb_publishable_...`.
3. File lokal diabaikan Git. Build debug menyalinnya ke sebelah EXE; `CopyToPublishDirectory=Never`.
4. Jika diperlukan, `RANTAUGEAR_CONFIG_PATH` menunjuk file konfigurasi lain secara eksplisit. Path yang salah menimbulkan error konfigurasi, bukan fallback.

Tidak diperlukan password database, secret/service-role key atau personal access token dalam aplikasi. Token login/refresh berada di memori dan tidak ditulis ke konfigurasi. Jangan merekam password/token melalui log, screenshot, source, atau argumen terminal.

## Akun dan akses

Akun Auth dan profil staf terpisah. Akun baru dibuat secara administratif di Supabase Authentication oleh pemilik; password tetap dipegang pengguna. Profil harus memiliki UUID yang sama dengan Auth, nama tampilan, role dan is_active=true. Admin pertama sudah dipasangkan secara terpisah, tidak ditanam sebagai seed/password dalam migrasi.

Role yang dikenal: `super_admin`, `front_office`, `warehouse_qc`. Role tidak dikenal, profil tidak ada, atau akun nonaktif ditolak. Peran diperiksa lagi sebelum pembacaan halaman; perubahan peran mengharuskan login ulang. UI tidak mempercayai user_metadata untuk memberi hak akses.

Semua user pada contoh pengujian adalah fixture lokal/HTTP tiruan. Jangan menerapkan `supabase/tests/bootstrap_local.sql` pada cloud.

## Repository dan sesi

`IRentalRepository` mempertahankan batas akses data dan menambahkan:

- `IsOnline` untuk membedakan jalur online dari pratinjau tes yang eksplisit.
- `GetCurrentUserAsync(CancellationToken)` untuk profil staf aktif.
- `ReadPageAsync(section, offset, pageSize, CancellationToken)` untuk halaman baca yang diizinkan, maksimal 100 baris per permintaan.
- `SignOutAsync(CancellationToken)` untuk logout.

`SupabaseRentalRepository` memakai PostgREST dengan select eksplisit, urutan deterministik dan count=exact. Profile query difilter dengan UUID hasil endpoint Auth `/user`. Nama, nominal dan waktu tidak diambil dari demo. Waktu dikonversi ke WIB. Getter/mutator sinkron lama melempar NotSupportedException jika dipanggil pada repository online; tidak memblokir jaringan melalui .Result/.Wait().

Anggota sinkron `IRentalRepository` dan `DummyRepository` sementara dipertahankan khusus service serta pratinjau tes lama. Adapter transaksi async terstruktur ditambahkan pada tahap transaksi berikutnya. Belum ada tombol UI online yang memanggil mutator sinkron.

`SupabaseSessionManager` mengelola refresh dengan SemaphoreSlim. Refresh dilakukan menjelang token habis, atau sekali ketika pembacaan ditolak 401. Refresh yang ditolak mengakhiri sesi. Tidak ada retry otomatis mutasi. Untuk RPC transaksi berikutnya gunakan request UUID sama dengan payload identik ketika mengulangi tindakan yang sama.

Logout selalu menghapus sesi lokal, termasuk saat jaringan gagal. UI memberitahu jika logout server belum dapat dikonfirmasi. Sebelum gateway/sesi dibuang, main form menunggu request halaman yang telah dibatalkan selesai. Request async tidak memperbarui kontrol yang sudah dibuang.

## Migrasi yang sudah terpasang

1. `20261120151712_rantau_schema.sql`: tabel, RLS, audit dan proteksi history.
2. `20261120151722_rantau_reservations.sql`: pelanggan, kapasitas, reservasi, DP, pembatalan, hangus, denda.
3. `20261120151733_rantau_rental_lifecycle.sql`: pengambilan, perpanjangan, QC, persetujuan dan settlement.
4. `20261120151742_rantau_rental_overview.sql`: view status efektif dengan security_invoker.
5. `20261120151939_rantau_private_rpc_and_indexes.sql`: implementasi privileged dipindah ke schema privat, wrapper API security invoker, indeks foreign key dan RLS ledger idempotensi.

Cloud memiliki 17 tabel public, satu ledger privat dan 11 endpoint RPC. Riwayat migrasi lokal/cloud sudah sinkron. Perubahan schema berikutnya memakai migrasi baru; jangan mengulang migrasi awal. Jika menggunakan CLI, periksa versi/--help, pilih target hosted/linked secara eksplisit dan review dry-run. Pemilik melakukan autentikasi sendiri tanpa mengirim rahasia.

## Kontrak RPC transaksi untuk tahap berikutnya

| RPC | Akses | Fungsi |
| --- | --- | --- |
| check_availability | Staff aktif | Kapasitas jenis alat untuk jadwal dan buffer |
| save_customer | Admin/front office | Tambah/edit pelanggan |
| create_reservation | Admin/front office | Reservasi dan DP atomik |
| cancel_reservation | Admin/front office | Pembatalan pelanggan tanpa refund DP |
| expire_reservations | Admin/front office | Mencatat reservasi lewat batas ambil |
| calculate_late_fine | Staff aktif | Denda berdasarkan snapshot aturan |
| pickup_reservation | Admin/front office | Alokasi unit, sisa sewa dan deposit |
| extend_rental | Admin/front office | Perpanjangan sebelum jatuh tempo dan tambahan pembayaran |
| inspect_return | Admin/QC | Semua unit sekaligus, waktu kembali aktual, usulan denda |
| approve_return_charges | Admin | Persetujuan denda |
| settle_return | Admin/front office | Deposit, refund dan tambahan pembayaran |

p_lines: [{"type_id":1,"quantity":2}]. p_checks: [{"unit_id":1,"condition":"good","charge":0,"notes":""}]. Kondisi damaged/lost wajib charge positif dan catatan. Metode cash/transfer. Waktu dikirim ISO 8601 dengan offset atau UTC.

Penyimpanan kapasitas mengunci baris jenis alat dalam urutan ID dan memeriksa ulang stok di server. Endpoint inventaris berikutnya harus memakai pola penguncian yang sama. CRUD inventaris/profil, koreksi ledger dan refund karena kesalahan toko masih memerlukan endpoint terkontrol sebelum UI penulisannya dibuka.

## Validasi

Pengujian database lokal: `supabase/tests/Test-Database.ps1`; PostgreSQL 18 diperlukan. Cluster localhost/port acak, semua data/log di folder backup, server dihentikan di finally. Fixture Auth lokal bukan Supabase Auth cloud.

Pengujian aplikasi: lihat README dan `docs/ONLINE_LOGIN_QA.md`. Server HTTP tiruan menguji login, role, refresh, pagination dan kegagalan tanpa kredensial asli. Login akun cloud nyata tetap dicoba sendiri oleh pemilik; hasil yang belum diamati tidak boleh dilaporkan lulus.

Pada deployment cloud, akses anonim dan akun tanpa profil staf telah diuji ditolak; publishable key diterima endpoint Auth. Security advisor tidak melaporkan WARN/ERROR setelah hardening. INFO `requests` RLS tanpa policy disengaja (deny-all dan hanya fungsi owner mengakses); INFO unused_index wajar pada database baru. Referensi: https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy ; https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index .
