# QA login dan repository online

Tanggal: 21 November 2026. Lingkup: login Supabase dan repository baca async. UI tetap ditulis di VB.NET; tidak dilakukan konversi Designer. Data master dan transaksi online belum diaktifkan.

## Bukti

- Build solusi: 0 warning, 0 error.
- Runner `--ui-check`: 136 pemeriksaan lulus, termasuk regresi, kontrak HTTP, sesi, repository dan click-through form tiga peran.
- Login cloud nyata: pemilik memasukkan password sendiri dan mengonfirmasi dashboard terbuka serta profil Administrator tampil. Password tidak diberikan kepada agent atau disimpan oleh aplikasi.
- Screenshot dari fixture pengujian: `D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-online-ui`. Screenshot fixture bukan bukti adanya transaksi tersebut di database cloud.
- Pengujian HTTP memakai `FakeSupabaseServer`, tidak membuat akun/password atau transaksi contoh pada cloud.

## Cara mengulang

```powershell
dotnet run --project tests/RantauGear.Checks.vbproj --artifacts-path 'D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-validation' -- --ui-check
```

Pengujian mencakup password salah, profil tidak ada/nonaktif, role tidak dikenal, perubahan role, pagination server, jadwal WIB, refresh bersamaan, 401 setelah refresh, logout offline, cancel, error dan empty state, perpindahan halaman saat request berjalan, serta navigasi tiap peran. Enter menjalankan validasi, Escape membatalkan login, native Tab berpindah kontrol, dan tombol tampil-password, muat ulang, awal/sebelumnya/berikutnya serta keluar akun diuji.

## Delivery Gate

Design read: aplikasi meja rental untuk petugas toko, identitas hijau hutan dengan register alat; ENERGY 2 / RHYTHM 2 / MOTION 1. Warna hijau memandu login/navigasi, workspace terang mendukung tabel, teks merah menyertai error. Tidak menambah logo, foto, ikon, animasi atau navigasi baru.

### Hard Gate

- R-02 PASS: copy UI baru memakai kalimat langsung tanpa em dash.
- R-03 PASS: login diperiksa pada ukuran minimum 900x600 dan workspace 1280x800; screenshot minimum ditinjau dan teks panel login diperbaiki agar terbaca. Form memakai DPI scaling; ini aplikasi desktop, bukan halaman mobile.
- R-17 PASS: jumlah data berasal dari Content-Range count=exact; tidak ada angka statistik pemasaran.
- R-18 PASS: tidak ada testimoni atau avatar buatan.
- R-23 PASS: aset dan struktur navigasi yang ada dipertahankan; screenshot hanya artefak QA pada folder backup.
- R-24 PASS: semua menu yang terlihat membuka OnlineDataPage; menu mengikuti role yang diuji.
- R-25 PASS: rasio kontras terukur: putih/hijau 7.85, muted/latar 5.48, error/latar 5.88, putih/sidebar 15.93, teks/latar 13.48. Kontrol native mengikuti Windows.
- R-26 PASS: tombol login, tampil-password, cancel, muat ulang, pagination dan logout memiliki aksi dan diuji; tidak ada tombol penulisan online yang berpura-pura sudah bekerja.
- R-27 PASS: loading, kosong, error jaringan, cache sebelumnya, pembatalan dan sesi berakhir diuji.
- R-28 PASS: tidak ada FAQ.
- R-32 PASS: native Tab, Enter dan Escape diuji; RoundedButton menggambar focus rectangle saat fokus keyboard.
- R-33 PASS: UI dan behavior berada dalam source VB yang dikompilasi; tidak ada patcher/runtime script yang mengubah UI saat aplikasi berjalan.
- R-34 PASS: workspace light tetap sesuai DESIGN; tidak menambah toggle tema.
- R-35 PASS: build dijalankan, form dijalankan, click-through tiga peran lulus, screenshot ditinjau dan login akun cloud dikonfirmasi pemilik.
- R-36 PASS: tidak mengklaim seluruh transaksi online selesai; footer dan dokumentasi menyatakan akses baca.
- R-37 PASS: arah visual dan dials ditetapkan sebelum implementasi.
- R-38 PASS: daftar produksi berasal dari Supabase; fixture terpisah pada proyek tests dan nama screenshot menyebut fixture.
- R-39 PASS: kontrol native bernama/berlabel, status disampaikan dengan teks, hover/focus/disabled tersedia, tanpa animasi baru. ARIA/HTML tidak berlaku pada WinForms.
- R-40 PASS: tidak menambah media, font remote, script web atau klaim performa numerik; row status tetap memiliki ruang selama loading/error.

### Purpose Gate

- R-01 PASS: tidak menambah gradient/glow.
- R-04 PASS: tidak menambah ikon dekoratif.
- R-06 PASS: Segoe UI mengikuti Windows; nama brand dipertahankan, tanpa monospace/tracking baru.
- R-07 PASS: tidak menambah pola background.
- R-08 PASS: tidak menambah panah dekoratif.
- R-09 PASS: tidak menambah badge marketing.
- R-10 PASS: tidak memakai glassmorphism.
- R-12 PASS: tidak menambah shadow pada komponen.
- R-13 PASS: tidak menambah glow.
- R-14 PASS: komposisi berdasarkan form login dan tabel operasional, bukan kartu fitur seragam.
- R-19 PASS: tidak menambah animasi; MOTION 1 dipertahankan.
- R-22 PASS: tidak menambah ilustrasi generik.

### Liveliness

- Dials PASS: ENERGY 2 / RHYTHM 2 / MOTION 1; brand/sidebar hijau, form terarah, halaman data memakai tabel.
- Fokus PASS: tombol Masuk menjadi aksi utama login; tabel menjadi fokus workspace.
- Ruang PASS: kolom brand dipisahkan dari form; status loading/error memiliki ruang tetap.
- Aksen PASS: aksi utama hijau dan error merah mempunyai fungsi nyata, tidak tersebar sebagai dekorasi.
- Identitas PASS: nama RANTAU Gear, warna hutan, kode unit, jadwal dan nominal mengikuti register rental.

### Craftsmanship dan consistency

- C-1 PASS: perubahan visual melayani login dan pembacaan data.
- C-2 PASS: kontrol yang ditampilkan diuji dan bekerja.
- C-3 PASS: tidak menambah bagian pemasaran atau isi pengisi.
- C-4 PASS: tiga role, ukuran minimum, error/empty/loading, cancellation dan lifecycle disposal diuji.
- C-5 PASS: hasil nyata dan fixture dibedakan; konfirmasi login cloud dicatat sebagai konfirmasi pemilik.
- R-05 PASS: mempertahankan shell meja rental; tidak memakai template landing page.
- R-11 PASS: input/tabel native, tombol memakai radius yang sudah ada, tanpa pill berulang.
- R-15 PASS: aksi spesifik Masuk, Muat ulang, Batalkan, Berikutnya dan Keluar akun.
- R-16 PASS: tanpa buzzword pemasaran.
- R-20 PASS: arah visual register rental yang ada dipertahankan.
- R-21 PASS: workspace light sesuai arah desain dan penggunaan toko.
- R-29 PASS: palet Theme dipertahankan.
- R-30 PASS: tidak mengadopsi tampilan produk lain.
- R-31 PASS: alasan layout, warna, font dan status dijelaskan pada design read.

## Batas pekerjaan

Penulisan data master, reservasi/pembayaran/QC, laporan kas lengkap, reset password mandiri dan pengelolaan akun staf belum diimplementasikan pada UI online. Menu terkait saat ini adalah daftar baca dengan keterangan yang jelas. Installer tetap ditunda. Tidak dilakukan pengujian cloud dengan password buatan atau menyimpan password pemilik.
