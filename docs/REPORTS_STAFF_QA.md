# QA laporan dan pengelolaan staf sebelum distribusi

Tanggal: 21 November 2026. Folder kerja D:\elaine\SEM 5\RANTAU Gear. Tetap VB.NET WinForms .NET 10, UI berkode.

## Hasil dan batas validasi

- PASS: build solusi debug, 0 warning dan 0 error. Tidak menjalankan dotnet publish, membuat installer, atau paket distribusi EXE.
- PASS: 416 pemeriksaan aplikasi/UI, termasuk seluruh baseline transaksi dan pemeriksaan laporan/akun. Filter periode/status, WIB, pagination maju/mundur, pembatalan request, data sebelumnya saat error, serta CSV seluruh hasil filter diuji.
- PASS: CSV UTF-8 BOM, escaping field dan teks formula diuji. Di atas batas hasil/ketika rows tidak lengkap ekspor ditolak, bukan dipotong diam-diam. Arus kas negatif tetap numerik.
- PASS: dialog tambah/edit/password memakai input bertopeng dan toggle tampil-password; self-role/active dilindungi; stale edit ditolak. Retry request yang responsnya hilang membekukan payload dan tidak menggandakan akun/mutasi. Password sendiri yang berhasil diubah meminta login ulang.
- PASS: toolbar Tambah, Edit, Ubah password dan Muat ulang staf benar-benar diklik. Target mengikuti ID akun pada baris terikat. QC/front office ditolak mengelola staf; QC ditolak membaca kas.
- PASS: 212 pemeriksaan PostgreSQL lokal, ditambah uji dua koneksi untuk stok terakhir. Tambahan laporan/staf memeriksa batas hari WIB, DP/sewa/denda, dua refund, pemotongan nonkas, saldo deposit awal/akhir, limit null, versi/proteksi admin, pemulihan Auth marker dan tidak adanya password pada ledger.
- PASS: 17 pemeriksaan handler Edge dengan Node/HTTP mock. Identitas diverifikasi melalui Auth, otorisasi admin melalui RPC; anonymous/invalid/non-admin ditolak. Create + finish, retry setelah Auth sukses/finish terputus, reset target, sanitasi error dan tidak mengirim email diuji.
- PASS: migrasi hosted 20261121032956_rantau_reports_staff tersimpan lokal dengan versi sama; sebelas migrasi.
- PASS: fungsi staff-admin ACTIVE versi 1, platform verify_jwt=true. Handler juga memeriksa Auth /user dan RPC admin; endpoint anonim diuji HTTP 401. Kunci service-role hanya berasal environment runtime server, bukan desktop atau konfigurasi lokal.
- PASS: smoke SQL hosted membaca laporan kas kosong, daftar staf, proteksi self-disable dan preparation create. Permintaan preparation di-ROLLBACK. Profil staf tetap satu; pelanggan/transaksi tetap nol. Tidak membuat akun Auth cloud percobaan.
- BELUM DIKONFIRMASI MANUAL: create/reset Auth dengan JWT admin nyata melalui GUI, login staf baru, tampilan pada komputer/DPI pemilik, dan penerimaan alur keseluruhan. GUI memakai HTTP fixture; Node memakai mock. Anonymous hosted dan smoke SQL bukan pengganti pengujian Auth nyata. Password pemilik tidak diminta/dipakai agent.

## Bukti

- Log aplikasi/UI: D:\elaine\SEM 5\Pemrograman Visual-backup\review-ui.log.
- Log database: D:\elaine\SEM 5\Pemrograman Visual-backup\review-db.log.
- Log Edge: D:\elaine\SEM 5\Pemrograman Visual-backup\staff-edge.log.
- Screenshot fixture yang ditinjau: D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-review-ui\cash-report-fixture.png, staff-create-minimum-fixture.png, staff-list-fixture.png. Password tetap bertopeng, bukan kredensial pemilik.
- Advisors: tidak ada ERROR schema/RLS baru. WARN Auth [leaked password protection](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection) belum aktif seperti sebelumnya. INFO [tiga ledger privat tanpa policy](https://supabase.com/docs/guides/database/database-linter?lint=0008_rls_enabled_no_policy) disengaja deny-all; INFO [unused index](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index) tetap dicatat.
- Referensi autentikasi: [Supabase Edge Functions](https://supabase.com/docs/guides/functions/auth). verify_jwt=true dan verifikasi identitas/role pada handler dipertahankan.

## Delivery Gate

Design read: register alat rental dengan hijau hutan, workspace terang, Segoe UI, ENERGY 2 / RHYTHM 2 / MOTION 1. Tabel memusatkan daftar; formulir berlabel memusatkan pengisian. Aksen hijau menandai Tambah/Simpan. Tidak membuat aset visual. Pelanggan menjadi akses operasional admin/front office yang diminta lingkup master.

### Hard Gate

- R-02 PASS: copy editor menggunakan kalimat langsung tanpa em dash.
- R-03 PASS: screenshot laporan pada 1250x900, daftar staf pada 1200x850, dan editor minimum 720x650 ditinjau; filter, nominal, password bertopeng dan tombol terbaca; field berlabel, tabel dan tombol terbaca, editor menyediakan scroll. DPI/ukuran layar lain tetap memerlukan uji perangkat nyata.
- R-17 PASS: total dan ringkasan laporan/staf berasal dari RPC server, fixture terpisah dari data produksi.
- R-18 PASS: tidak menambah avatar atau testimoni.
- R-23 PASS: tidak menambah logo/foto; screenshot hanya QA. Menu Pelanggan melayani pengelolaan pelanggan dalam lingkup pekerjaan.
- R-24 PASS: katalog tiga tab, paket dan pelanggan membuka halaman master; navigasi tiga role diuji.
- R-25 PASS: palet Theme yang telah diukur dipertahankan, putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, teks/latar 13.48; kontrol native Windows.
- R-26 PASS: Tampilkan, filter, pagination maju/mundur, pembatalan pemuatan dan Ekspor CSV dijalankan. Toolbar Tambah/Edit/Ubah password/Muat ulang akun diklik; retry memakai UUID sama dan mutasi sekali.
- R-27 PASS: daftar menyediakan loading/kosong/error; editor menyediakan validasi, busy, sesi berakhir dan hasil simpan belum pasti; error tidak menghapus input.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: input, combo dan grid native menerima fokus; dialog menetapkan AcceptButton/CancelButton, aksi async tetap dapat dibatalkan, baseline keyboard checks lulus.
- R-33 PASS: UI disimpan dalam source VB dan dikompilasi, tanpa runtime patcher.
- R-34 PASS: workspace terang mengikuti DESIGN, tidak ada toggle tema baru.
- R-35 PASS: form dan dialog dijalankan dalam 416 checks; tombol editor dan toolbar diklik; screenshot ditinjau. Uji manual GUI ke cloud belum diklaim.
- R-36 PASS: laporan membedakan kas, deposit, pemotongan nonkas dan tagihan; pengujian GUI/Auth nyata yang belum dilakukan dinyatakan sebagai pemeriksaan manual, distribusi belum diizinkan; batas validasi hosted/fixture dinyatakan di atas.
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
- C-2 PASS: filter laporan, ekspor utuh/formula CSV, tambah/edit/password/versi akun dan retry diuji; seluruh baseline tetap lulus.
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

## Batas penyerahan

Versi ini siap diperiksa dari project Visual Studio, belum dinyatakan lolos review pemilik atau siap distribusi. Ikuti [PRE_DISTRIBUTION_REVIEW.md](PRE_DISTRIBUTION_REVIEW.md); semua checkbox manual sengaja belum dicentang. Tidak boleh publish/installer/paket EXE sebelum pemilik selesai memeriksa dan memberi instruksi baru.

CSV maksimal 10.000 hasil per ekspor; perkecil periode bila lebih besar. Laporan kas hanya transaksi rental, bukan laba/modal/biaya operasional toko. Buat akun baru dengan email baru; pengambilalihan/link Auth lama atau penggantian email login belum tersedia. Reset password tidak diklaim mencabut semua access token lama. Perubahan nama sendiri pada header sesi dapat memerlukan login ulang. Koreksi history/refund karena kesalahan toko tetap memerlukan alur tersendiri dan tidak boleh disimulasikan dengan hapus ledger.
