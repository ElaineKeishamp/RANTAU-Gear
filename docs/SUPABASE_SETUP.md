# Integrasi Supabase

## Status

Delapan migrasi telah diterapkan ke proyek RANTAUGear `zlwkpiuozuxxynyenhww`, PostgreSQL 17. Login utama sekarang memakai Supabase Auth. Profil/peran staf dibaca dari server; repository async dan halaman baca online sudah terhubung. Master, reservasi dan pembayaran DP sudah diaktifkan; pengambilan/pelunasan/deposit juga sudah diaktifkan; pengembalian belum diaktifkan. Tidak ada fallback ke login demo.

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
- `ReadMasterAsync(kind, offset, pageSize, CancellationToken)` untuk record master bertipe, termasuk row_version.
- `SaveMasterAsync(requestId, record, CancellationToken)` untuk RPC atomik save_master_data. Tidak ada retry otomatis.

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

6. `20261121014656_rantau_master_data.sql`: editor master, row_version, ledger retry privat, proteksi kapasitas dan komponen paket nonaktif.

7. `20261121020414_rantau_booking_dp.sql`: preview server, guard nominal DP dan view register security_invoker.

8. `20261121022025_rantau_pickup_ui.sql`: rincian pengambilan dan jumlah ledger sewa/deposit pada register.

Cloud memiliki 17 tabel public dan dua ledger privat. Ada 15 wrapper RPC; save_customer lama dinonaktifkan untuk klien, sehingga 14 dapat dipanggil authenticated sesuai peran. Riwayat migrasi lokal/cloud sudah sinkron. Perubahan schema berikutnya memakai migrasi baru; jangan mengulang migrasi awal. Jika menggunakan CLI, periksa versi/--help, pilih target hosted/linked secara eksplisit dan review dry-run. Pemilik melakukan autentikasi sendiri tanpa mengirim rahasia.

## Kontrak RPC transaksi untuk tahap berikutnya

| RPC | Akses | Fungsi |
| --- | --- | --- |
| check_availability | Staff aktif | Kapasitas jenis alat untuk jadwal dan buffer |
| save_master_data | Admin; pelanggan juga front office | Tambah/edit master dengan versi dan UUID retry |
| save_customer | Tidak tersedia untuk klien | Kontrak lama, digantikan save_master_data |
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

Penyimpanan kapasitas mengunci baris jenis alat dalam urutan ID dan memeriksa ulang stok di server. Endpoint inventaris berikutnya harus memakai pola penguncian yang sama. Pengelolaan profil, koreksi ledger dan refund karena kesalahan toko masih memerlukan endpoint terkontrol sebelum UI penulisannya dibuka.

## Validasi

Pengujian database lokal: `supabase/tests/Test-Database.ps1`; PostgreSQL 18 diperlukan. Cluster localhost/port acak, semua data/log di folder backup, server dihentikan di finally. Fixture Auth lokal bukan Supabase Auth cloud.

Pengujian aplikasi: lihat README dan `docs/ONLINE_LOGIN_QA.md`. Server HTTP tiruan menguji login, role, refresh, pagination dan kegagalan tanpa kredensial asli. Login akun cloud nyata tetap dicoba sendiri oleh pemilik; hasil yang belum diamati tidak boleh dilaporkan lulus.

Pada deployment cloud, akses anonim dan akun tanpa profil staf telah diuji ditolak; publishable key diterima endpoint Auth. Tidak ada ERROR advisor schema; WARN Auth leaked-password protection belum aktif, detail pada BOOKING_DP_QA.md. INFO `requests` RLS tanpa policy disengaja (deny-all dan hanya fungsi owner mengakses); INFO unused_index wajar pada database baru. Referensi: https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy ; https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index .


## Pengelolaan data master

Urutan pengisian awal: kategori → jenis alat beserta tarif/nilai penggantian → unit fisik dengan kode unik dan konfirmasi siap → paket opsional → pelanggan. Admin membuka Katalog/Paket; admin dan front office membuka Pelanggan. Gudang/QC tetap membaca unit melalui halaman yang diizinkan, tanpa editor master.

save_master_data menerima p_request_id UUID, p_entity (categories/equipment_types/equipment_units/packages/customers), p_id null untuk tambah, p_version null untuk tambah atau row_version terbaru untuk edit, dan p_values object. Semua nominal rupiah bulat. Return berupa ID bigint. Perubahan berhasil dicatat pada audit yang sudah ada; request ledger bersifat privat dan tidak dapat dibaca klien.

Jika row_version berubah, edit ditolak: tutup form, muat ulang, lalu edit kembali. Jika respons simpan terputus, editor mengunci payload dan tombol Simpan ulang memakai UUID serta payload yang sama untuk memeriksa hasil tanpa menduplikasi. UUID tidak boleh dipakai ulang untuk tindakan berbeda. Retry hanya tersimpan sepanjang dialog terbuka; setelah aplikasi ditutup paksa, periksa daftar sebelum menambah ulang. Mutasi tidak diulang otomatis saat 401.

Tidak ada penghapusan master. Gunakan status nonaktif; unit memakai kondisi retired. Kategori dengan jenis aktif tidak dapat dinonaktifkan. Jenis dalam paket aktif juga harus dilepas dari paket terlebih dahulu. Pengurangan kapasitas ditolak bila mengganggu reservasi. Kode/jenis unit yang memiliki riwayat tidak dapat diganti. Status rented/lost mengikuti transaksi, dan mengubah unit menjadi ready memerlukan konfirmasi QC. Harga yang diubah tidak mengubah snapshot transaksi lama. Komponen paket yang dilepas diberi is_active=false; reservasi baru hanya memakai komponen aktif.

Penulisan langsung tabel master oleh anon/authenticated dicabut. Wrapper public adalah security invoker; implementasi private memeriksa akun aktif dan peran sebelum bekerja. Uji fixture dan bootstrap lokal tidak boleh dijalankan pada cloud.


## Kontrak reservasi dan DP

GetReservationOptionsAsync mengambil semua halaman pelanggan/jenis/paket aktif untuk admin/front office, tanpa endpoint editor master yang khusus admin. PreviewReservationAsync memanggil preview_reservation(p_pickup_at,p_due_at,p_lines,p_package_id), menghasilkan daily_price, rental_days, rent_amount, down_payment_due, deposit_due, pickup_deadline_at. Preview memakai helper kapasitas yang sudah ada, tanpa menahan stok.

ReserveWithDpAsync memanggil reserve_with_dp(p_request_id,p_customer_id,p_pickup_at,p_due_at,p_lines,p_method,p_reference,p_package_id,p_expected_rent,p_expected_dp,p_expected_deposit). Wrapper memanggil logika create_reservation, lalu membandingkan nominal terhadap ringkasan terkonfirmasi. Perbedaan harga/aturan menyebabkan seluruh rental, DP, event dan ledger rollback. Kapasitas tetap diperiksa atomik dengan penguncian yang sudah ada, tanpa override baru.

Perubahan pelanggan, jadwal, alat, paket, metode atau referensi membatalkan preview dan konfirmasi uang. UI hanya menyimpan setelah checkbox DP diterima dicentang. Paket satu bundel dengan isi tetap; beberapa paket sekaligus ditunda. Alat satuan boleh beberapa jenis/jumlah. Waktu dikirim offset WIB +07:00 secara eksplisit.

Respons simpan hilang: payload dibekukan, tombol Periksa simpan memakai UUID sama, sehingga tidak ada DP kedua. Setelah aplikasi ditutup paksa, cek daftar sebelum membuat transaksi ulang; pending request tidak disimpan di disk. HTTP 401 pada mutasi tidak diulang otomatis.

reservation_register adalah view security_invoker dari rental_overview, pelanggan dan jumlah ledger down_payment. DP diterima adalah uang tersimpan, bukan DP yang seharusnya dibayar. RLS pelanggan/pembayaran tetap berlaku dan gudang/QC tidak dapat membaca register pelanggan ini.


## Pengambilan dan pembayaran saat ambil

GetPickupDetailsAsync(rentalId, ct) memanggil get_pickup_details(p_rental_id). Server memeriksa staff admin/front office aktif, status reserved, waktu ambil sudah tiba, maksimal batas 12 jam dan sebelum due. Hasil berisi identitas/nomor rental, nama pelanggan, due/deadline, snapshot biaya, uang yang sudah dibayar, rent_balance, deposit_balance, lines menurut name_snapshot dan unit ready tanpa assignment aktif atau buffer pemeriksaan yang belum selesai. Ini pembacaan rincian, bukan penguncian atau penahanan unit.

PickupReservationAsync(requestId,rentalId,unitIds,method,reference,ct) memakai RPC pickup_reservation yang sudah ada. UI memvalidasi jumlah setiap jenis, memisahkan konfirmasi uang dari konfirmasi pemeriksaan unit, dan tidak meminta DP kedua. Nilai rupiah dihitung dari snapshot rental serta ledger pembayaran di server. Biaya master terbaru tidak mengubah tagihan reservasi. Selama reserved, jalur aplikasi tidak mengubah snapshot biaya atau mencatat pelunasan lain; transaksi pengambilan serentak yang kedua ditolak karena status sudah berubah.

RPC memeriksa ulang jadwal, status, jumlah/jenis, unit siap dan buffer. Dalam satu transaksi: mencatat rent_payment dan deposit_received yang masih kurang, membuat unit_assignments, menandai unit rented dan rental active. Tidak ada update unit atau insert pembayaran langsung dari client. Sisa sewa/deposit berasal dari server, tidak ditulis sebagai konstanta.

Saat hasil simpan tidak pasti, form membekukan unit IDs/metode/referensi dan menggunakan UUID yang sama saat Periksa simpan. HTTP 401 tidak mengulang pembayaran otomatis. Setelah aplikasi ditutup paksa, muat ulang daftar/status sebelum tindakan baru. Hasil composite RPC diterima sebagai array satu baris atau object dan diperiksa identitasnya.

Register Booking sekarang juga menampilkan rent_paid (DP + pelunasan sewa) dan deposit_received (penerimaan deposit bruto, belum dikurangi refund di tahap pengembalian). Deposit tetap terpisah dari pendapatan. RecordId tersembunyi terikat ke baris DataTable sehingga sorting tidak memindahkan tindakan ke reservasi lain.
