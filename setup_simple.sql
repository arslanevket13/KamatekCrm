-- pgAdmin'de File > Open > bu dosya > Execute

-- Veritabanı oluştur (eğer yoksa)
SELECT 'CREATE DATABASE kamatekcrm'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'kamatekcrm');

-- Eğer yukarıdaki çalışmazsa manuel oluştur:
-- CREATE DATABASE kamatekcrm;
