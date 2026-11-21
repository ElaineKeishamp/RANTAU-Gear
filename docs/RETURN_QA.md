# QA pengembalian, denda dan penyelesaian deposit

Tanggal: 21 November 2026. VB.NET WinForms .NET 10, folder kerja D:\elaine\SEM 5\RANTAU Gear.

## Bukti validasi

- PASS: 322 pemeriksaan aplikasi/UI (baseline pickup 233, satu menu front office baru, 88 pemeriksaan pengembalian). QC memerlukan kondisi semua unit; biaya/catatan wajib untuk rusak/hilang. Perubahan input membatalkan konfirmasi. Persetujuan dan settlement tidak mengedit hasil QC.
- PASS: skenario kehilangan (150.000 + terlambat 25.000, deposit 100.000, tambahan 75.000), semua baik/on-time (refund 100.000), rusak sebagian (refund 35.000), denda tepat sebesar deposit (tanpa perpindahan uang), serta respons hilang pada ketiga RPC. UUID/payload retry sama dan tindakan/uang hanya dicatat sekali.
- PASS: QC tidak menerima financial dalam rincian; front office menerima financial tetapi tidak dapat menyetujui denda. QC tidak bisa refund/menerima kekurangan. Input uang tidak lengkap atau tidak seimbang ditolak sebelum konfirmasi.
- PASS: klik toolbar QC, approval dan settlement sampai daftar menunjukkan Selesai. Status yang berubah menolak simpan dan meminta rincian baru. Baseline ID terikat setelah sorting tetap lulus.
- PASS: database lokal 154 pemeriksaan (67 rental, 23 master, 20 booking, 20 pickup, 24 return), plus race dua koneksi untuk stok terakhir. Role QC tidak melihat identitas/financial pada rincian, pengembalian parsial ditolak, kerusakan/hilang ditambah keterlambatan, refund penuh/parsial dan kekurangan sesuai ledger, retry tidak menduplikasi. Log: D:\elaine\SEM 5\Pemrograman Visual-backup\return-db.log.
- PASS: migrasi 20261121023808_rantau_return_ui diterapkan pada hosted dan disimpan lokal dengan versi sama. Sembilan migrasi.
- PASS: smoke SQL hosted melalui role authenticated dan admin yang ada: reservasi, pickup, QC dua unit dengan satu hilang, persetujuan dan tambahan pembayaran; skenario terpisah semua baik menghasilkan refund penuh. Ketiga tahap diulang dengan UUID sama dan ledger tetap tepat. Semua QA di-ROLLBACK: pelanggan/rental/pembayaran tetap 0, katalog tetap 20 unit ready.
- Batas bukti: GUI memakai HTTP fixture; smoke hosted menggunakan SQL/RPC database dalam rollback. GUI dengan password pemilik tidak dicoba pada tahap ini; tidak ada password/token pemilik dipakai/disimpan.
- Screenshot fixture ditinjau: D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-return-ui\Inspect-fixture.png, Inspect-minimum-fixture.png, Approve-minimum-fixture.png dan Settle-minimum-fixture.png. Screenshot bukan transaksi toko nyata.
- Advisors: tidak ada ERROR schema/RLS baru. WARN Auth [leaked password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) belum aktif, sama dengan sebelumnya. INFO [RLS ledger tanpa policy](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy) disengaja deny-all; INFO [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index) sesuai database awal, tidak dihapus tanpa bukti beban nyata.

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot QC/persetujuan/settlement 1000x850 dan minimum 900x760 ditinjau; nominal dan konfirmasi tetap terlihat; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total master dari count=exact server, bukan angka buatan.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Tiga tombol membuka tahap QC, persetujuan dan settlement. Kondisi tiap unit, usulan, catatan, konfirmasi, metode, simpan dan retry dijalankan. Toolbar mencapai tahap Selesai; retry ketiga RPC tidak menggandakan tindakan atau uang.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 322 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: footer menyebut alur reservasi/pengambilan/pengembalian online; batas laporan/pembatalan/perpanjangan tetap dijelaskan; batas validasi hosted/fixture dinyatakan di atas.
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
- C-2 PASS: QC, persetujuan dan settlement melalui toolbar diuji, termasuk refund penuh/parsial, kekurangan, nol perpindahan uang dan retry; baseline tetap lulus.
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

## Batas pekerjaan

Perpanjangan, pembatalan/pencatatan hangus melalui UI, laporan kas lengkap, pengelolaan akun staf dan distribusi EXE masih tahap berikutnya. Koreksi hasil QC/denda yang sudah dicatat belum memiliki alur koreksi; jangan menghapus history atau mengedit ledger langsung untuk menyimulasikannya. Installer dan konversi Designer tetap ditunda.
