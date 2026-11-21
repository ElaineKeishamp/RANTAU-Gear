# QA data master online

Tanggal: 21 November 2026. Folder kerja: `D:\elaine\SEM 5\RANTAU Gear`. Login dan fondasi baca tetap mengikuti [QA sebelumnya](ONLINE_LOGIN_QA.md); dokumen ini mencatat milestone master terbaru.

## Hasil validasi

- PASS: runner `--ui-check`, 165 pemeriksaan aplikasi/UI. Lima editor benar-benar dibuka, input kosong ditolak, field diisi, tombol Simpan diklik dan hasil repository diperiksa. Tombol Tambah/Edit dari halaman katalog juga membuka dialog dan memuat ulang tabel.
- PASS: database lokal terisolasi, 67 pemeriksaan transaksi + 23 pemeriksaan master; dua koneksi bersamaan tidak dapat memesan unit terakhir dua kali. Log: `D:\elaine\SEM 5\Pemrograman Visual-backup\master-db-final.log`.
- PASS: fixture HTTP menguji respons simpan hilang setelah commit, penguncian payload, retry UUID sama tanpa duplikat, penolakan versi lama, serta pembatasan admin/front office/QC.
- PASS: migrasi hosted `20261121014656_rantau_master_data` diterapkan dan disimpan dengan versi cloud yang sama. Terdapat enam migrasi lokal/cloud.
- PASS: smoke SQL hosted memakai role authenticated dan profil admin yang sudah ada, membuat/membaca kelima master, mengedit pelanggan, mengulang UUID, serta menolak versi lama. Seluruh transaksi di-ROLLBACK. Setelahnya kategori/jenis/unit/paket/pelanggan tetap nol; tidak ada fixture cloud permanen.
- PASS: anon tidak dapat memanggil save_master_data; authenticated tidak dapat memanggil save_customer lama. Direct writes master dicabut; seluruh pengujian lokal tetap lulus.
- Screenshot QA fixture: `D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-master-ui`. Ditinjau: EquipmentType-fixture.png, EquipmentUnit-fixture.png, Package-fixture.png, catalog-tabs-fixture.png. Screenshot bukan data toko nyata.
- Manual cloud: login asli telah dikonfirmasi pemilik pada milestone sebelumnya. Penyimpanan lewat GUI menggunakan password pemilik belum dikonfirmasi; validasi penulisan hosted di atas dilakukan lewat SQL dalam transaksi rollback, bukan klaim end-to-end login+PostgREST dari GUI.

## Advisors setelah migrasi

Tidak ada ERROR schema/RLS. Satu WARN Auth yang sudah ada: [leaked password protection belum aktif](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection). Pengaturan Auth tersebut tidak diubah pada milestone master. INFO RLS tanpa policy pada kedua ledger privat memang deny-all, ditambah pencabutan grant klien. INFO unused index sesuai database yang belum memiliki transaksi; indeks tidak dihapus tanpa bukti beban nyata. [Penjelasan linter RLS](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy), [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index).

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot editor 720x740 dan katalog 1100x760 ditinjau; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total master dari count=exact server, bukan angka buatan.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Tambah/Edit membuka modal, Simpan kelima jenis mengubah fixture repository, Muat ulang/pagination/cancel memakai async handler. Retry simpan dengan UUID sama diuji.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 165 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: footer menyebut master online dan form transaksi belum aktif; batas validasi hosted/fixture dinyatakan di atas.
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
- C-2 PASS: simpan lima jenis, toolbar Tambah/Edit, retry dan validasi diuji.
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

## Tahap berikutnya

Hubungkan form reservasi/DP, pengambilan/deposit, perpanjangan, QC/persetujuan denda dan settlement ke RPC yang sudah ada. Laporan kas, pengelolaan staf dan distribusi EXE masih terpisah. Tidak dilakukan konversi Designer.
