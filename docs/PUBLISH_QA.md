# Publish Windows x64 - 21 November 2026

Pemilik mengizinkan publish melalui instruksi "baik maka lakukan". Paket folder dan ZIP dibuat; installer belum dibuat. Checklist manual pada PRE_DISTRIBUTION_REVIEW.md tetap terbuka.

- Sumber paket: commit bc4fc4bde05acf34e2a7e9dedaaa9efc67b61221, working tree bersih saat publish.
- Target: .NET 10 Windows x64, self-contained, tanpa trimming dan simbol debug.
- Output: `D:\elaine\SEM 5\Pemrograman Visual-backup\RANTAUGear-publish-20261121-135538`.
- Arsip: `RANTAU-Gear-win-x64.zip`, 51.600.539 byte; folder aplikasi beserta subfolder bahasa 272 file, 123.296.288 byte.
- SHA256 ZIP: `F19A4692319B9CBF028C1B233BD3AED84EF29D68B0B29FB38481B048DED6B855`.
- 417 pemeriksaan aplikasi/UI lulus, termasuk lokasi ekspor portabel. Publish selesai tanpa warning/error pada log.
- Uji proses EXE berhasil menemukan form RANTAU Gear Rental - Login dan coreclr.dll dari folder paket, dengan DOTNET_ROOT diarahkan ke lokasi tidak tersedia.
- Paket hanya menyalin projectUrl dan publishableKey publik; tidak memuat password atau kunci admin. Tidak menyertakan sumber, PDB, tests, bin/obj atau .git.
- Seluruh 272 file dalam ZIP diperiksa SHA256 terhadap file hasil publish; checksum ZIP sesuai.
- Probe MainWindowTitle awal tidak mendeteksi jendela yang disembunyikan; pemeriksaan judul lewat EnumWindows milik PID yang sama berhasil. Hitungan awal 220 hanya mencakup file di root; pemeriksaan arsip lengkap mencakup 272 file termasuk subfolder bahasa.

Ekstrak seluruh ZIP ke folder yang bisa ditulis, kemudian buka RANTAUGear.exe. Semua file harus tetap bersama. Visual Studio dan instalasi .NET terpisah tidak diperlukan. Internet serta akun staf aktif tetap diperlukan. Laporan CSV disimpan di subfolder Laporan; RANTAUGEAR_EXPORT_DIRECTORY dapat mengganti lokasinya.

Belum diuji di komputer Windows lain, belum ditandatangani dengan sertifikat penerbit, dan login/transaksi cloud menyeluruh serta pembuatan akun Auth nyata masih mengikuti checklist manual pemilik. Uji launch tidak mengirim password, membuat akun, atau menulis transaksi cloud.

Skrip reproduksi: scripts/Publish-Windows.ps1. Bukti lokal: publish.log, release-info.json, smoke-test.json, SHA256SUMS.txt pada folder output; publish-ui-check.log pada folder backup.
