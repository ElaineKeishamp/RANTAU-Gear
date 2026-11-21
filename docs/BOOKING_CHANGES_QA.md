# QA perpanjangan, pembatalan dan hangus

Tanggal: 21 November 2026. VB.NET WinForms .NET 10, folder kerja D:\elaine\SEM 5\RANTAU Gear.

## Bukti validasi

- PASS: 367 pemeriksaan aplikasi/UI (baseline 322 + 45 perubahan sewa). Preview perpanjangan, perubahan tanggal membatalkan konfirmasi, alasan pembatalan wajib, retained DP, tiga retry setelah respons hilang, dan tiga tombol toolbar sampai refresh diuji dengan HTTP fixture.
- PASS: stok/nominal berubah setelah preview tidak mencatat tambahan uang. Perpanjangan nol biaya meminta konfirmasi khusus tanpa payment. Hangus sebelum deadline dan perpanjangan terlambat diblokir menggunakan waktu server. QC ditolak membaca rincian perubahan.
- PASS: tombol lifecycle tetap berada dalam toolbar pada ukuran minimum efektif MainForm setelah DPI scaling. Tanggal/nominal diberi lebar kolom minimum dan header autosize; tabel bergulir horizontal saat sempit.
- PASS: database lokal 181 pemeriksaan (67 rental, 23 master, 20 booking, 20 pickup, 24 return, 27 changes), plus race dua koneksi untuk stok terakhir. Buffer dua jam tetap berlaku. Preview dan save menolak konflik, nominal salah ditolak tanpa payment, deposit tidak ditambah, retry tidak menduplikasi event/payment, DP pembatalan/hangus tetap, expiry dini ditolak, zero-cost extension tidak membuat payment. Log: D:\elaine\SEM 5\Pemrograman Visual-backup\changes-db.log.
- PASS: migrasi hosted 20261121030042_rantau_booking_changes diterapkan dan disimpan dengan versi yang sama; sepuluh migrasi.
- PASS: smoke hosted role authenticated/admin yang sudah ada: perpanjangan tenda 85.000/hari menjadi dua hari menghasilkan tambahan 85.000 sekali, deposit tetap 85.000; pembatalan mempertahankan satu payment DP; expiry mencatat satu event walau diulang. Skenario expiry memundurkan deadline hanya rental QA di dalam transaksi rollback, tidak mengubah katalog/riwayat nyata. Semua QA di-ROLLBACK: customers/rentals/payments tetap 0 dan 20 unit ready.
- Batas bukti: GUI memakai HTTP fixture, hosted memakai SQL/RPC dalam rollback. Tidak memakai password pemilik atau mengklaim GUI/PostgREST nyata sudah dicoba pemilik.
- Screenshot fixture ditinjau: D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-changes-ui\Extend-minimum-fixture.png, Cancel-minimum-fixture.png, Expire-minimum-fixture.png, booking-minimum-fixture.png. Bukan transaksi toko nyata.
- Advisors: tidak ada ERROR schema/RLS baru. WARN Auth [leaked password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) belum aktif seperti sebelumnya. INFO [ledger RLS tanpa policy](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy) sengaja deny-all dan [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index) tetap dicatat tanpa penghapusan indeks spekulatif.

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot perpanjangan/pembatalan/hangus 820x730 dan minimum 760x660 ditinjau; nominal dan konfirmasi terbaca; toolbar diperiksa pada minimum efektif setelah DPI scaling; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total master dari count=exact server, bukan angka buatan.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Perpanjangan, Pembatalan dan Catat hangus membuka dialog sesuai baris terikat. Preview, alasan wajib, konfirmasi, simpan dan Periksa simpan diklik. Ketiga tombol toolbar mengirim tindakan yang benar dan refresh daftar; efek uang/event sekali.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 367 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: status perpanjangan/pembatalan/hangus dijelaskan, pencatatan expiry dibedakan dari status efektif; laporan kas dan akun staf masih tahap berikutnya; batas validasi hosted/fixture dinyatakan di atas.
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
- C-2 PASS: tiga perubahan sewa lewat toolbar, konflik/quote berubah, zero-cost extension, deadline dan retry diuji; seluruh baseline tetap lulus.
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

Laporan kas lengkap, pengelolaan akun staf dan distribusi EXE masih tahap berikutnya. Tidak ada job UI yang berjalan saat aplikasi ditutup; status efektif/ketersediaan expiry dihitung server saat query, event hangus dicatat per reservasi melalui tombol. Refund karena kesalahan toko dan koreksi history belum memiliki alur. UI tetap berkode, tanpa konversi Designer.
