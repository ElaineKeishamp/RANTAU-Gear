# Integrasi Supabase

## Status

Sebelas migrasi telah diterapkan ke proyek RANTAUGear `zlwkpiuozuxxynyenhww`, PostgreSQL 17. Login utama sekarang memakai Supabase Auth. Profil/peran staf dibaca dari server; repository async dan halaman baca online sudah terhubung. Master, reservasi dan pembayaran DP sudah diaktifkan; pengambilan/pelunasan/deposit juga sudah diaktifkan; pengembalian/QC, persetujuan denda dan penyelesaian deposit sudah diaktifkan. Tidak ada fallback ke login demo.

UI tetap berbasis kode; konversi Designer tidak dikerjakan.

## Konfigurasi

1. Salin `supabase.example.json` menjadi `supabase.local.json` di root.
2. Isi `projectUrl` dan publishable key aktif `sb_publishable_...`.
3. File lokal diabaikan Git. Build debug menyalinnya ke sebelah EXE; `CopyToPublishDirectory=Never`.
4. Jika diperlukan, `RANTAUGEAR_CONFIG_PATH` menunjuk file konfigurasi lain secara eksplisit. Path yang salah menimbulkan error konfigurasi, bukan fallback.

Tidak diperlukan password database, secret/service-role key atau personal access token dalam aplikasi. Token login/refresh berada di memori dan tidak ditulis ke konfigurasi. Jangan merekam password/token melalui log, screenshot, source, atau argumen terminal.

## Akun dan akses

Akun Auth dan profil staf terpisah. Akun baru dapat dibuat Super Admin melalui Kelola User dan Edge Function staff-admin, atau secara administratif melalui dashboard. Password tetap diketik/dipegang pengguna, bukan diberikan kepada agent. Profil harus memiliki UUID yang sama dengan Auth, nama tampilan, role dan is_active=true. Admin pertama sudah dipasangkan secara terpisah, tidak ditanam sebagai seed/password dalam migrasi.

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

9. `20261121023808_rantau_return_ui.sql`: register tahap pengembalian dan rincian QC/keuangan sesuai peran.

10. `20261121030042_rantau_booking_changes.sql`: rincian perubahan, preview perpanjangan, dan mutasi terkonfirmasi/idempoten.

11. `20261121032956_rantau_reports_staff.sql`: snapshot laporan kas/penyewaan, versi profil staf dan ledger pengelolaan akun.

Cloud memiliki 17 tabel public dan tiga ledger privat. Ada 24 wrapper RPC: 22 dapat dipanggil authenticated sesuai peran, staff_admin_finish khusus service_role, dan save_customer lama dinonaktifkan untuk klien. Riwayat migrasi lokal/cloud sudah sinkron. Perubahan schema berikutnya memakai migrasi baru; jangan mengulang migrasi awal. Jika menggunakan CLI, periksa versi/--help, pilih target hosted/linked secara eksplisit dan review dry-run. Pemilik melakukan autentikasi sendiri tanpa mengirim rahasia.

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


## Pengembalian, persetujuan dan deposit

GetReturnDetailsAsync(rentalId,ct) memanggil get_return_details(p_rental_id). Identitas rental, jadwal, kondisi setiap unit, acuan nilai ganti dan hasil QC boleh dibaca staff aktif. Role gudang/QC menerima JSON tanpa financial; fungsi tidak membaca customer/ledger uang pada cabang tersebut. Admin/front office menerima deposit, usulan biaya kondisi, denda terlambat, total, pemotongan deposit, refund dan kekurangan. Kolom financial wajib lengkap dan nominal harus nonnegatif, rupiah bulat, serta seimbang sebelum ditampilkan untuk konfirmasi.

ProcessReturnAsync(requestId,rentalId,action,checks,method,reference,ct) memilih RPC yang sudah ada: inspect_return untuk admin/QC, approve_return_charges untuk admin saja, settle_return untuk admin/front office. p_checks menyebut seluruh unit dengan unit_id, condition good/damaged/lost, charge rupiah, notes. Tidak boleh parsial atau duplikasi; baik harus charge 0, rusak/hilang harus charge positif dan catatan. Tidak ada default kondisi baik yang melewati pemeriksaan.

QC mengembalikan rental menjadi returned dan melepas seluruh assignment. Baik menjadi ready dengan buffer, rusak maintenance, hilang lost. Usulan belum menjadi denda sampai admin menyetujui. Admin melihat hasil QC read-only dan denda terlambat tetap ditambahkan. Sesudah returned, biaya keterlambatan memakai returned_at yang tersimpan, tidak bertambah ketika menunggu persetujuan. Snapshot inspection, denda disetujui dan deposit tidak diubah oleh jalur aplikasi pada tahap settlement.

Settlement hanya setelah persetujuan: deposit_deductions membayar denda, deposit_refund adalah uang keluar, fine_payment adalah uang tambahan diterima. UI menyebut nominal serta arah uang secara eksplisit, termasuk jika tidak ada perpindahan uang. Tidak ada pembayaran online otomatis dan tidak ada insert ledger langsung dari desktop.

return_register memakai security_invoker dan tidak memuat nama/kontak atau jumlah uang. Tahap dibentuk dari state rental serta event charges_approved/settled. Role front office membuka menu Penyelesaian dengan tombol settlement saja; QC hanya pemeriksaan; admin ketiganya. Mutasi memakai RecordId baris terikat, bukan indeks tabel.

Pada respons simpan terputus, unit/checks/metode/referensi dibekukan dan Periksa simpan memakai UUID sama. Tidak memberikan refund atau menerima kekurangan lagi hanya karena respons hilang. Setelah aplikasi ditutup paksa, muat ulang tahap sebelum tindakan baru; pending request tidak ditulis ke disk. Respons composite diterima sebagai array tepat satu baris/object dengan identitas/state yang diverifikasi.


## Perpanjangan, pembatalan dan hangus

GetBookingChangeDetailsAsync memanggil get_booking_change_details(p_rental_id), khusus admin/front office, mengembalikan state tersimpan, server_now, tanggal ambil/due/deadline, snapshot sewa dan DP/deposit yang diterima. Kelayakan awal dialog memakai server_now, bukan jam Windows; mutasi selalu memeriksa lagi di server.

PreviewExtensionAsync memanggil preview_extension(p_rental_id,p_due_at): old_due, new_due, days, rent_amount, additional, deposit. Snapshot tarif sewa/paket dipakai; pembulatan per 24 jam tetap. Tambahan boleh 0 bila durasi baru masih dalam hari tagihan yang sama. Preview menggunakan helper available_quantity yang sudah ada, dengan rental sendiri dikecualikan. Tidak ada penahanan stok atau override.

ChangeBookingAsync memakai change_booking(p_request_id,p_rental_id,p_action,p_due_at,p_reason,p_expected_due,p_expected_additional,p_method,p_reference), action extend/cancel/expire. Semua argumen masuk payload ledger operation booking_change. UUID sama dengan payload/actor berbeda ditolak. Implementasi privat memeriksa staf aktif; wrapper public security_invoker.

Extend mengunci rental serta jenis alat berurutan, seperti extend_rental. Hanya active sebelum jatuh tempo; due baru lebih akhir. Due dasar dan tambahan biaya harus cocok dengan preview terkonfirmasi; perbedaan menyebabkan rollback seluruh tindakan dan pembayaran. Stok diperiksa ulang memakai helper yang sama. Tambahan >0 dicatat rent_payment; 0 tidak membuat payment. Deposit tidak ditambah. Update due, total sewa, event dan request ledger atomik. RPC extend_rental lama tetap ada untuk kompatibilitas; UI memakai change_booking agar nominal terkonfirmasi terjaga.

Cancel meminta alasan dan memanggil logika cancel_reservation yang sudah ada dalam transaksi dengan ledger retry. Hanya reserved sebelum atau tepat pada deadline diperbolehkan; setelah deadline gunakan expiry. Pembatalan pelanggan tidak menambah refund atau menghapus DP. Refund karena kesalahan toko tetap tidak tersedia.

Expire adalah pencatatan per rental terpilih, hanya reserved dan statement_timestamp() > pickup_deadline_at. Event expired mencatat deadline/DP hangus dan rental tetap disimpan. Batas 12 jam tidak berubah karena pelanggan mengabari. Fungsi expire_reservations lama tetap tersedia, tetapi UI tidak menjalankan expiry massal otomatis. Tampilan efektif dan kapasitas sudah mengabaikan reservasi lewat deadline berdasarkan server, terlepas pencatatan event; Muat ulang memperbarui tabel.

Bila respons hilang, form mengunci jadwal/alasan/metode/referensi dan mengulang UUID serta snapshot yang sama. Jangan menerima tambahan uang lagi hanya karena respons hilang. Setelah aplikasi ditutup paksa, cek status/riwayat sebelum tindakan baru; request pending tidak disimpan ke disk. HTTP 401 tidak otomatis mengulang mutasi.


## Laporan

ReadReportAsync(query,offset,pageSize,ct) memanggil read_report(p_kind,p_from,p_to,p_status,p_offset,p_limit), admin/front office saja. Jenis cash/rentals. Batas tanggal inklusif di UI dikonversi server ke interval setengah terbuka dari 00:00 Asia/Jakarta sampai 00:00 sehari setelah tanggal akhir. kind/status/pagination diverifikasi server, limit maksimal 10.000. Fungsi STABLE membuat summary dan rows memakai snapshot query sama. Pagination adalah pembacaan baru; ekspor mengambil satu snapshot baru dengan filter sama dan semua rows hingga batas, tidak mengekspor sebagian secara diam-diam.

Kas: rental_receipts = DP + rent_payment + fine_payment, deposit_received dipisah, deposit_refund/rent_refund adalah kas keluar. cash_net = penerimaan sewa/denda + deposit masuk - dua refund. deposit_applied berasal deposit_deductions dan bersifat nonkas, tidak mengurangi cash_net kedua kali. Saldo deposit awal berasal seluruh penerimaan/refund/pemotongan sebelum awal periode; saldo akhir = awal + deposit diterima periode - refund deposit - pemotongan. Kas transaksi ini tidak mencakup modal/biaya operasional dan bukan laba usaha.

Penyewaan memakai pickup_at dan customer name yang hanya dibaca admin/front office. Status efektif saat query, bukan rekonstruksi status historis pada akhir periode. Riwayat cancelled/expired tetap masuk sesuai filter. CSV UTF-8 BOM memakai semicolon, escaping quote/newline, dan perlindungan formula untuk teks; bilangan negatif tetap numerik. Output folder sesuai permintaan pemilik: D:\elaine\SEM 5\Pemrograman Visual-backup\Laporan.

## Pengelolaan staf

ReadStaffAsync memanggil list_staff: email Auth, nama, peran, aktif dan row_version, hanya admin. SaveStaffAsync update memakai update_staff dengan UUID, target dan versi. Self-disable/self-demotion dilarang; minimal admin aktif dipertahankan dan update diserialisasi. is_active=false segera ditolak pemeriksaan profil/RLS pada request berikutnya, meskipun token lama belum kedaluwarsa. Nama sendiri pada header sesi dapat mengikuti sesi login baru.

Create/password memanggil functions/v1/staff-admin. Platform verify_jwt=true dipertahankan pada deployment/config. Handler memverifikasi token ke Auth /user, lalu staff_admin_prepare dengan token pengguna memeriksa profil admin aktif. Kunci SUPABASE_SERVICE_ROLE_KEY hanya dibaca pada runtime Edge Function, tidak pada desktop/repo. Fungsi memakai Auth Admin REST untuk create atau update password; tidak memanggil invite/recovery atau mengirim email. email_confirm=true untuk akun staf yang dibuat admin.

staff_admin_prepare menyimpan hanya requestId/actor/operasi/email/nama/peran/target yang relevan, bukan password atau hash password. Create memakai app_metadata penanda request/actor yang hanya bisa ditulis server. Jika Auth berhasil tetapi respons/finish hilang, prepare berikutnya menemukan akun dengan penanda sama, lalu finish membuat profil sekali. Email Auth yang sudah ada dengan penanda berbeda ditolak; tidak mengambil alih akun lama.

staff_admin_finish hanya diberi EXECUTE ke service_role; desktop tidak dapat menyelesaikan operasi privileged secara langsung. Handler memanggilnya setelah Auth berhasil dan finish memeriksa aktor masih admin aktif. Audit PASSWORD_CHANGED/STAFF_CREATED tidak memuat password. Password tersedia sementara di memori selama request/retry; tidak ditulis ke konfigurasi/ledger/log. Retry UUID yang selesai tidak mengganti password lagi; password baru adalah nilai pada operasi berhasil pertama. Reset password bukan klaim pencabutan semua access token aktif; uji login baru dengan password lama/baru dilakukan dengan logout dahulu.

Auth dan Postgres adalah dua sistem, sehingga create bisa sementara memiliki Auth tanpa staff profile bila finish gagal. Akun tanpa profil tidak dapat masuk aplikasi; gunakan Periksa simpan dengan UUID yang sama. Jangan menghapus Auth/profile/history untuk menutupi hasil yang belum terkonfirmasi. Setelah aplikasi ditutup paksa, periksa daftar dan laporan hasil sebelum mencoba ulang; pending request tidak disimpan di disk.

Deploy sumber supabase/functions/staff-admin dengan verify_jwt=true sesuai config. Tidak perlu mengirim password database atau service key kepada agent. Edge source tidak punya dependensi npm eksternal. Pengujian Node memakai mock; anonymous hosted ditolak HTTP 401. Valid JWT + pembuatan Auth nyata masih perlu konfirmasi manual pemilik melalui aplikasi.
