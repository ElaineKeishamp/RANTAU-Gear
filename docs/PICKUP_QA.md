# QA pengambilan, pelunasan dan deposit

Tanggal: 21 November 2026. VB.NET WinForms .NET 10, folder kerja D:\elaine\SEM 5\RANTAU Gear.

## Bukti validasi

- PASS: 233 pemeriksaan aplikasi/UI (baseline 193 + 40 pickup). Admin/front office memuat rincian; jumlah unit salah/kosong ditolak; perubahan pilihan menghapus konfirmasi; uang dan pemeriksaan dikonfirmasi terpisah.
- PASS: respons hilang setelah commit mengunci payload dan retry memakai UUID sama, tanpa pembayaran kedua. Penolakan karena unit/jadwal berubah tidak mencatat uang, mengharuskan buka rincian baru. QC ditolak oleh repository.
- PASS: sorting tabel Booking lalu memilih baris tetap memproses RecordId yang benar, bukan indeks baris. Setelah simpan daftar diperbarui, status Disewa, sewa dibayar dan deposit diterima ditampilkan.
- PASS: PostgreSQL lokal, 130 pemeriksaan (67 rental + 23 master + 20 booking + 20 pickup), ditambah race dua koneksi untuk stok terakhir. Pengujian pickup memeriksa snapshot harga tetap setelah master berubah, unit maintenance ditolak, assignment exact, rent/deposit terpisah, retry dan akses QC/anon. Log: D:\elaine\SEM 5\Pemrograman Visual-backup\pickup-db.log.
- PASS: migrasi hosted 20261121022025_rantau_pickup_ui diterapkan, versi lokal sama; delapan migrasi.
- PASS: smoke SQL hosted memakai role authenticated/profil admin yang sudah ada. Buat reservasi rollback, baca rincian, ambil unit, terima sisa sewa/deposit, ulang request, verifikasi tepat 3 pembayaran termasuk DP awal. Semua QA di-ROLLBACK; pelanggan/rental/pembayaran tetap 0, katalog 20 unit ready tetap utuh.
- Batas bukti: GUI memakai HTTP fixture. Login pemilik pernah dikonfirmasi, tetapi pengambilan GUI dengan password pemilik belum dicoba. Password pemilik tidak diminta atau disimpan. SQL hosted bukan klaim end-to-end GUI/PostgREST.
- Screenshot fixture ditinjau: D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-pickup-ui\pickup-ready-fixture.png dan pickup-minimum-fixture.png. Tidak ada data penyewa nyata pada screenshot.
- Advisors: tidak ada ERROR schema/RLS baru. WARN Auth [leaked password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) belum aktif, sama dengan sebelumnya. INFO [RLS ledger tanpa policy](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy) disengaja deny-all; [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index) tetap dicatat, tanpa menghapus indeks karena database baru.

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot pengambilan 900x850 dan minimum 800x730 ditinjau; ringkasan uang dan kedua checkbox terlihat; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total master dari count=exact server, bukan angka buatan.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Proses pengambilan membuka rincian reservasi terpilih; centang unit, dua konfirmasi, metode dan Simpan pengambilan dijalankan. Sorting tetap memakai bound ID. Periksa simpan memakai UUID sama tanpa pelunasan/deposit ganda; baseline tetap lulus.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 233 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: footer menyebut reservasi/pengambilan online dan pengembalian/QC belum aktif; batas validasi hosted/fixture dinyatakan di atas.
- R-37 PASS: arah visual memakai DESIGN proyek sebelum implementasi; tidak mengganti framework atau pola UI.
- R-38 PASS: produksi membaca Supabase; fixture terpisah di tests dengan nama screenshot fixture dan tidak ditanam ke cloud.
- R-39 PASS: kontrol native memiliki label/AccessibleName, enabled/disabled, fokus dan status teks; tidak mengandalkan warna saja. ARIA/HTML tidak berlaku untuk WinForms.
- R-40 PASS: tidak menambah media/font remote/animasi atau angka performa; status memiliki ruang tetap.

### Purpose Gate

- R-01 PASS: tidak menambah gradient/glow.
- R-04 PASS: tidak menambah ikon dekoratif.
- R-06 PASS: Segoe UI dipertahankan untuk kontrol Windows dan keterbacaan tabel.
- R-07 PASS: tidak menambah pola background.
- R-08 PASS: tidak menambah panah dekoratif pada tombol.
- R-09 PASS: tidak menambah badge pemasaran.
- R-10 PASS: tidak memakai glassmorphism.
- R-12 PASS: tidak menambah shadow.
- R-13 PASS: tidak menambah glow.
- R-14 PASS: tabel dan form mengikuti struktur record master, bukan kartu seragam.
- R-19 PASS: tanpa animasi baru, MOTION 1 tetap.
- R-22 PASS: tidak menambah ilustrasi generik.

### Liveliness

- Dials PASS: ENERGY 2 / RHYTHM 2 / MOTION 1 mengikuti DESIGN dan screenshot.
- Fokus PASS: daftar berfokus pada tabel; dialog berfokus pada input dan tombol Simpan.
- Ruang PASS: toolbar/status terpisah dari tabel, footer dialog terpisah dari scroll field.
- Aksen PASS: hijau pada Tambah/Simpan; merah pada pesan kesalahan, bukan dekorasi.
- Identitas PASS: kode unit, tarif rupiah, QC, isi paket dan status aktif membentuk register rental.
- Design Read PASS: warna, font dan pola register dipertahankan dari arah proyek.

### Craftsmanship dan quality locks

- C-1 PASS: daftar/form melayani lima jenis master yang dibutuhkan sebelum transaksi.
- C-2 PASS: pengambilan melalui toolbar, pilihan unit, dua konfirmasi, kondisi/jadwal berubah dan retry diuji; baseline reservasi/master lulus.
- C-3 PASS: tidak menambah bagian pengisi/pemasaran.
- C-4 PASS: hak tiga role, stale edit, koneksi gagal, cancellation dan disposal ikut pengujian; batas DPI/perangkat dicatat.
- C-5 PASS: hosted rollback dibedakan dari fixture dan konfirmasi manual pemilik.
- R-05 PASS: shell meja rental tetap, tanpa template landing page.
- R-11 PASS: field/grid native dan radius tombol yang sudah ada dipertahankan.
- R-15 PASS: aksi bernama Tambah, Edit pilihan, Simpan, Simpan ulang, Muat ulang.
- R-16 PASS: copy tanpa klaim pemasaran.
- R-20 PASS: struktur kategori/jenis/unit, kondisi QC dan komponen paket mengikuti rental outdoor.
- R-21 PASS: workspace terang mendukung penggunaan petugas toko.
- R-29 PASS: memakai palet Theme yang sama.
- R-30 PASS: tidak mengadopsi tampilan produk lain.
- R-31 PASS: hijau untuk aksi, register untuk daftar, form native untuk pengisian; alasan tercatat di Design Read.

Register Booking memakai lebar minimum kolom agar tanggal/nominal terbaca; pada jendela sempit pengguna menggulir tabel secara horizontal, bukan halaman aplikasi.

## Batas pekerjaan

Pengembalian/QC, persetujuan denda, pemotongan/refund deposit, pembayaran kekurangan, perpanjangan, pembatalan/hangus melalui UI dan laporan kas masih tahap berikutnya. Deposit diterima pada Booking adalah penerimaan bruto, bukan saldo setelah refund. Installer dan konversi Designer tetap ditunda.
