-- Katalog camping awal. Tidak berisi pelanggan, akun, atau transaksi.
-- Tarif demo adalah contoh proyek; nilai penggantian belum dinilai (0), bukan estimasi harga pasar.
-- Jalankan sebagai admin aktif melalui JWT sub; tidak mengubah record yang sudah ada.
do $$
declare item jsonb; component record; cat bigint; typ bigint; contents jsonb;
begin

 perform pg_advisory_xact_lock(hashtextextended('rantau.catalog.seed',0));
 for item in select value from jsonb_array_elements('["Tenda", "Perlengkapan Tidur", "Alat Masak", "Tas & Carrier", "Penerangan"]'::jsonb) loop
  if not exists(select 1 from public.categories where lower(name)=lower(item#>>'{}')) then
   perform public.save_master_data(gen_random_uuid(),'categories',null,null,jsonb_build_object('name',item#>>'{}','is_active',true));
  end if;
 end loop;
 for item in select value from jsonb_array_elements('[{"code": "TND-001", "name": "Tenda Dome 4P", "category": "Tenda", "daily_rate": 85000}, {"code": "TND-002", "name": "Tenda Dome 4P", "category": "Tenda", "daily_rate": 85000}, {"code": "TND-003", "name": "Tenda Dome 2P", "category": "Tenda", "daily_rate": 65000}, {"code": "TND-004", "name": "Tenda Dome 2P", "category": "Tenda", "daily_rate": 65000}, {"code": "SLP-001", "name": "Sleeping Bag Polar", "category": "Perlengkapan Tidur", "daily_rate": 45000}, {"code": "SLP-002", "name": "Sleeping Bag Polar", "category": "Perlengkapan Tidur", "daily_rate": 45000}, {"code": "MTR-001", "name": "Matras Camping", "category": "Perlengkapan Tidur", "daily_rate": 25000}, {"code": "MTR-002", "name": "Matras Camping", "category": "Perlengkapan Tidur", "daily_rate": 25000}, {"code": "MSK-001", "name": "Kompor Portable", "category": "Alat Masak", "daily_rate": 50000}, {"code": "MSK-002", "name": "Nesting Set 4 Person", "category": "Alat Masak", "daily_rate": 60000}, {"code": "CAR-001", "name": "Carrier 60L", "category": "Tas & Carrier", "daily_rate": 80000}, {"code": "CAR-002", "name": "Carrier 60L", "category": "Tas & Carrier", "daily_rate": 80000}, {"code": "CAR-003", "name": "Daypack 25L", "category": "Tas & Carrier", "daily_rate": 40000}, {"code": "LMP-001", "name": "Headlamp LED", "category": "Penerangan", "daily_rate": 20000}, {"code": "LMP-002", "name": "Lamp Camping LED", "category": "Penerangan", "daily_rate": 25000}, {"code": "LMP-003", "name": "Powerbank Outdoor", "category": "Penerangan", "daily_rate": 35000}, {"code": "SLP-003", "name": "Sleeping Bag Polar", "category": "Perlengkapan Tidur", "daily_rate": 45000}, {"code": "SLP-004", "name": "Sleeping Bag Polar", "category": "Perlengkapan Tidur", "daily_rate": 45000}, {"code": "MTR-003", "name": "Matras Camping", "category": "Perlengkapan Tidur", "daily_rate": 25000}, {"code": "MTR-004", "name": "Matras Camping", "category": "Perlengkapan Tidur", "daily_rate": 25000}]'::jsonb) loop
  select id into strict cat from public.categories where name=item->>'category';
  select id into typ from public.equipment_types where category_id=cat and name=item->>'name';
  if typ is null then
   typ:=public.save_master_data(gen_random_uuid(),'equipment_types',null,null,jsonb_build_object('category_id',cat,'name',item->>'name','daily_rate',(item->>'daily_rate')::numeric,'replacement_value',0,'description','Perlengkapan camping. Tarif contoh proyek; nilai penggantian belum ditentukan.','is_active',true));
  end if;
  if not exists(select 1 from public.equipment_units where code=item->>'code') then
   perform public.save_master_data(gen_random_uuid(),'equipment_units',null,null,jsonb_build_object('equipment_type_id',typ,'code',item->>'code','state','ready','qc_confirmed',true,'notes','Inventaris awal proyek perkuliahan, bukan riwayat sewa demo.'));
  end if;
 end loop;
 for item in select value from jsonb_array_elements('[{"name": "Paket Camping 4 Orang", "daily_price": 275000, "components": {"Tenda Dome 4P": 1, "Sleeping Bag Polar": 4, "Matras Camping": 4, "Kompor Portable": 1}}, {"name": "Paket Camping 2 Orang", "daily_price": 165000, "components": {"Tenda Dome 2P": 1, "Sleeping Bag Polar": 2, "Matras Camping": 2, "Kompor Portable": 1}}, {"name": "Paket Pendakian", "daily_price": 225000, "components": {"Tenda Dome 2P": 1, "Carrier 60L": 1, "Sleeping Bag Polar": 1, "Headlamp LED": 1}}]'::jsonb) loop
  contents:='[]';
  for component in select key,value from jsonb_each_text(item->'components') loop
   select id into strict typ from public.equipment_types where name=component.key;
   contents:=contents||jsonb_build_array(jsonb_build_object('type_id',typ,'quantity',component.value::integer));
  end loop;
  if not exists(select 1 from public.packages where name=item->>'name') then
   perform public.save_master_data(gen_random_uuid(),'packages',null,null,jsonb_build_object('name',item->>'name','daily_price',(item->>'daily_price')::numeric,'is_active',true,'components',contents));
  end if;
 end loop;
end $$;
