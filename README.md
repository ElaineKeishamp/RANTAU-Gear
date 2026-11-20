# RANTAU Gear Rental

Aplikasi desktop VB.NET Windows Forms untuk penyewaan alat outdoor dan dokumentasi. Target: .NET 10 di Windows.

## Menjalankan proyek

Buka `RANTAUGear.slnx` di Visual Studio dengan dukungan .NET 10 dan Windows Forms, lalu jalankan proyek `RANTAUGear`. Alternatif dari folder ini:

```powershell
dotnet run --project RANTAUGear/RANTAUGear.vbproj
```

Semua data masih berupa contoh dalam memori. Perubahan bertahan saat keluar-masuk akun dalam proses yang sama, tetapi hilang setelah aplikasi ditutup. Tidak ada database online yang terhubung.

| Peran demo | Username | Password | Akses |
| --- | --- | --- | --- |
| Administrator | superadmin | admin123 | Semua halaman |
| Front office | front | front123 | Dashboard, Booking, Laporan |
| Gudang dan QC | gudang | gudang123 | Dashboard, Pengembalian & QC |

Akun dan password tersebut hanya untuk demo lokal. Autentikasi produksi belum diterapkan.

## Alur yang tersedia

- Dashboard menampilkan jumlah unit dan antrean pengembalian berdasarkan data repository.
- Katalog mendukung pencarian serta perubahan status siap/perawatan. Alat yang masih disewa harus melalui pengembalian dahulu.
- Paket menampilkan isi dan kesiapan unit fisik. Paket dapat dipilih saat booking.
- Booking mencatat pelanggan baru, pemeriksaan identitas, waktu ambil/kembali, unit atau paket, dan deposit. Booking langsung mengeluarkan unit dari stok siap. Tarif dihitung per hari, dibulatkan ke atas per 24 jam; ini belum merupakan reservasi masa depan dengan status terpisah.
- Pengembalian memeriksa setiap unit. Kerusakan/denda memerlukan catatan; alat yang perlu perawatan tidak langsung tersedia lagi.
- Denda terlambat menggunakan toleransi 30 menit, kemudian 10% jumlah tarif harian unit per jam, dibulatkan ke atas. Besaran ini adalah aturan demo dan dapat disesuaikan.
- Laporan memfilter tanggal pengambilan, membedakan subtotal sewa dari deposit, dan mengekspor CSV UTF-8. Nilai sewa merupakan rekap transaksi, bukan pencatatan pembayaran kas.
- Kelola User mengaktifkan/nonaktifkan akun demo dan mencegah penonaktifan akun sendiri.

## Pemeriksaan

```powershell
dotnet build RANTAUGear.slnx
dotnet run --project tests/RantauGear.Checks.vbproj -- --check
```

Runner tanpa paket pengujian eksternal memeriksa validasi booking, stok, hak akses, harga paket, pembulatan tarif, batas denda, dan pencatatan QC. Tanpa `--check`, runner membuka pratinjau administrator menggunakan repository demo terpisah untuk pemeriksaan desain. Pratinjau tersebut berada di proyek pengujian dan bukan jalur login aplikasi utama.

## Supabase: persiapan integrasi

Migrasi database dan transport HTTP async tersedia di branch pengembangan. Belum diterapkan ke akun Supabase pengguna dan belum menggantikan repository pada form. Aplikasi utama masih menjalankan alur serta aturan demo yang dijelaskan di atas.

- [Aturan bisnis yang disepakati](docs/BUSINESS_RULES.md): DP 25%, deposit 100%, buffer dua jam, batas ambil 12 jam, denda harian 25% setelah toleransi satu jam.
- [Panduan Supabase dan status pekerjaan](docs/SUPABASE_SETUP.md): schema, RLS, RPC, akun awal, konfigurasi lokal, pengujian, dan langkah yang masih perlu dilakukan.
- Migrasi di `supabase/migrations`; fixture/Auth tiruan di `supabase/tests` hanya untuk PostgreSQL lokal, bukan cloud.
- `supabase.example.json` hanya template. Konfigurasi sebenarnya memakai `supabase.local.json`, yang diabaikan Git. Jangan menanam secret/service-role key atau password database ke aplikasi.
- `IRentalRepository` tetap menjadi batas akses data. Adapter online serta migrasi pemanggil ke async belum selesai; transport tidak boleh dipasang dengan blocking `.Result` pada UI.

```powershell
.\supabase\tests\Test-Database.ps1
```

Runner database membuat cluster uji terisolasi pada localhost, menerapkan migrasi, menguji aturan bisnis dan RLS, serta menguji dua koneksi yang memesan unit terakhir bersamaan. Server dihentikan setelah selesai. Hasil pengujian disimpan di folder backup proyek.

Pembuatan installer atau paket EXE distribusi tetap ditunda.

## Skill dan alat pengembangan

Skill [anti-slop](https://github.com/miqdadbadjuber/anti-slop) sudah tersedia di lingkungan Codex dan digunakan dalam mode DURING pada pekerjaan ini. Arah visual dicatat di `DESIGN.md`, dengan panduan proyek di `AGENTS.md`.

[Codebase Memory MCP](https://github.com/DeusData/codebase-memory-mcp) sudah terhubung dan proyek ini telah diindeks. Cakupan simbol VB.NET pada indeks yang tersedia terbatas, sehingga pemeriksaan kode tetap membaca berkas `.vb` secara langsung. Tidak diperlukan pemasangan ulang kedua alat tersebut.
