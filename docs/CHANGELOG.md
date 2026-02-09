# KamatekCRM - Değişiklik Günlüğü

## 2026-02-09 (v6.9 - Remote Access & Documentation)
- **Remote Access Configuration**:
    - **Global Bindings**: API (5050) ve Web (7000) artık `0.0.0.0` dinliyor.
    - **Firewall Script**: `Enable-RemoteAccess.ps1` ile otomatik port açma.
    - **Documentation**: `REMOTE_ACCESS_GUIDE.md` ve `WEB.md` eklendi.
- **Web App Hotfixes**:
    - **MudBlazor Integration**: Eksik servis kayıtları ve paketler eklendi.
    - **Port Stability**: Web App portu 7000'e sabitlendi.
    - **Namespace Repair**: `Program.cs` ve Razor dosyalarındaki `CS0234` hataları giderildi.
- **Project Structure**: `docs/` klasörü güncellendi, `TEKNIK_HARITA` hibrit yapıyı kapsacak şekilde revize edildi.

## 2026-02-08 (v6.8 - Build Fixes & Architectural Improvements)
- **Compiler Fixes**: `Enums.` prefix removal and namespace standardization.
- **Null Safety**: `AddProductViewModel` constructor initialization and `EnumToBooleanConverter` null checks.
- **Architecture**: `UnitOfWork` parameterless constructor removed (enforcing DI). `SalesDomainService` and `InventoryDomainService` updated to use manual context temporarily (transaction isolation).
- **WPF Stability**: `MainWindow` changed to Transient to fix re-opening crashes.
- **Web Config**: API BaseUrl moved to `appsettings.json`.

## 2026-02-08 (v6.7 - Technician App Enhancement & Stability)
- **Photo Upload**: Blazor üzerinden fotoğraf yükleme ve galeri görünümü. `IPhotoStorageService` ile thumbnail desteği.
- **Google Maps**: Görev detay sayfasında müşteri konumuna navigasyon ve harita görünümü.
- **Web App Stability**: Namespace çakışmaları ve derleme hataları giderildi. `RootNamespace` tanımlandı.
- **Database Reset**: `SQLite Error (missing columns)` hatası için veritabanı %AppData% altına taşındı ve şema sıfırlandı.
- **DI & Navigation**: ViewModels manuel `new` yerine `NavigationService` üzerinden DI uyumlu hale getirildi.

## 2026-02-08 (v6.6 - Professional UI/UX Enhancement)
- **Toast Notifications**: Modern bildirim sistemi (Success, Error, Warning, Info). `IToastService` ile global yönetim.
- **Loading Overlay**: Asenkron işlemler için global yükleme ekranı. `ILoadingService` ile yönetim.
- **Animations**: Sayfa geçişleri ve liste animasyonları (`AnimationHelper`).
- **Dependency Injection**: UI servisleri (Toast, Loading) tüm ViewModel katmanına entegre edildi.
- **API Fix**: `AppDbContext` için `DbContextOptions` constructor eklendi (ASP.NET Core DI hatası giderildi).

## 2026-02-07 (v6.5 - Logging & Error Handling)
- **Serilog**: Günlük dönen log dosyaları (%AppData%) ve console loglama.
- **Global Exception Handler**: UI ve arka plan hatalarını yakalayan merkezi mekanizma.
- **Custom Exceptions**: `ValidationException`, `NotFoundException`, `BusinessRuleException`.
- **Infrastructure**: Temiz kod prensipleri ve yapısal iyileştirmeler.

## 2026-02-07 (v6.4 - Dependency Injection Refactoring)

### 🏗️ Architecture & DI
- **AuthService Integration**: `AuthService` artık static değil, `IAuthService` olarak inject ediliyor.
- **Domain Services**: `InventoryDomainService` ve `SalesDomainService` constructor injection yapısına geçirildi.
- **ViewModels**: `StockTransferViewModel` ve `ProductViewModel` DI uyumlu hale getirildi.
- **Clean Code**: Manuel servis oluşturma (`new Service()`) desenleri temizlendi.
- **Build Fixes**: Statik üye erişimi kaynaklı tüm derleme hataları giderildi.

## 2026-02-07 (v6.3 - Code Cleanup & Refactoring)

### 🧹 Code Cleanup & MVVM Enforcement
- **Refactored Views**: UI components (`CustomersView`, `StockTransferView`, `ToastNotificationControl`, etc.) refactored to remove code-behind and use MVVM Commands.
- **Login Module**: `LoginViewModel` now handles login logic via `ExecuteLoginAsync` command, removing dependency on code-behind.
- **Compiler Warnings (CS86xx)**: Addressed 50+ nullability warnings in ViewModels, Services, and Models (`CustomerAsset`, `ServiceJobViewModel`, `ProcessManager`, etc.).
- **Async Fixes (CS4014)**: Verified async/await usage across the application.
- **Architecture**: Enforced strict separation of concerns (Views strictly for UI, ViewModels for logic).

## 2026-02-07 (v6.3.1 - Critical API Fixes)

### 🛠️ API Stabilization
- **Middleware Fixes**: Resolved 500 errors by correcting `UseAuthentication` and `UseAuthorization` order.
- **Static Files**: Enabled `UseStaticFiles` and created `wwwroot` to prevent crashes.
- **Database**: Fixed `appsettings.json` connection string and successfully applied initial migrations (`AutoFix_InitialCreate`).
- **Swagger**: Ensured Swagger UI is available for API testing.

## 2026-02-07 (v6.2 - Architecture & Web Technician Integration)

### 🏆 Enterprise Architecture & Web Integration (Final Phase)
Backend API ve Web/Masaüstü istemcileri arasındaki entegrasyon tamamlandı.

- **API Controllers**:
  - `TechnicianController`: Teknisyenlerin kendilerine atanan görevleri görmesi ve durum güncellemesi için eklendi ([Authorize]).
  - `AdminController`: Yöneticilerin görev oluşturması ve ataması için eklendi ([Authorize(Roles = "Admin")]).
  - `AuthController`: JWT token üretiminde `ClaimTypes.NameIdentifier` eksikliği giderildi.
  - `AllowAll` CORS politikası onaylandı.

- **WPF Client Integration**:
  - `ApiService`: `HttpClient` tabanlı API katmanı oluşturuldu.
  - `LoginViewModel`: API üzerinden gerçek `LoginAsync` işlemi yapacak şekilde güncellendi. Token saklama mekanizması entegre edildi.
  - `ServiceJob.cs` (Shared): `Title` ve `AssignedTechnicianId` özellikleri eklendi.

- **Web Technician Panel**:
  - `TechnicianPanel.razor`: Teknisyenlerin görevlerini listelemesi ve durumlarını (Bekliyor, Devam Ediyor, Tamamlandı) güncellemesi için yeni sayfa oluşturuldu.
  - `MainLayout`: Giriş yapmış kullanıcılar için "Teknisyen Paneli" linki eklendi.
  - **Critical Fix**: `IAuthService` hatası giderildi ve `ApiAuthenticationStateProvider` stabil hale getirildi.

## 2026-02-07 (v6.0 - Greenfield Reconfiguration)
...
