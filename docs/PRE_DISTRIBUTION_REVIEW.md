# Pemeriksaan total sebelum distribusi

Status: pemilik mengizinkan publish folder/ZIP Windows x64 pada 2026-11-21. Izin pengemasan tidak berarti checklist manual berikut sudah selesai. Installer belum dibuat.

## Cara mulai

1. Buka D:\elaine\SEM 5\RANTAU Gear\RANTAUGear.slnx di Visual Studio. Jalankan proyek RANTAUGear dengan F5.
2. Masuk memakai akun admin Supabase sendiri. Password diketik sendiri, jangan dikirim kepada agent atau dimasukkan ke catatan review.
3. Gunakan data khusus uji yang mudah dikenali bila mencoba transaksi cloud. Riwayat transaksi memang tidak dihapus; data uji manual yang kamu simpan akan tetap ada. Agent belum menanam pelanggan/transaksi/akun staf percobaan ke cloud.
4. Catat setiap temuan: langkah, hasil yang diharapkan, hasil aktual, nomor sewa, role dan waktu WIB. Screenshot jangan memuat password atau token.

## Akun dan hak akses (prioritas pertama)

- [ ] Kelola User menampilkan admin sendiri, email, nama, role dan status aktif.
- [ ] Buat satu akun front office dan satu gudang/QC dengan email/password yang kamu tentukan. Tidak ada email undangan. Ini adalah uji manual penting untuk Auth server nyata yang belum dilakukan agent.
- [ ] Logout lalu login sebagai kedua akun baru. Front office melihat Booking, Pelanggan, Penyelesaian dan Laporan; gudang tidak melihat laporan kas/kontak pelanggan atau pengelolaan akun.
- [ ] Ubah nama/peran akun lain, logout/login akun itu dan pastikan akses sesuai.
- [ ] Nonaktifkan akun lain dan pastikan login/request berikutnya ditolak; aktifkan lagi lewat admin.
- [ ] Akun sendiri tidak dapat diturunkan dari admin atau dinonaktifkan.
- [ ] Ubah password akun uji; setelah logout, password lama gagal dan password baru bisa masuk. Jangan menganggap sesi lama otomatis tercabut hanya karena password diganti.
- [ ] Bila mengubah password admin sendiri, aplikasi meminta login baru setelah sukses.

## Katalog dan pelanggan

- [ ] Kategori, jenis alat, tarif, nilai penggantian, unit fisik dan tiga paket terbaca dari database.
- [ ] Tambah/edit/nonaktifkan data master uji. Kategori dengan jenis aktif dan jenis dalam paket aktif dilindungi.
- [ ] Buat pelanggan uji. Tidak ada login pelanggan atau foto KTP.
- [ ] Dua jendela mengedit record sama: hasil edit lama ditolak dan diminta muat ulang.

## Satu alur sewa lengkap

- [ ] Booking alat satuan untuk tepat 24 jam. Untuk langsung diambil, jadwal ambil harus sudah tiba dan jadwal kembali masih di depan.
- [ ] Contoh tarif Rp85.000/hari: sewa Rp85.000, DP 25% Rp21.250, deposit Rp85.000. Cocokkan dengan tarif yang tampil; tarif katalog bisa diubah pemilik.
- [ ] Sebelum uang diterima jangan centang konfirmasi. Sesudah DP diterima, simpan dan pastikan nomor sewa serta DP muncul.
- [ ] Pengambilan: pilih kode unit sesuai jenis/jumlah. Contoh di atas menerima sisa sewa Rp63.750 + deposit Rp85.000 = Rp148.750. DP tidak dibayar lagi.
- [ ] Sewa menjadi Disewa, unit menjadi dipinjam, jumlah sewa dibayar/deposit diterima benar.
- [ ] QC: semua unit diperiksa sekaligus. Coba menyimpan sebelum memilih semua kondisi, lalu pastikan ditolak.
- [ ] Kembalikan baik sebelum jatuh tempo. Admin menyetujui tagihan nol, front office menyerahkan refund penuh deposit dan mencatatnya.
- [ ] Tahap akhir Selesai. Riwayat pembayaran dan transaksi tetap ada.

## Variasi transaksi

- [ ] Paket 2/4 orang memakai isi tetap dan tarif paket; unit baru dipilih saat pengambilan.
- [ ] Pesan alat yang melebihi stok/jadwal bentrok: ditolak tanpa override.
- [ ] Perpanjang sewa aktif sebelum jatuh tempo. Tambahan sewa sesuai snapshot, deposit tidak bertambah. Bentrok reservasi berikutnya ditolak.
- [ ] Pembatalan pelanggan membutuhkan alasan; DP tidak direfund dan riwayat tetap ada.
- [ ] Muat ulang setelah deadline: status efektif Hangus. Catat hangus hanya sesudah 12 jam sejak jadwal ambil dan hanya mencatat kejadian; kabar terlambat tidak menggeser deadline.
- [ ] Gunakan unit khusus uji untuk rusak/hilang: kondisi stok akan benar-benar berubah. Isi biaya dan catatan, admin setujui, lalu pastikan deposit dipotong dan kekurangan/refund benar. Jangan merusak catatan unit katalog utama demi percobaan.
- [ ] Kasus waktu seperti toleransi satu jam/batas 12 jam sudah diuji otomatis pada database. Untuk uji manual gunakan jadwal yang benar-benar memenuhi waktu server; jangan mengubah jam server atau ledger untuk mempercepatnya.

## Laporan dan ekspor

- [ ] Kas transaksi menggunakan waktu pembayaran WIB; laporan penyewaan menggunakan jadwal ambil dan filter status.
- [ ] Untuk satu sewa Rp85.000 yang dikembalikan baik tanpa denda pada periode sama: penerimaan sewa Rp85.000, deposit masuk Rp85.000, refund deposit Rp85.000, arus kas bersih Rp85.000, saldo deposit akhir Rp0 jika tidak ada transaksi lain.
- [ ] Pemotongan deposit bukan kas baru dan tidak dihitung dua kali. Deposit bukan pendapatan sewa.
- [ ] Pembatalan/hangus tetap terlihat di laporan penyewaan; DP tetap masuk kas sesuai tanggal penerimaan.
- [ ] Ganti tanggal/status, klik Tampilkan. Filter belum diterapkan tidak boleh mengekspor tabel lama secara diam-diam.
- [ ] Ekspor CSV mencakup semua halaman sesuai filter. Buka dari D:\elaine\SEM 5\Pemrograman Visual-backup\Laporan dan cocokkan ringkasan/baris dengan snapshot saat ekspor. Di atas 10.000 baris harus mempersempit periode.

## Koneksi, tampilan dan penutupan

- [ ] Uji muat ulang, pagination, kondisi kosong, tanggal salah, dan koneksi terputus. Data lama diberi keterangan bila masih ditampilkan.
- [ ] Bila hasil simpan belum terkonfirmasi gunakan Periksa simpan; jangan menerima uang atau memberi refund kedua kali. Sesudah aplikasi ditutup paksa, muat ulang status/riwayat dahulu.
- [ ] Periksa semua form pada layar/DPI komputermu, Tab/Enter/Escape, dropdown, scroll tabel dan pesan validasi.
- [ ] Setelah selesai, logout. Pastikan tidak ada password pada screenshot/log/konfigurasi/repo.

## Keputusan pemilik

- [ ] Temuan dicatat dan sudah diperbaiki/diterima.
- [ ] Alur akun Auth nyata dan login akun baru sudah dikonfirmasi sendiri oleh pemilik.
- [ ] Pemilik menyatakan review selesai.
- [ ] Pemilik memberi instruksi terpisah untuk distribusi. Sampai itu terjadi: TIDAK ADA PUBLISH/INSTALLER/PAKET EXE.
