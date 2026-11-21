# Aturan rental yang disepakati

Versi awal desktop VB.NET WinForms .NET 10. Pengguna aplikasi hanya petugas toko. Pelanggan tidak memiliki akun. Tidak menyimpan foto KTP.

## Jadwal dan stok

- Alur: reservasi, pengambilan, pengembalian. Walk-in memakai alur yang sama.
- Reservasi menyebut jenis produk dan jumlah. Jenis produk berbeda dari kategori dan unit fisik; misalnya kategori Tenda, jenis Dome 4 Orang, unit TND-001.
- Unit fisik dipilih saat pengambilan. Unit harus siap, lolos QC, dan tidak sedang dipinjam.
- Durasi tagihan per 24 jam, dibulatkan ke atas, minimal satu hari. Harga dan aturan transaksi disalin saat reservasi.
- Rentang ketersediaan setengah terbuka: waktu ambil termasuk, akhir buffer tidak termasuk. Buffer dua jam ditambahkan setelah jadwal kembali; pengembalian aktual juga menahan unit dua jam untuk pemeriksaan.
- Kapasitas adalah unit ready/rented dari jenis aktif; maintenance, lost, retired tidak dapat ditawarkan.
- Hitung puncak pemakaian bersamaan, bukan menjumlahkan semua reservasi yang bersinggungan dengan rentang panjang.
- Sewa aktif yang terlambat dan belum kembali tetap menahan kapasitas. Waktu kembalinya belum dapat diasumsikan.
- Tidak ada override konflik stok.
- Batas ambil 12 jam sejak jadwal ambil. Tepat pada batas masih diizinkan; sesudahnya hangus. Mengabari terlambat tidak menggeser batas. DP tetap tercatat dan tidak dikembalikan.
- Ketersediaan langsung mengabaikan reservasi hangus berdasarkan waktu server, meskipun job pencatatan status belum berjalan. rental_overview menampilkan status efektif expired/late; expire_reservations mencatat status dan event hangus.

## Uang

- DP reservasi 25% biaya sewa. Pencatatan reservasi melalui RPC sekaligus mencatat DP yang benar-benar diterima petugas.
- Deposit 100% biaya sewa awal. Deposit bukan pendapatan.
- Saat pengambilan, petugas menerima sisa sewa dan deposit. Metode tunai atau transfer, dicatat manual.
- Perpanjangan melalui pegawai hanya jika kapasitas tersedia. Tambahan biaya sewa dicatat saat disetujui; deposit yang sudah diterima tetap.
- Implementasi awal perpanjangan hanya sebelum jatuh tempo. Permintaan setelah jatuh tempo ditolak agar perubahan jadwal tidak menghapus denda yang sudah timbul. Alur perpanjangan setelah terlambat memerlukan aturan tambahan sebelum dibuka.
- Denda terlambat: toleransi 60 menit, lalu 25% tarif harian per 24 jam setelah toleransi, dibulatkan ke atas. Paket memakai tarif harian paket. Tidak menghitung keterlambatan selama petugas menyelesaikan persetujuan atau pembayaran setelah barang diserahkan.
- Contoh tarif Rp100.000, due Minggu 10.00: kembali Minggu 11.00 nol, Minggu 11.01 Rp25.000, Senin 11.00 Rp25.000, Senin 11.01 Rp50.000.
- Rusak dan hilang wajib memiliki usulan denda dan catatan QC; Super Admin menyetujui tagihan. Nilai penggantian jenis alat tersedia sebagai referensi QC, bukan nominal yang dapat diubah pelanggan.
- Denda keterlambatan tetap ditambahkan jika barang juga rusak/hilang.
- Seluruh unit harus diperiksa sekaligus. Yang hilang dicatat lost, bukan dibiarkan sebagai pengembalian sebagian.
- Deposit dipakai untuk membayar denda melalui deposit_deductions. Sisa dikembalikan; kekurangan dicatat sebagai pembayaran tambahan. Pemotongan deposit bukan penerimaan uang baru.
- Penyelesaian uang melalui RPC berarti uang benar-benar diterima/dikembalikan oleh petugas, bukan hanya hasil estimasi. UI nanti harus memberi konfirmasi nominal sebelum memanggilnya.
- Pembatalan pelanggan mempertahankan DP, rental dan riwayat pembayaran. Pengembalian uang karena kesalahan toko belum memiliki RPC dan tidak boleh disimulasikan sebagai pembatalan pelanggan.
- Nilai rupiah bulat; persentase yang menghasilkan pecahan dibulatkan ke atas ke rupiah penuh.

## Peran dan riwayat

- Super Admin mengelola pengaturan, data master, akun, dan persetujuan denda.
- Front office mengelola pelanggan, reservasi, pengambilan, perpanjangan dan pembayaran.
- Gudang/QC memeriksa semua barang dan mencatat hasil pengembalian. Kontak pelanggan serta ledger pembayaran tidak dapat dibaca role ini.
- Status barang kembali terpisah dari penyelesaian tagihan. Setelah QC, admin menyetujui tagihan, kemudian front office menyelesaikan uang.
- Semua riwayat pembayaran, denda, event dan audit append-only. Tidak ada DELETE untuk akun authenticated; trigger menolak penghapusan catatan bisnis termasuk melalui SQL admin biasa.
- Penandaan pelanggan bermasalah, portal pelanggan, pembayaran online, notifikasi otomatis, foto KTP, pengembalian sebagian, dan installer ditunda.
