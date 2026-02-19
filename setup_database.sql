-- KamatekCRM PostgreSQL Kurulum Scripti
-- pgAdmin veya psql'de çalıştır

-- 1. Veritabanı oluştur
CREATE DATABASE kamatekcrm
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'Turkish_Turkey.1254'
    LC_CTYPE = 'Turkish_Turkey.1254'
    TABLESPACE = pg_default
    CONNECTION LIMIT = -1;

-- 2. Kullanıcı oluştur (eğer kamatek_admin istiyorsan)
CREATE USER kamatek_admin WITH PASSWORD 'Kamatek2024!';

-- 3. Yetkileri ver
GRANT ALL PRIVILEGES ON DATABASE kamatekcrm TO kamatek_admin;

-- 4. Schema yetkileri
\c kamatekcrm;
GRANT ALL ON SCHEMA public TO kamatek_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kamatek_admin;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kamatek_admin;

-- Hazır! Uygulama migration'ları otomatik çalıştıracak.
