@echo off
cd /d "C:\Program Files\PostgreSQL\18\bin"
echo Testing PostgreSQL connection...
echo.
psql -U postgres -h localhost -p 5432 -c "SELECT version();"
echo.
echo Listing databases:
psql -U postgres -h localhost -p 5432 -l
echo.
echo If you see the version and database list above, PostgreSQL is working!
pause
