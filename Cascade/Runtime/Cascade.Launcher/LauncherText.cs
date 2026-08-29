using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cascade.Launcher
{
    /// <summary>
    /// Launcher / patch UI 迷你多语言词表（AOT，脚本内嵌）。
    /// 为什么不走 JSON：补丁界面在资源系统初始化之前就要显示，任何 IO/资源加载失败都会让「错误提示」本身失效。
    /// 词条少（约 30 个），脚本字典编译期可见、零 IO、AOT 安全。
    /// 语言：en / zh-CN / zh-TW / fr / de / id / pt / ru / es / th / tr / ko / ja / ar；默认 en。
    /// </summary>
    public static class LauncherText
    {
        // ---- keys：英文常量同时是 fallback 文案（语言缺失/词条缺失时兜底显示英文）----
        public const string Starting = "Starting…";
        public const string Retrying = "Retrying…";
        public const string Cancelled = "Cancelled.";
        public const string CheckInstall = "Checking installation…";
        public const string InitResource = "Initializing resource system…";
        public const string CheckVersion = "Checking resource version…";
        public const string LoadManifest = "Loading resource manifest {0}…";
        public const string NetworkOfflineUseLocal = "Network unavailable; using local resource version {0}…";
        public const string CountingUpdate = "Counting update contents…";
        public const string UpdateFoundStartDownload = "Update found ({0}); starting download…";
        public const string StartDownload = "Starting download…";
        public const string DownloadingPercent = "Downloading {0}%";
        public const string DownloadingSize = "Downloading {0}% ({1}/{2})";
        public const string DownloadComplete = "Download complete.";
        public const string UpToDate = "Resources are up to date.";
        public const string CleaningObsolete = "Cleaning obsolete resources…";
        public const string EditorSimulate = "Using Editor simulated resources.";
        public const string BuiltinResource = "Using built-in resources.";
        public const string NoAotMetadata = "No additional AOT metadata needed.";
        public const string LoadingAotMetadata = "Loading AOT metadata ({0})…";
        public const string LoadingLocalization = "Loading language config…";
        public const string LoadingHotUpdate = "Loading hot-update code…";
        public const string LaunchingGame = "Starting game logic…";
        public const string Completed = "Done. Resource version={0}";

        // 补丁确认弹窗
        public const string ConfirmUpdate = "Game update detected; {0} to download.";
        public const string WaitingConfirm = "Waiting for update confirmation…";

        // 错误（LauncherFlow.GetUserFacingErrorMessage）
        public const string ErrorTitle = "An error occurred";
        public const string ErrorInitResource = "Failed to initialize resources. Check your network and try again.";
        public const string ErrorNoServerNoLocal = "Cannot connect to the update server and no local resources are available. Connect to the network and try again.";
        public const string ErrorLocalIncomplete = "Local resources are incomplete. Connect to the network to complete the update and try again.";
        public const string ErrorDownload = "Failed to download the update. Check your network and try again.";
        public const string ErrorLoadLocalization = "Failed to load language config. Please try again.";
        public const string ErrorLoadAotMetadata = "Failed to load game components. Please try again.";
        public const string ErrorLoadGameCode = "Failed to load game code. Please try again.";
        public const string ErrorLaunchGame = "Failed to start the game. Please try again.";
        public const string ErrorResource = "Resource update failed. Check your network and try again.";
        public const string ErrorLoadGame = "Failed to load the game. Please try again.";
        public const string ErrorUnknown = "An error occurred. Please try again.";

        /// <summary>语言代码（与 LocalizationService.NormalizeLocaleCode 规范一致）。</summary>
        public static readonly IReadOnlyList<string> SupportedLocales = new[]
        {
            "en", "zh-CN", "zh-TW", "fr", "de", "id", "pt", "ru",
            "es", "th", "tr", "ko", "ja", "ar"
        };

        // [locale][key] -> 文案。索引用英文常量串（即 key 本身），值数组顺序与 SupportedLocales 严格一致（测试保证）。
        private static readonly Dictionary<string, string[]> Tables = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["en"] = new[]
            {
                Starting, Retrying, Cancelled, CheckInstall, InitResource, CheckVersion,
                LoadManifest, NetworkOfflineUseLocal, CountingUpdate, UpdateFoundStartDownload,
                StartDownload, DownloadingPercent, DownloadingSize, DownloadComplete, UpToDate,
                CleaningObsolete, EditorSimulate, BuiltinResource, NoAotMetadata, LoadingAotMetadata,
                LoadingLocalization, LoadingHotUpdate, LaunchingGame, Completed,
                ConfirmUpdate, WaitingConfirm,
                ErrorTitle, ErrorInitResource, ErrorNoServerNoLocal, ErrorLocalIncomplete,
                ErrorDownload, ErrorLoadLocalization, ErrorLoadAotMetadata, ErrorLoadGameCode,
                ErrorLaunchGame, ErrorResource, ErrorLoadGame, ErrorUnknown,
            },
            ["zh-CN"] = new[]
            {
                "正在启动…", "正在重试…", "已取消。", "检查安装…", "初始化资源系统…", "检查资源版本…",
                "加载资源清单 {0}…", "网络不可用，使用本地资源版本 {0}…", "统计更新内容…", "检测到更新 {0}，开始下载…",
                "开始下载…", "下载中 {0}%", "下载中 {0}%（{1}/{2}）", "下载完成。", "资源已是最新。",
                "清理过期资源…", "使用 Editor 模拟资源。", "使用内置资源。", "无需补充 AOT 元数据。", "加载 AOT 元数据（{0}）…",
                "加载语言配置…", "加载热更代码…", "启动游戏逻辑…", "完成 资源版本={0}",
                "检测到游戏更新，需要下载 {0}", "等待确认更新…",
                "出现错误", "初始化资源失败，请检查网络后重试。", "无法连接更新服务器，且本地没有可用资源，请联网后重试。", "本地资源不完整，请联网完成更新后重试。",
                "下载更新失败，请检查网络后重试。", "加载语言配置失败，请重试。", "加载游戏组件失败，请重试。", "加载游戏代码失败，请重试。",
                "启动游戏失败，请重试。", "资源更新失败，请检查网络后重试。", "加载游戏失败，请重试。", "出现错误，请重试。",
            },
            ["zh-TW"] = new[]
            {
                "正在啟動…", "正在重試…", "已取消。", "檢查安裝…", "初始化資源系統…", "檢查資源版本…",
                "載入資源清單 {0}…", "網路不可用，使用本機資源版本 {0}…", "統計更新內容…", "偵測到更新 {0}，開始下載…",
                "開始下載…", "下載中 {0}%", "下載中 {0}%（{1}/{2}）", "下載完成。", "資源已是最新。",
                "清理過期資源…", "使用 Editor 模擬資源。", "使用內建資源。", "無需補充 AOT 中繼資料。", "載入 AOT 中繼資料（{0}）…",
                "載入語言設定…", "載入熱更新程式碼…", "啟動遊戲邏輯…", "完成 資源版本={0}",
                "偵測到遊戲更新，需要下載 {0}", "等待確認更新…",
                "出現錯誤", "初始化資源失敗，請檢查網路後重試。", "無法連線更新伺服器，且本機沒有可用資源，請連上網路後重試。", "本機資源不完整，請連上網路完成更新後重試。",
                "下載更新失敗，請檢查網路後重試。", "載入語言設定失敗，請重試。", "載入遊戲元件失敗，請重試。", "載入遊戲程式碼失敗，請重試。",
                "啟動遊戲失敗，請重試。", "資源更新失敗，請檢查網路後重試。", "載入遊戲失敗，請重試。", "出現錯誤，請重試。",
            },
            ["fr"] = new[]
            {
                "Démarrage…", "Nouvelle tentative…", "Annulé.", "Vérification de l'installation…", "Initialisation du système de ressources…", "Vérification de la version des ressources…",
                "Chargement du manifeste de ressources {0}…", "Réseau indisponible ; utilisation de la version locale {0}…", "Calcul du contenu de la mise à jour…", "Mise à jour détectée ({0}) ; début du téléchargement…",
                "Début du téléchargement…", "Téléchargement {0}%", "Téléchargement {0}% ({1}/{2})", "Téléchargement terminé.", "Les ressources sont à jour.",
                "Nettoyage des ressources obsolètes…", "Utilisation des ressources simulées par l'éditeur.", "Utilisation des ressources intégrées.", "Aucune métadonnée AOT supplémentaire requise.", "Chargement des métadonnées AOT ({0})…",
                "Chargement de la configuration linguistique…", "Chargement du code de mise à jour…", "Démarrage de la logique du jeu…", "Terminé. Version des ressources={0}",
                "Mise à jour du jeu détectée ; {0} à télécharger.", "En attente de confirmation de la mise à jour…",
                "Une erreur est survenue", "Échec de l'initialisation des ressources. Vérifiez votre réseau et réessayez.", "Impossible de joindre le serveur de mise à jour et aucune ressource locale n'est disponible. Connectez-vous au réseau et réessayez.", "Les ressources locales sont incomplètes. Connectez-vous au réseau pour terminer la mise à jour puis réessayez.",
                "Échec du téléchargement de la mise à jour. Vérifiez votre réseau et réessayez.", "Échec du chargement de la configuration linguistique. Veuillez réessayer.", "Échec du chargement des composants du jeu. Veuillez réessayer.", "Échec du chargement du code du jeu. Veuillez réessayer.",
                "Échec du démarrage du jeu. Veuillez réessayer.", "Échec de la mise à jour des ressources. Vérifiez votre réseau et réessayez.", "Échec du chargement du jeu. Veuillez réessayer.", "Une erreur est survenue. Veuillez réessayer.",
            },
            ["de"] = new[]
            {
                "Starte…", "Wiederhole…", "Abgebrochen.", "Prüfe Installation…", "Initialisiere Ressourcensystem…", "Prüfe Ressourcenversion…",
                "Lade Ressourcenmanifest {0}…", "Netzwerk nicht verfügbar; verwende lokale Ressourcenversion {0}…", "Zähle Update-Inhalte…", "Update gefunden ({0}); Download startet…",
                "Starte Download…", "Lade {0}%", "Lade {0}% ({1}/{2})", "Download abgeschlossen.", "Ressourcen sind aktuell.",
                "Räume veraltete Ressourcen auf…", "Verwende Editor-Simulationsressourcen.", "Verwende integrierte Ressourcen.", "Keine zusätzlichen AOT-Metadaten erforderlich.", "Lade AOT-Metadaten ({0})…",
                "Lade Sprachkonfiguration…", "Lade Hot-Update-Code…", "Starte Spiellogik…", "Fertig. Ressourcenversion={0}",
                "Spielupdate erkannt; {0} werden heruntergeladen.", "Warte auf Update-Bestätigung…",
                "Ein Fehler ist aufgetreten", "Ressourceninitialisierung fehlgeschlagen. Netzwerk prüfen und erneut versuchen.", "Updateserver nicht erreichbar und keine lokalen Ressourcen verfügbar. Mit dem Netzwerk verbinden und erneut versuchen.", "Lokale Ressourcen unvollständig. Mit dem Netzwerk verbinden, Update abschließen und erneut versuchen.",
                "Download des Updates fehlgeschlagen. Netzwerk prüfen und erneut versuchen.", "Sprachkonfiguration konnte nicht geladen werden. Bitte erneut versuchen.", "Spielkomponenten konnten nicht geladen werden. Bitte erneut versuchen.", "Spielcode konnte nicht geladen werden. Bitte erneut versuchen.",
                "Spielstart fehlgeschlagen. Bitte erneut versuchen.", "Ressourcenupdate fehlgeschlagen. Netzwerk prüfen und erneut versuchen.", "Spiel konnte nicht geladen werden. Bitte erneut versuchen.", "Ein Fehler ist aufgetreten. Bitte erneut versuchen.",
            },
            ["id"] = new[]
            {
                "Memulai…", "Mencoba lagi…", "Dibatalkan.", "Memeriksa instalasi…", "Menginisialisasi sistem sumber daya…", "Memeriksa versi sumber daya…",
                "Memuat manifes sumber daya {0}…", "Jaringan tidak tersedia; menggunakan versi sumber daya lokal {0}…", "Menghitung konten pembaruan…", "Pembaruan ditemukan ({0}); mulai mengunduh…",
                "Mulai mengunduh…", "Mengunduh {0}%", "Mengunduh {0}% ({1}/{2})", "Unduhan selesai.", "Sumber daya sudah yang terbaru.",
                "Membersihkan sumber daya usang…", "Menggunakan sumber daya simulasi Editor.", "Menggunakan sumber daya bawaan.", "Tidak perlu metadata AOT tambahan.", "Memuat metadata AOT ({0})…",
                "Memuat konfigurasi bahasa…", "Memuat kode pembaruan…", "Memulai logika game…", "Selesai. Versi sumber daya={0}",
                "Pembaruan game terdeteksi; {0} akan diunduh.", "Menunggu konfirmasi pembaruan…",
                "Terjadi kesalahan", "Gagal menginisialisasi sumber daya. Periksa jaringan Anda lalu coba lagi.", "Tidak dapat terhubung ke server pembaruan dan tidak ada sumber daya lokal. Hubungkan ke jaringan lalu coba lagi.", "Sumber daya lokal tidak lengkap. Hubungkan ke jaringan untuk menyelesaikan pembaruan lalu coba lagi.",
                "Gagal mengunduh pembaruan. Periksa jaringan Anda lalu coba lagi.", "Gagal memuat konfigurasi bahasa. Silakan coba lagi.", "Gagal memuat komponen game. Silakan coba lagi.", "Gagal memuat kode game. Silakan coba lagi.",
                "Gagal memulai game. Silakan coba lagi.", "Pembaruan sumber daya gagal. Periksa jaringan Anda lalu coba lagi.", "Gagal memuat game. Silakan coba lagi.", "Terjadi kesalahan. Silakan coba lagi.",
            },
            ["pt"] = new[]
            {
                "Iniciando…", "Tentando novamente…", "Cancelado.", "Verificando instalação…", "Inicializando sistema de recursos…", "Verificando versão dos recursos…",
                "Carregando manifesto de recursos {0}…", "Rede indisponível; usando versão local de recursos {0}…", "Calculando conteúdo da atualização…", "Atualização encontrada ({0}); iniciando download…",
                "Iniciando download…", "Baixando {0}%", "Baixando {0}% ({1}/{2})", "Download concluído.", "Os recursos estão atualizados.",
                "Limpando recursos obsoletos…", "Usando recursos simulados do Editor.", "Usando recursos integrados.", "Nenhum metadado AOT adicional necessário.", "Carregando metadados AOT ({0})…",
                "Carregando configuração de idioma…", "Carregando código de atualização…", "Iniciando lógica do jogo…", "Concluído. Versão dos recursos={0}",
                "Atualização do jogo detectada; {0} para baixar.", "Aguardando confirmação da atualização…",
                "Ocorreu um erro", "Falha ao inicializar os recursos. Verifique sua rede e tente novamente.", "Não foi possível conectar ao servidor de atualização e não há recursos locais disponíveis. Conecte-se à rede e tente novamente.", "Os recursos locais estão incompletos. Conecte-se à rede para concluir a atualização e tente novamente.",
                "Falha ao baixar a atualização. Verifique sua rede e tente novamente.", "Falha ao carregar a configuração de idioma. Tente novamente.", "Falha ao carregar componentes do jogo. Tente novamente.", "Falha ao carregar o código do jogo. Tente novamente.",
                "Falha ao iniciar o jogo. Tente novamente.", "Falha na atualização de recursos. Verifique sua rede e tente novamente.", "Falha ao carregar o jogo. Tente novamente.", "Ocorreu um erro. Tente novamente.",
            },
            ["ru"] = new[]
            {
                "Запуск…", "Повтор…", "Отменено.", "Проверка установки…", "Инициализация системы ресурсов…", "Проверка версии ресурсов…",
                "Загрузка манифеста ресурсов {0}…", "Сеть недоступна; используется локальная версия ресурсов {0}…", "Подсчёт содержимого обновления…", "Обнаружено обновление ({0}); начинается загрузка…",
                "Начало загрузки…", "Загрузка {0}%", "Загрузка {0}% ({1}/{2})", "Загрузка завершена.", "Ресурсы актуальны.",
                "Очистка устаревших ресурсов…", "Используются ресурсы, имитируемые редактором.", "Используются встроенные ресурсы.", "Дополнительные метаданные AOT не требуются.", "Загрузка метаданных AOT ({0})…",
                "Загрузка языковых настроек…", "Загрузка кода горячего обновления…", "Запуск игровой логики…", "Готово. Версия ресурсов={0}",
                "Обнаружено обновление игры; скачать {0}.", "Ожидание подтверждения обновления…",
                "Произошла ошибка", "Не удалось инициализировать ресурсы. Проверьте сеть и повторите попытку.", "Невозможно подключиться к серверу обновлений, локальные ресурсы недоступны. Подключитесь к сети и повторите попытку.", "Локальные ресурсы неполны. Подключитесь к сети, завершите обновление и повторите попытку.",
                "Не удалось загрузить обновление. Проверьте сеть и повторите попытку.", "Не удалось загрузить языковые настройки. Повторите попытку.", "Не удалось загрузить компоненты игры. Повторите попытку.", "Не удалось загрузить код игры. Повторите попытку.",
                "Не удалось запустить игру. Повторите попытку.", "Не удалось обновить ресурсы. Проверьте сеть и повторите попытку.", "Не удалось загрузить игру. Повторите попытку.", "Произошла ошибка. Повторите попытку.",
            },
            ["es"] = new[]
            {
                "Iniciando…", "Reintentando…", "Cancelado.", "Comprobando instalación…", "Inicializando sistema de recursos…", "Comprobando versión de recursos…",
                "Cargando manifiesto de recursos {0}…", "Red no disponible; usando versión local de recursos {0}…", "Calculando contenido de la actualización…", "Actualización encontrada ({0}); iniciando descarga…",
                "Iniciando descarga…", "Descargando {0}%", "Descargando {0}% ({1}/{2})", "Descarga completada.", "Los recursos están actualizados.",
                "Limpiando recursos obsoletos…", "Usando recursos simulados del Editor.", "Usando recursos integrados.", "No se necesitan metadatos AOT adicionales.", "Cargando metadatos AOT ({0})…",
                "Cargando configuración de idioma…", "Cargando código de actualización…", "Iniciando lógica del juego…", "Listo. Versión de recursos={0}",
                "Actualización del juego detectada; {0} para descargar.", "Esperando confirmación de la actualización…",
                "Se ha producido un error", "No se pudieron inicializar los recursos. Comprueba tu red e inténtalo de nuevo.", "No se puede conectar con el servidor de actualización y no hay recursos locales disponibles. Conéctate a la red e inténtalo de nuevo.", "Los recursos locales están incompletos. Conéctate a la red para completar la actualización e inténtalo de nuevo.",
                "No se pudo descargar la actualización. Comprueba tu red e inténtalo de nuevo.", "No se pudo cargar la configuración de idioma. Inténtalo de nuevo.", "No se pudieron cargar los componentes del juego. Inténtalo de nuevo.", "No se pudo cargar el código del juego. Inténtalo de nuevo.",
                "No se pudo iniciar el juego. Inténtalo de nuevo.", "Falló la actualización de recursos. Comprueba tu red e inténtalo de nuevo.", "No se pudo cargar el juego. Inténtalo de nuevo.", "Se ha producido un error. Inténtalo de nuevo.",
            },
            ["th"] = new[]
            {
                "กำลังเริ่ม…", "กำลังลองอีกครั้ง…", "ยกเลิกแล้ว", "กำลังตรวจสอบการติดตั้ง…", "กำลังเริ่มต้นระบบทรัพยากร…", "กำลังตรวจสอบเวอร์ชันทรัพยากร…",
                "กำลังโหลดรายการทรัพยากร {0}…", "เครือข่ายไม่พร้อมใช้งาน ใช้ทรัพยากรเวอร์ชันท้องถิ่น {0}…", "กำลังคำนวณเนื้อหาอัปเดต…", "พบการอัปเดต ({0}) เริ่มดาวน์โหลด…",
                "เริ่มดาวน์โหลด…", "กำลังดาวน์โหลด {0}%", "กำลังดาวน์โหลด {0}% ({1}/{2})", "ดาวน์โหลดเสร็จสิ้น", "ทรัพยากรเป็นเวอร์ชันล่าสุดแล้ว",
                "กำลังล้างทรัพยากรที่ล้าสมัย…", "ใช้ทรัพยากรจำลองของ Editor", "ใช้ทรัพยากรในตัว", "ไม่จำเป็นต้องใช้เมทาดาทา AOT เพิ่มเติม", "กำลังโหลดเมทาดาทา AOT ({0})…",
                "กำลังโหลดการตั้งค่าภาษา…", "กำลังโหลดโค้ดอัปเดต…", "กำลังเริ่มตรรกะเกม…", "เสร็จสิ้น เวอร์ชันทรัพยากร={0}",
                "พบการอัปเดตเกม ต้องดาวน์โหลด {0}", "กำลังรอการยืนยันการอัปเดต…",
                "เกิดข้อผิดพลาด", "ไม่สามารถเริ่มต้นทรัพยากรได้ โปรดตรวจสอบเครือข่ายแล้วลองอีกครั้ง", "ไม่สามารถเชื่อมต่อเซิร์ฟเวอร์อัปเดตและไม่มีทรัพยากรในเครื่อง โปรดเชื่อมต่ออินเทอร์เน็ตแล้วลองอีกครั้ง", "ทรัพยากรในเครื่องไม่สมบูรณ์ โปรดเชื่อมต่ออินเทอร์เน็ตเพื่ออัปเดตให้เสร็จแล้วลองอีกครั้ง",
                "ดาวน์โหลดอัปเดตล้มเหลว โปรดตรวจสอบเครือข่ายแล้วลองอีกครั้ง", "โหลดการตั้งค่าภาษาล้มเหลว โปรดลองอีกครั้ง", "โหลดส่วนประกอบเกมล้มเหลว โปรดลองอีกครั้ง", "โหลดโค้ดเกมล้มเหลว โปรดลองอีกครั้ง",
                "เริ่มเกมล้มเหลว โปรดลองอีกครั้ง", "อัปเดตทรัพยากรล้มเหลว โปรดตรวจสอบเครือข่ายแล้วลองอีกครั้ง", "โหลดเกมล้มเหลว โปรดลองอีกครั้ง", "เกิดข้อผิดพลาด โปรดลองอีกครั้ง",
            },
            ["tr"] = new[]
            {
                "Başlatılıyor…", "Yeniden deneniyor…", "İptal edildi.", "Kurulum kontrol ediliyor…", "Kaynak sistemi başlatılıyor…", "Kaynak sürümü kontrol ediliyor…",
                "Kaynak bildirimi yükleniyor {0}…", "Ağ kullanılamıyor; yerel kaynak sürümü {0} kullanılıyor…", "Güncelleme içeriği hesaplanıyor…", "Güncelleme bulundu ({0}); indirme başlıyor…",
                "İndirme başlıyor…", "İndiriliyor %{0}", "İndiriliyor %{0} ({1}/{2})", "İndirme tamamlandı.", "Kaynaklar güncel.",
                "Eski kaynaklar temizleniyor…", "Editor simülasyon kaynakları kullanılıyor.", "Yerleşik kaynaklar kullanılıyor.", "Ek AOT meta verisi gerekmiyor.", "AOT meta verisi yükleniyor ({0})…",
                "Dil yapılandırması yükleniyor…", "Sıcak güncelleme kodu yükleniyor…", "Oyun mantığı başlatılıyor…", "Tamamlandı. Kaynak sürümü={0}",
                "Oyun güncellemesi algılandı; {0} indirilecek.", "Güncelleme onayı bekleniyor…",
                "Bir hata oluştu", "Kaynaklar başlatılamadı. Ağınızı kontrol edip tekrar deneyin.", "Güncelleme sunucusuna bağlanılamıyor ve yerel kaynak yok. Ağa bağlanıp tekrar deneyin.", "Yerel kaynaklar eksik. Güncellemeyi tamamlamak için ağa bağlanıp tekrar deneyin.",
                "Güncelleme indirilemedi. Ağınızı kontrol edip tekrar deneyin.", "Dil yapılandırması yüklenemedi. Lütfen tekrar deneyin.", "Oyun bileşenleri yüklenemedi. Lütfen tekrar deneyin.", "Oyun kodu yüklenemedi. Lütfen tekrar deneyin.",
                "Oyun başlatılamadı. Lütfen tekrar deneyin.", "Kaynak güncellemesi başarısız oldu. Ağınızı kontrol edip tekrar deneyin.", "Oyun yüklenemedi. Lütfen tekrar deneyin.", "Bir hata oluştu. Lütfen tekrar deneyin.",
            },
            ["ko"] = new[]
            {
                "시작 중…", "다시 시도 중…", "취소되었습니다.", "설치 확인 중…", "리소스 시스템 초기화 중…", "리소스 버전 확인 중…",
                "리소스 매니페스트 {0} 로드 중…", "네트워크를 사용할 수 없어 로컬 리소스 버전 {0}을(를) 사용합니다…", "업데이트 내용 계산 중…", "업데이트 발견({0}); 다운로드 시작…",
                "다운로드 시작…", "다운로드 중 {0}%", "다운로드 중 {0}% ({1}/{2})", "다운로드 완료.", "리소스가 최신 상태입니다.",
                "오래된 리소스 정리 중…", "Editor 시뮬레이션 리소스 사용 중.", "내장 리소스 사용 중.", "추가 AOT 메타데이터가 필요하지 않습니다.", "AOT 메타데이터({0}) 로드 중…",
                "언어 설정 로드 중…", "핫 업데이트 코드 로드 중…", "게임 로직 시작 중…", "완료. 리소스 버전={0}",
                "게임 업데이트가 감지되었습니다. {0} 다운로드 필요.", "업데이트 확인 대기 중…",
                "오류가 발생했습니다", "리소스 초기화에 실패했습니다. 네트워크를 확인하고 다시 시도하세요.", "업데이트 서버에 연결할 수 없고 로컬 리소스가 없습니다. 네트워크에 연결하고 다시 시도하세요.", "로컬 리소스가 불완전합니다. 네트워크에 연결하여 업데이트를 완료한 후 다시 시도하세요.",
                "업데이트 다운로드에 실패했습니다. 네트워크를 확인하고 다시 시도하세요.", "언어 설정을 불러오지 못했습니다. 다시 시도해 주세요.", "게임 구성 요소를 불러오지 못했습니다. 다시 시도해 주세요.", "게임 코드를 불러오지 못했습니다. 다시 시도해 주세요.",
                "게임 시작에 실패했습니다. 다시 시도해 주세요.", "리소스 업데이트에 실패했습니다. 네트워크를 확인하고 다시 시도하세요.", "게임을 불러오지 못했습니다. 다시 시도해 주세요.", "오류가 발생했습니다. 다시 시도해 주세요.",
            },
            ["ja"] = new[]
            {
                "起動中…", "再試行中…", "キャンセルされました。", "インストール確認中…", "リソースシステム初期化中…", "リソースバージョン確認中…",
                "リソースマニフェスト {0} 読み込み中…", "ネットワークが利用できないため、ローカルリソースバージョン {0} を使用します…", "更新内容を計算中…", "更新を検出しました（{0}）。ダウンロードを開始します…",
                "ダウンロード開始…", "ダウンロード中 {0}%", "ダウンロード中 {0}%（{1}/{2}）", "ダウンロード完了。", "リソースは最新です。",
                "不要なリソースを整理中…", "Editor シミュレーションリソースを使用中。", "内蔵リソースを使用中。", "追加の AOT メタデータは不要です。", "AOT メタデータ（{0}）読み込み中…",
                "言語設定を読み込み中…", "ホットアップデートコードを読み込み中…", "ゲームロジックを起動中…", "完了。リソースバージョン={0}",
                "ゲーム更新を検出しました。{0} のダウンロードが必要です。", "更新の確認を待機中…",
                "エラーが発生しました", "リソースの初期化に失敗しました。ネットワークを確認して再試行してください。", "更新サーバーに接続できず、ローカルリソースもありません。ネットワークに接続して再試行してください。", "ローカルリソースが不完全です。ネットワークに接続して更新を完了し、再試行してください。",
                "更新のダウンロードに失敗しました。ネットワークを確認して再試行してください。", "言語設定を読み込めませんでした。もう一度お試しください。", "ゲームコンポーネントを読み込めませんでした。もう一度お試しください。", "ゲームコードを読み込めませんでした。もう一度お試しください。",
                "ゲームを起動できませんでした。もう一度お試しください。", "リソースの更新に失敗しました。ネットワークを確認して再試行してください。", "ゲームを読み込めませんでした。もう一度お試しください。", "エラーが発生しました。もう一度お試しください。",
            },
            ["ar"] = new[]
            {
                "جارٍ التشغيل…", "جارٍ إعادة المحاولة…", "تم الإلغاء.", "جارٍ التحقق من التثبيت…", "جارٍ تهيئة نظام الموارد…", "جارٍ التحقق من إصدار الموارد…",
                "جارٍ تحميل بيان الموارد {0}…", "الشبكة غير متاحة؛ يتم استخدام إصدار الموارد المحلي {0}…", "جارٍ حساب محتويات التحديث…", "تم العثور على تحديث ({0})؛ بدء التنزيل…",
                "بدء التنزيل…", "جارٍ التنزيل {0}%", "جارٍ التنزيل {0}% ({1}/{2})", "اكتمل التنزيل.", "الموارد محدّثة بالفعل.",
                "جارٍ تنظيف الموارد القديمة…", "يتم استخدام موارد المحرر المحاكاة.", "يتم استخدام الموارد المدمجة.", "لا حاجة لبيانات وصفية إضافية لـ AOT.", "جارٍ تحميل بيانات AOT الوصفية ({0})…",
                "جارٍ تحميل إعدادات اللغة…", "جارٍ تحميل كود التحديث…", "جارٍ تشغيل منطق اللعبة…", "اكتمل. إصدار الموارد={0}",
                "تم اكتشاف تحديث اللعبة؛ يجب تنزيل {0}.", "في انتظار تأكيد التحديث…",
                "حدث خطأ", "فشل تهيئة الموارد. تحقق من الشبكة وحاول مرة أخرى.", "تعذّر الاتصال بخادم التحديث ولا توجد موارد محلية متاحة. اتصل بالشبكة وحاول مرة أخرى.", "الموارد المحلية غير مكتملة. اتصل بالشبكة لإكمال التحديث وحاول مرة أخرى.",
                "فشل تنزيل التحديث. تحقق من الشبكة وحاول مرة أخرى.", "فشل تحميل إعدادات اللغة. يرجى المحاولة مرة أخرى.", "فشل تحميل مكونات اللعبة. يرجى المحاولة مرة أخرى.", "فشل تحميل كود اللعبة. يرجى المحاولة مرة أخرى.",
                "فشل تشغيل اللعبة. يرجى المحاولة مرة أخرى.", "فشل تحديث الموارد. تحقق من الشبكة وحاول مرة أخرى.", "فشل تحميل اللعبة. يرجى المحاولة مرة أخرى.", "حدث خطأ. يرجى المحاولة مرة أخرى.",
            },
        };

        private static string _currentLocale = "en";

        /// <summary>当前语言（规范代码，如 en）。默认 en。</summary>
        public static string CurrentLocale => _currentLocale;

        /// <summary>
        /// 解析初始语言：存档优先（PlayerPrefs f13.localization.locale）→ 系统语言 → en。
        /// </summary>
        public static void Initialize(string savedLocale = null)
        {
            var normalized = Normalize(savedLocale);
            if (string.IsNullOrEmpty(normalized))
                normalized = Normalize(GetSystemLocale());
            if (string.IsNullOrEmpty(normalized) || !Tables.ContainsKey(normalized))
                normalized = "en";
            _currentLocale = normalized;
        }

        /// <summary>取当前语言文案；key 缺失或语言缺失时返回 key 本身（英文 fallback）。</summary>
        public static string Get(string key)
        {
            if (Tables.TryGetValue(_currentLocale, out var table))
            {
                var index = IndexOfKey(key);
                if (index >= 0 && index < table.Length)
                    return table[index];
            }
            return key;
        }

        /// <summary>取文案并格式化占位符（{0} 等）。</summary>
        public static string Format(string key, params object[] args)
        {
            var text = Get(key);
            if (args == null || args.Length == 0)
                return text;
            try
            {
                return string.Format(CultureInfo.InvariantCulture, text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        /// <summary>key 常量在 SupportedLocales 中的下标（与 Tables 数组对齐）。</summary>
        public static int IndexOfKey(string key)
        {
            return Array.IndexOf(Keys, key);
        }

        private static readonly string[] Keys =
        {
            Starting, Retrying, Cancelled, CheckInstall, InitResource, CheckVersion,
            LoadManifest, NetworkOfflineUseLocal, CountingUpdate, UpdateFoundStartDownload,
            StartDownload, DownloadingPercent, DownloadingSize, DownloadComplete, UpToDate,
            CleaningObsolete, EditorSimulate, BuiltinResource, NoAotMetadata, LoadingAotMetadata,
            LoadingLocalization, LoadingHotUpdate, LaunchingGame, Completed,
            ConfirmUpdate, WaitingConfirm,
            ErrorTitle, ErrorInitResource, ErrorNoServerNoLocal, ErrorLocalIncomplete,
            ErrorDownload, ErrorLoadLocalization, ErrorLoadAotMetadata, ErrorLoadGameCode,
            ErrorLaunchGame, ErrorResource, ErrorLoadGame, ErrorUnknown,
        };

        private static string Normalize(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return string.Empty;
            var parts = locale.Trim().Replace('_', '-').Split('-');
            if (parts.Length == 1)
                return parts[0].ToLowerInvariant();
            return parts[0].ToLowerInvariant() + "-" + parts[1].ToUpperInvariant();
        }

        private static string GetSystemLocale()
        {
            switch (UnityEngine.Application.systemLanguage)
            {
                case UnityEngine.SystemLanguage.Chinese:
                case UnityEngine.SystemLanguage.ChineseSimplified: return "zh-CN";
                case UnityEngine.SystemLanguage.ChineseTraditional: return "zh-TW";
                case UnityEngine.SystemLanguage.French: return "fr";
                case UnityEngine.SystemLanguage.German: return "de";
                case UnityEngine.SystemLanguage.Indonesian: return "id";
                case UnityEngine.SystemLanguage.Portuguese: return "pt";
                case UnityEngine.SystemLanguage.Russian: return "ru";
                case UnityEngine.SystemLanguage.Spanish: return "es";
                case UnityEngine.SystemLanguage.Thai: return "th";
                case UnityEngine.SystemLanguage.Turkish: return "tr";
                case UnityEngine.SystemLanguage.Korean: return "ko";
                case UnityEngine.SystemLanguage.Japanese: return "ja";
                case UnityEngine.SystemLanguage.Arabic: return "ar";
                default: return "en";
            }
        }
    }
}
