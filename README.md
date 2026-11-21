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
- Reservasi alat satuan atau satu paket, preview biaya/stok dari server, konfirmasi DP tunai/transfer, dan daftar DP yang diterima.
- Pengambilan melalui Booking: pilih unit siap sesuai reservasi, konfirmasi pelunasan/deposit dan pemeriksaan, lalu aktifkan sewa. Daftar menampilkan sewa dibayar dan deposit diterima dari ledger.
- Pengembalian seluruh unit: QC baik/rusak/hilang, persetujuan denda oleh admin, lalu potong deposit dan catat refund atau pembayaran kekurangan.
- Perpanjangan aktif sebelum jatuh tempo dengan preview biaya/stok, pembatalan pelanggan tanpa refund DP, serta pencatatan hangus per reservasi. Semua mempertahankan riwayat dan melindungi retry.
- Laporan kas transaksi dan penyewaan: filter periode WIB/status, pagination, ringkasan deposit/nonkas, serta CSV seluruh hasil filter (maksimal 10.000 baris).
- Kelola User: buat akun staf, edit nama/peran/aktif dengan pemeriksaan versi, dan ubah password melalui fungsi server Supabase.
- Refresh token terkoordinasi di memori. Logout membersihkan sesi lokal dan meminta pencabutan sesi server.

Login, pembacaan, dan pengelolaan data master sudah menggunakan Supabase. Reservasi dan pembayaran DP sudah tersedia. Pengambilan, pelunasan sisa sewa dan deposit sudah tersedia. Pengembalian/QC, persetujuan denda admin dan penyelesaian deposit juga sudah tersedia di jalur online. Nilai sewa/deposit pada daftar bukan laporan penerimaan kas. Perubahan transaksi dan pembayaran melalui RPC; laporan kas/penyewaan dan pengelolaan akun staf juga sudah terhubung.

Tidak dilakukan konversi ke Visual Studio Designer. UI tetap disusun dalam kode sesuai keputusan pemilik.

## Database Supabase

Proyek `RANTAUGear` sudah memiliki sebelas migrasi, RLS, audit, serta fungsi transaksi. File migrasi lokal memakai versi yang sama dengan cloud; jangan menjalankan ulang migrasi awal pada proyek yang sudah terpasang.

- [Aturan bisnis](docs/BUSINESS_RULES.md): DP 25%, deposit 100%, buffer dua jam, batas ambil 12 jam, denda harian 25% setelah toleransi satu jam.
- [Setup dan kontrak integrasi](docs/SUPABASE_SETUP.md).
- [Pemeriksaan login](docs/ONLINE_LOGIN_QA.md) dan [data master](docs/MASTER_DATA_QA.md), dan [reservasi/DP](docs/BOOKING_DP_QA.md), dan [pengambilan](docs/PICKUP_QA.md), dan [pengembalian/deposit](docs/RETURN_QA.md), dan [perubahan sewa](docs/BOOKING_CHANGES_QA.md), dan [laporan/akun](docs/REPORTS_STAFF_QA.md).

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

## Katalog awal

Supabase diisi 5 kategori, 11 jenis perlengkapan camping, 20 unit ready, serta Paket Camping 4 Orang, Paket Camping 2 Orang dan Paket Pendakian. Tarif mengikuti contoh demo dan dapat diubah dari Katalog/Paket. Tidak menyalin kamera, drone, gas sekali pakai, penyewa, akun demo atau riwayat transaksi. Sleeping bag dan matras masing-masing 4 unit agar paket empat orang sesuai isinya. Nilai penggantian awal 0 berarti belum ditentukan; admin dapat mengisinya kemudian.

Seed supabase/seeds/camping_catalog.sql terpisah dari migrasi, tidak otomatis dijalankan pada semua database, tidak mengubah record yang sudah ada. Ini pengisian awal proyek yang diminta pemilik, bukan inventaris toko yang sudah diverifikasi fisik.

Untuk mencoba: tambahkan pelanggan melalui Pelanggan, buka Booking > Reservasi baru, pilih alat/paket dan jadwal WIB, klik Hitung biaya, lalu konfirmasi DP hanya setelah uang diterima. Simpan reservasi + DP mencatat keduanya sekaligus. Hindari transaksi percobaan di cloud jika tidak ingin riwayat permanen.

Untuk pengambilan: pilih baris pada Booking, klik Proses pengambilan, centang kode unit sesuai jenis/jumlah reservasi, pilih metode pembayaran, lalu konfirmasi uang diterima dan alat siap diserahkan. Jadwal ambil harus sudah tiba, belum melewati batas 12 jam, dan belum jatuh tempo. DP yang diterima sebelumnya sudah dikurangkan; deposit dicatat terpisah dari pelunasan sewa. Saat respons simpan terputus, gunakan Periksa simpan tanpa meminta uang lagi.

Untuk pengembalian: admin/gudang membuka Pengembalian / QC, memilih sewa lalu Pemeriksaan QC. Isi kondisi semua unit; rusak/hilang membutuhkan biaya rupiah bulat dan catatan. Admin melanjutkan Persetujuan denda. Admin/front office memakai Penyelesaian deposit setelah uang refund diserahkan atau kekurangan diterima. Front office mempunyai menu Penyelesaian; tidak bisa mengubah hasil QC atau menyetujui denda.

Status tabel mengikuti tahap: Pemeriksaan QC, Persetujuan admin, Penyelesaian uang, Selesai. Dikembalikan berarti pemeriksaan fisik selesai; uang baru selesai setelah tahap terakhir. Denda terlambat dihitung sampai waktu kembali tersimpan, bukan selama menunggu persetujuan/pembayaran.

Perubahan sewa ada di Booking: pilih baris lalu Perpanjangan, Pembatalan, atau Catat hangus. Perpanjangan hanya sebelum jatuh tempo, memakai tarif snapshot dan tambahan biaya sesuai pembulatan hari, deposit tetap. Hitung biaya dan cek stok sebelum menerima uang. Pembatalan khusus permintaan pelanggan, membutuhkan alasan dan tidak mengembalikan DP. Status hangus dihitung server saat data dimuat; klik Muat ulang untuk melihat status terbaru. Catat hangus hanya menyimpan event/status setelah batas 12 jam, tanpa menggeser deadline atau menghapus DP. Stok sudah mengabaikan reservasi lewat deadline meskipun event hangus belum dicatat.

## Berhenti untuk pemeriksaan pemilik

Versi ini disiapkan untuk diperiksa melalui project Visual Studio. Tidak dilakukan dotnet publish, pembuatan installer, atau pengemasan distribusi EXE. Tahap distribusi menunggu pemeriksaan total dan instruksi lanjutan pemilik.

Mulai dari [panduan pemeriksaan menyeluruh](docs/PRE_DISTRIBUTION_REVIEW.md). Tanda centang manual masih kosong dengan sengaja; pengujian otomatis tidak menggantikan konfirmasi pemilik.

Laporan ada di menu Laporan. Kas transaksi berdasarkan waktu pembayaran; penyewaan berdasarkan jadwal ambil dan status efektif saat query. Deposit bukan pendapatan, pemotongan deposit bukan kas baru. Ekspor mengambil snapshot baru seluruh hasil filter, bukan hanya halaman tabel; CSV disimpan pada D:\elaine\SEM 5\Pemrograman Visual-backup\Laporan.

Kelola User khusus Super Admin. Akun dibuat dengan password yang diketik admin tanpa email undangan; akses diberikan kepada petugas secara pribadi. Jangan mengirim password kepada agent. Pengujian pembuatan akun Auth nyata dan login akun baru masih perlu dicoba sendiri oleh pemilik; agent tidak memegang password pemilik dan tidak meninggalkan akun cloud percobaan.
