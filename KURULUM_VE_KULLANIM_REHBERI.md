# 🛋️ Furnish AR — Yüzey Tarama, Elle Yerleştirme/Döndürme ve Home Screen (Turuncu Temalı Sürüm)

Bu proje, paylaştığınız YouTube eğitimindeki (**freeCodeCamp - Project-Based Augmented Reality Course with Unity Engine and AR Foundation**, `1:28:24` **Furnish AR** bölümü) yüzey tarayıp odaya eşya yerleştirme projesinin **Unity 2021.3.15f1** için sıfırdan, eksiksiz ve **turuncu tonlarda özelleştirilmiş arayüz + Home Screen + Elle Taşıma & Döndürme** ile hazırlanmış halidir.

---

## ✨ Eklenen Yeni Özellikler (Mevcut Hiçbir Şey Değiştirilmeden Eklendi)

1. **Turuncu Temalı Home Screen (`HomeScreenPanel`):**
   * Uygulama başladığında sizi turuncu tonlarda şık bir **Giriş Ekranı (Home Screen)** karşılar.
   * Üzerindeki **`BAŞLA - AR EKRANINI AÇ`** butonuna bastığınızda doğrudan ana AR ekranı açılır.
   * Mevcut sahneye otomatik olarak (`FurnishAR > Mevcut Sahneye Sadece Home Screen Ekle` veya Unity penceresine tıkladığınızda otomatik olarak) eklenir.

2. **Elle Yerleştirme, Sürükleyerek Taşıma ve Elle Döndürme:**
   * **Mobilde (Dokunmatik Ekran):**
     * **Tek Parmakla Boş Zemine Dokun / Sürükle:** Eşyayı zemine yerleştirir; parmağınızı kaldırmadan kaydırırsanız eşyayı zeminde elle gezdirir.
     * **Tek Parmakla Mevcut Eşyayı Tutup Sürükle:** Yerleştirilmiş bir eşyaya dokunup parmağınızı sürükleyerek odada istediğiniz yere elle taşıyabilirsiniz.
     * **İki Parmakla Çevir / Kaydır (Twist & Pinch):** İki parmağınızı ekranda çevirerek veya sağa-sola kaydırarak eşyayı **elle 360° döndürebilir**, iki parmağınızı açıp kapatarak boyutlandırabilirsiniz.
   * **Bilgisayarda (Unity Editor Play Modu - Fare İle):**
     * **Sol Tık + Sürükle:** Eşyayı zemine yerleştirir veya mevcut eşyayı tutup fareyle istediğiniz yere **elle sürükleyerek taşır**.
     * **Sağ Tık Basılı Tutup Sağa/Sola Sürükle (veya Fare Tekerleği):** Seçili eşyayı fareyle **elle 360° döndürür**.
   * **Mevcut Butonlar:** Üstteki `< Sol Döndür`, `Sağ Döndür >`, `- Küçült`, `+ Büyüt`, `Yüzey: Açık/Gizli`, `Geri Al` ve `Temizle` butonları da aynı şekilde çalışmaya devam eder.
