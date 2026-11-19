# 🛋️ Orange Furnished AR — Artırılmış Gerçeklik (AR) Oda & Mobilya Tasarım Uygulaması

Numara:2022556043
İsim-Soyisim: Pelin Gül Kızılkılıç


**Unity 2021.3.15f1** ve **AR Foundation (ARCore)** kullanılarak geliştirilmiş, gerçek dünyadaki yüzeyleri tarayıp 3 boyutlu mobilyaları odaya yerleştirmeyi, parmakla sürükleyerek taşımayı ve döndürmeyi sağlayan **Turuncu Temalı** Artırılmış Gerçeklik (AR) uygulaması.

---

## ✨ Temel Özellikler

* **Gerçek Zamanlı Yüzey Tarama (`ARPlaneManager` & `ARRaycastManager`):** Kamera ile odadaki yatay zeminleri algılar ve turuncu sınır çizgili AR düzlemleri oluşturur.
* **Elle Yerleştirme, Sürükleyerek Taşıma ve Döndürme:**
  * **Tek Parmak Dokunuş & Sürükleme:** Boş zemine dokunarak eşya yerleştirme ve mevcut eşyayı parmakla sürükleyerek odada istenilen yere taşıma.
  * **İki Parmakla Döndürme & Boyutlandırma (Twist & Pinch):** İki parmakla eşyayı 360° döndürme ve yaklaştırıp uzaklaştırarak boyutlandırma.
* **Alttan Açılan Turuncu Hamburger Menü (`BottomMenuDrawer`):**
  * Ekranın üst kısmını tamamen kamera görüntüsüne bırakan, alttaki **`☰ MENÜ & EŞYALAR`** hamburger butonuyla açılıp kapanan birleşik kontrol çekmecesi.
  * İçerisinde 5 farklı 3B Low-Poly mobilya seçimi (`Modern Kanepe`, `Tekli Berjer`, `Orta Sehpa`, `Tripod Lambader`, `Dekoratif Kitaplık`) ve hızlı kontrol butonları (`< Sol Döndür`, `Sağ Döndür >`, `- Küçült`, `+ Büyüt`, `Yüzey: Açık/Gizli`, `Geri Al`, `Temizle`) yer alır.
* **Turuncu Temalı Giriş Ekranı (`HomeScreenPanel`):** Uygulama açılışında karşılayan ve tek butonla (`BAŞLA - AR EKRANINI AÇ`) AR sahnesine geçiş sağlayan ana menü ekranı.
* **Unity Editor Test Modu:** Telefon bağlamadan bilgisayar üzerinde fare ile (Sol Tık: Yerleştir/Sürükle, Sağ Tık: Döndür) tüm özellikleri test edebilme imkanı.

---

## 📂 Proje Yapısı

* `Assets/Scenes/FurnishARScene.unity` — Ana AR Sahnesi
* `Assets/Scripts/FurniturePlacementManager.cs` — Yüzey tarama, eşya yerleştirme, elle taşıma/döndürme ve UI yönetim kodu
* `Assets/Models/Furnitures/` — 5 adet Low-Poly `.obj` ve `.mtl` 3B mobilya modeli
* `Assets/Prefabs/` — Hazır mobilya Prefab'ları ve `AR Default Plane` prefab'ı
* `Assets/UI/Sprites/` — 3B mobilya ikonları, uygulama ikonu (`AppIcon.png`) ve arayüz görselleri
* `Assets/Editor/FurnishARProjectSetup.cs` — Sahne, arayüz ve APK yapılandırma aracı
