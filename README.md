<a id="top"></a>

# ITAD Library Sync

<p align="center">
  <a href="https://www.patreon.com/16495069/join" target="_blank"><img src="https://img.shields.io/badge/Support%20on-Patreon-FF424D?style=for-the-badge&logo=patreon&logoColor=white" alt="Support on Patreon" /></a>
  &nbsp;&nbsp;
  <a href="#english-documentation"><img src="https://img.shields.io/badge/Language-English%20%F0%9F%87%AC%F0%9F%87%B7-blue?style=for-the-badge" alt="English Documentation" /></a>
  &nbsp;&nbsp;
  <a href="#turkish-documentation"><img src="https://img.shields.io/badge/Dil-T%C3%BCrk%C3%A7e%20%F0%9F%87%B9%F0%9F%87%B7-red?style=for-the-badge" alt="Türkçe Dokümantasyon" /></a>
</p>

---

<a id="english-documentation"></a>
## 🇬🇧 ITAD Library Sync - English

[![Support on Patreon](https://img.shields.io/badge/Support%20on-Patreon-FF424D?style=for-the-badge&logo=patreon&logoColor=white)](https://www.patreon.com/16495069/join)
[![GitHub Release](https://img.shields.io/github/v/release/Tunamaran/ITAD.LibrarySync?color=blue&style=for-the-badge)](https://github.com/Tunamaran/ITAD.LibrarySync/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=for-the-badge)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6?style=for-the-badge&logo=windows)](https://github.com/Tunamaran/ITAD.LibrarySync)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

**ITAD Library Sync** is a lightweight, modern Windows WPF system tray application that automatically synchronizes your local game libraries from **Epic Games Store, Ubisoft Connect, Battle.net, Xbox / Microsoft Store, and EA App** to your [IsThereAnyDeal](https://isthereanydeal.com/) Collection and Waitlist profiles.

---

### 🌟 Key Features

- **⚡ 100% Local & Password-Free Launcher Sync:** 
  Reads games directly from local encrypted launcher caches and Windows registries for Epic, Ubisoft, Battle.net, and EA App. No launcher login credentials required!
- **🤖 Smart Match Engine (SmartMatchEngine):**
  Automatically normalizes game titles, handles franchise prefixes (*Sid Meier's, Tom Clancy's, EA SPORTS*), formats DLC colons (*Base: DLC*), and strips regional tags so games match ITAD database IDs seamlessly.
- **🧹 Automatic ITAD Obsolete Entry Purging:**
  Automatically identifies and deletes obsolete or mis-matched game IDs from your ITAD Waitlist/Collection right after a successful sync.
- **🔍 Multi-Drive Deep Scanner:**
  Scans all fixed drives (`C:`, `D:`, `E:`, `F:`) to discover games installed across custom directories.
- **📊 Searchable Library Preview & Diagnostic Info:**
  Inspect your full game list, store IDs, detected folder paths (`📌 Resolved Path`), and detection methods (`🔍 Detection Source`) before running a sync.
- **🧩 Unmatched Games & Custom Mappings Manager:**
  Review games not automatically matched in ITAD's catalog, paste an ITAD page URL (e.g., `https://isthereanydeal.com/game/syberia-ii/info/`) or slug to create custom mappings, view/delete saved rules, and trigger dedicated single-click syncs for custom mapped games!
- **🌐 Dual Language Support (English & Türkçe):**
  Full English interface by default, with dynamic runtime switching to Turkish in Settings.
- **⏰ Automatic & Tray Background Synchronization:**
  Runs silently in the system tray with customizable auto-sync intervals (6h, 12h, 24h, weekly) or manual one-click sync.

---

### 🎮 Supported Platform Launcher Matrix

| Platform Launcher | Local Sync Method | Credentials Required? | Wishlist Sync |
| :--- | :--- | :---: | :---: |
| **Epic Games Store** | Local Manifests + Multi-Drive Scan | ❌ No | ✅ Best-effort (Local Cache) |
| **Ubisoft Connect** | Encrypted `configurations` Cache + Registry | ❌ No | ➖ N/A |
| **Battle.net** | Local Product Database Scan | ❌ No | ➖ N/A |
| **EA App** | Local Encrypted `IS` Cache Scan | ❌ No | ➖ N/A |
| **Xbox / Microsoft Store** | Installed Apps + Xbox Live OAuth Play History | 🔑 Xbox Sign-In | ➖ N/A |

---

### 🚀 Installation & Getting Started

1. **Download the App:**
   - Download `ITAD.LibrarySync-Setup-vX.X.X.exe` (Installer) or `ITAD.LibrarySync-win-x64.zip` (Portable) from [GitHub Releases](https://github.com/Tunamaran/ITAD.LibrarySync/releases).
2. **First-Run Setup:**
   - Launch the application and click **"Connect to ITAD"** to sign in via the secure web login (OAuth2 PKCE).
3. **Verify Detected Libraries:**
   - Go to **Settings → Platforms** and click **Test** or **Details** next to any launcher to preview detected games.
4. **Sync Your Library:**
   - Click **"Sync Now"** in Settings or right-click the System Tray icon and select **"Sync Now"**.

---

### ⚙️ How EA App & Xbox Sync Work

- **EA App:** 
  Works 100% locally from EA's encrypted `IS` cache file on your PC. You do not need to sign in to EA inside the app. If decryption fails, simply open and launch EA App once while online so EA can refresh its local cache key.
- **Xbox / Microsoft Store:**
  Combines PC local app manifest scanning with Xbox Live Title History. Connect your Xbox account under **Settings → Platforms → Xbox Connect** for full coverage including Xbox Game Pass games.

---

### 🛠️ Building From Source

Prerequisites:
- **Windows 10/11 x64**
- **.NET 8.0 SDK**

```powershell
# Clone the repository
git clone https://github.com/Tunamaran/ITAD.LibrarySync.git
cd ITAD.LibrarySync

# Build the project
dotnet build

# Run the app locally
dotnet run --project src/ITAD.LibrarySync.App

# Publish a self-contained release binary
dotnet publish src/ITAD.LibrarySync.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

<p align="right">
  <a href="#top"><b>⬆ Back to Top</b></a> &nbsp;|&nbsp; 
  <a href="#turkish-documentation"><b>🇹🇷 Switch to Türkçe</b></a>
</p>

---

<br/>

<a id="turkish-documentation"></a>
## 🇹🇷 ITAD Library Sync - Türkçe

[![Patreon'da Destek Ol](https://img.shields.io/badge/Patreon'da-Destek%20Ol-FF424D?style=for-the-badge&logo=patreon&logoColor=white)](https://www.patreon.com/16495069/join)

**ITAD Library Sync**, **Epic Games Store, Ubisoft Connect, Battle.net, Xbox / Microsoft Store ve EA App** kütüphanelerinizdeki oyunları yerel olarak tarayıp [IsThereAnyDeal](https://isthereanydeal.com/) (ITAD) Koleksiyon ve İstek Listesi (Waitlist) profillerinize otomatik aktaran hafif, modern bir Windows WPF sistem tepsisi (system tray) uygulamasıdır.

---

### 🌟 Öne Çıkan Özellikler

- **⚡ %100 Yerel ve Şifresiz Platform Taraması:**
  Epic Games, Ubisoft Connect, Battle.net ve EA App kütüphanelerinizi bilgisayarınızdaki şifreli kütüphane dosyalarından ve sistem kayıt defterinden okur. Hiçbir platform kullanıcı adı/şifresi saklanmaz!
- **🤖 Akıllı Eşleştirme Motoru (SmartMatchEngine):**
  Oyun isimlerini otomatik temizler, seri ön eklerini (*Sid Meier's, Tom Clancy's, EA SPORTS*) düzenler, DLC iki nokta üst üste biçimlendirmelerini (*Ana Oyun: DLC*) yapar ve ITAD veritabanı ID'leri ile kusursuz eşleştirir.
- **🧹 Otomatik ITAD Hatalı Kayıt Temizliği (Auto-Purge):**
  Başarılı bir senkronizasyonun ardından, ITAD hesabınızda bulunan eski veya yanlış eşleşmiş oyun ID'lerini tespit ederek ITAD profillerinizden otomatik olarak siler.
- **🔍 Tüm Sürücüleri Derinlemesine Tarama (Multi-Drive Scan):**
  `C:`, `D:`, `E:`, `F:` gibi tüm sabit disklerinizi tarayarak farklı sürücülere kurulu oyunlarınızı eksiksiz tespit eder.
- **📊 Aranabilir Kütüphane Önizlemesi ve Tanılama Kartı:**
  Senkronize etmeden önce tüm oyun listenizi, mağaza ID'lerini, tespit edilen klasör yolunu (`📌 Tespit Edilen Yol`) ve tarama metodunu (`🔍 Tespit Metodu`) detaylarıyla inceleyin.
- **🧩 Eşleşmeyen Oyun Yönetimi & Özel Eşleştirme Kuralları (Custom Mappings):**
  ITAD kataloğunda otomatik bulunamayan oyunları inceleyin, doğrudan ITAD web adresi (`https://isthereanydeal.com/game/syberia-ii/info/`) veya oyun slug'ı girerek eşleştirin, kurallarınızı yönetin ve sadece özel eşleştirilmiş oyunlarınızı tek tıkla ITAD'a aktarın!
- **🌐 Çift Dil Desteği (İngilizce & Türkçe):**
  Varsayılan İngilizce arayüz, Ayarlar sekmesinden tek tıkla anında Türkçe yapılabilir.
- **⏰ Arka Planda Otomatik Senkronizasyon:**
  Sistem tepsisinde (tray) sessizce çalışır; belirlediğiniz zaman aralıklarında (6 saat, 12 saat, 24 saat, haftalık) kütüphanelerinizi güncel tutar.

---

### 🎮 Desteklenen Platform Matrisi

| Platform İstemcisi | Yerel Tarama Metodu | Şifre / Giriş Gerekli mi? | İstek Listesi |
| :--- | :--- | :---: | :---: |
| **Epic Games Store** | Yerel Manifestler + Çoklu Sürücü Taraması | ❌ Hayır | ✅ Desteklenir (Yerel Önbelek) |
| **Ubisoft Connect** | Şifreli `configurations` Önbelleği + Registry | ❌ Hayır | ➖ N/A |
| **Battle.net** | Yerel Ürün Veritabanı Taraması | ❌ Hayır | ➖ N/A |
| **EA App** | Yerel Şifreli `IS` Önbellek Taraması | ❌ Hayır | ➖ N/A |
| **Xbox / Microsoft Store** | Kurulu Uygulamalar + Xbox Live Oynama Geçmişi | 🔑 Xbox Girişi | ➖ N/A |

---

### 🚀 Kurulum ve Kullanım

1. **Uygulamayı İndirin:**
   - [GitHub Releases](https://github.com/Tunamaran/ITAD.LibrarySync/releases) sayfasından `ITAD.LibrarySync-Setup-vX.X.X.exe` (Kurulum dosyası) veya `ITAD.LibrarySync-win-x64.zip` (Portatif) sürümünü indirin.
2. **İlk Kurulum Sihirbazı:**
   - Uygulamayı çalıştırın ve **"ITAD'a Bağlan"** butonuna basarak güvenli web tarayıcı penceresinden (OAuth2 PKCE) IsThereAnyDeal hesabınızla giriş yapın.
3. **Tespit Edilen Oyunları İnceleyin:**
   - **Ayarlar → Platformlar** sekmesinden istediğiniz platformun yanındaki **Test Et** veya **Detaylar** butonuna basarak okunan oyunları kontrol edin.
4. **Senkronize Edin:**
   - Ayarlar penceresinden **"Şimdi Senkronize Et"** butonuna basın veya sağ alttaki sistem tepsisi simgesine sağ tıklayıp **"Şimdi Senkronize Et"** seçeneğini seçin.

---

### 📜 Lisans

Bu proje **MIT Lisansı** altında lisanslanmıştır — detaylar için [LICENSE](LICENSE) dosyasına bakabilirsiniz.

---

<p align="right">
  <a href="#top"><b>⬆ Başa Dön / Back to Top</b></a> &nbsp;|&nbsp; 
  <a href="#english-documentation"><b>🇬🇧 English Version</b></a>
</p>
