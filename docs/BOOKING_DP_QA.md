# QA reservasi dan pembayaran DP

Tanggal: 21 November 2026. Folder kerja: D:\elaine\SEM 5\RANTAU Gear. Tetap VB.NET WinForms .NET 10, UI berkode.

## Bukti

- PASS: 193 pemeriksaan aplikasi/UI, termasuk baseline login/master dan 28 pemeriksaan reservasi. Pilih pelanggan, jumlah, preview, perubahan tanggal, paket tetap, konfirmasi DP dan simpan dijalankan dengan HTTP fixture.
- PASS: respons RPC composite satu baris berbentuk array PostgREST dan bentuk object diuji; adapter memeriksa tepat satu bukti transaksi. [Kontrak PostgREST](https://docs.postgrest.org/en/stable/references/api/resource_representation.html#singular-or-plural).
- PASS: respons hilang setelah commit membekukan payload; retry memakai UUID sama, satu reservasi dan satu DP. Stok/harga berubah ditolak, konfirmasi uang dibatalkan.
- PASS: toolbar Reservasi baru membuka dialog, menyimpan, menampilkan nomor transaksi dan memuat ulang daftar.
- PASS: 110 pemeriksaan database lokal (67 rental + 23 master + 20 booking/DP), serta race dua koneksi untuk unit terakhir. Log: D:\elaine\SEM 5\Pemrograman Visual-backup\booking-db.log.
- PASS: migrasi 20261121020414_rantau_booking_dp diterapkan ke hosted; versi lokal sama, tujuh migrasi.
- PASS: smoke SQL hosted memakai role authenticated dan profil admin yang ada; alat satuan dan paket empat orang berhasil direservasi dengan DP, retry tidak menambah DP, register memuat pelanggan/uang. Seluruh QA di-ROLLBACK: pelanggan, rental dan pembayaran tetap 0. Ini bukan klaim end-to-end GUI dengan login pemilik; password pemilik tidak dipakai/disimpan.
- Katalog permanen yang diminta pemilik: 5 kategori, 11 jenis, 20 unit ready, 3 paket. Tidak menyalin pelanggan, transaksi atau akun demo. Nilai penggantian belum ditentukan (0), tarif mengikuti contoh demo.
- Screenshot fixture ditinjau: D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-booking-ui\reservation-quote-fixture.png. Ringkasan sewa/DP/deposit/batas ambil dan checkbox selalu terlihat, bagian input dapat digulir.
- Advisors: tidak ada ERROR schema/RLS. WARN Auth [leaked password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) belum aktif, sama dengan sebelumnya. INFO [RLS tanpa policy](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy) pada ledger privat disengaja deny-all. INFO [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index) pada database awal tidak dihapus.

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot reservasi 860x860 dan baseline editor/katalog ditinjau; ringkasan uang dan checkbox DP selalu terlihat; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total master dari count=exact server, bukan angka buatan.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Reservasi baru membuka modal, Hitung biaya memanggil preview, checkbox mengaktifkan Simpan reservasi + DP. Perubahan jadwal membatalkan quote. Periksa simpan memakai UUID sama tanpa DP kedua; baseline master/pagination lulus.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 193 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: footer menyebut master/reservasi online dan pengambilan/QC belum aktif; batas validasi hosted/fixture dinyatakan di atas.
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
- C-2 PASS: simpan reservasi/DP melalui toolbar, preview, paket, perubahan harga/stok dan retry diuji; baseline master lulus.
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

Pengambilan, penerimaan deposit/sisa sewa, perpanjangan, QC/persetujuan denda, settlement dan laporan kas belum terhubung ke UI. Form ini mencatat reservasi dan DP yang diterima manual, bukan pembayaran online. Pembatalan dan pencatatan hangus di UI merupakan tahap transaksi lanjutan; aturan dan endpoint server tetap berlaku. Installer dan konversi Designer tidak dikerjakan.
