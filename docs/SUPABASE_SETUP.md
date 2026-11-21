# Kebutuhan database

## Menggunakan database aplikasi yang sudah tersedia

Minta Project URL dan publishable key kepada pengelola proyek. Isi kedua nilai tersebut di supabase.local.json mengikuti supabase.example.json. Akun staf dibuat melalui menu Kelola User oleh Super Admin. Akun pelanggan tidak digunakan.

Aplikasi memerlukan internet dan profil staff_profiles yang aktif dengan ID sesuai akun Supabase Auth. Peran yang digunakan adalah super_admin, front_office, dan warehouse_qc.

Jangan menjalankan migrasi atau seed kembali pada database yang sudah disiapkan.

## Menyiapkan database terpisah

Bagian ini diperlukan hanya untuk lingkungan baru.

1. Siapkan proyek Supabase dan terapkan file supabase/migrations/ menurut urutan nama pada database kosong.
2. Buat akun admin pertama di Supabase Auth dan profil staff_profiles dengan UUID akun tersebut, nama tampilan, role super_admin, dan is_active=true.
3. Deploy fungsi staff-admin dari supabase/functions/staff-admin dengan verifikasi JWT aktif. Fungsi ini memerlukan konfigurasi server SUPABASE_URL serta SUPABASE_SERVICE_ROLE_KEY.
4. Katalog awal dapat diisi melalui supabase/seeds/camping_catalog.sql dalam konteks admin aktif. Seed ini tidak menambahkan akun, pelanggan, atau transaksi.
5. Arahkan konfigurasi desktop ke Project URL dan publishable key proyek tersebut.

Password database dan kunci administrasi hanya digunakan pada pengelolaan server. Jangan menaruhnya di konfigurasi desktop, source code, atau repository. Fixture supabase/tests/bootstrap_local.sql khusus pengujian lokal dan tidak boleh diterapkan pada database aplikasi.

## Konfigurasi desktop

Build Debug menyalin supabase.local.json dari root proyek ke folder output. Untuk paket Release, letakkan konfigurasi yang memuat projectUrl dan publishableKey di sebelah EXE. Alternatifnya, variabel RANTAUGEAR_CONFIG_PATH dapat menunjuk file konfigurasi lain.

Kegagalan konfigurasi atau koneksi tidak mengalihkan aplikasi ke akun demo. Transaksi membutuhkan akun staf aktif; publishable key saja tidak memberikan akses staf.
