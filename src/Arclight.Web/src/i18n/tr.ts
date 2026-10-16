import type { Messages } from './en';

/** Türkçe site metinleri. Terimler masaüstü uygulamasının Türkçe arayüzüyle aynı (Oyunları Tara, Oyun Ekle, Kütüphanem…). */
export const tr: Messages = {
  common: {
    download: 'İndir',
    downloadForWindows: 'Windows için indir',
    releaseNotes: 'Sürüm notları',
  },

  layout: {
    skipToContent: 'İçeriğe geç',
    homeLabel: 'Arclight ana sayfa',
    mainNav: 'Ana menü',
    features: 'Özellikler',
    platforms: 'Platformlar',
    privacy: 'Gizlilik',
    github: 'GitHub’da Arclight',
    language: 'Dil',
    footerNav: 'Alt bilgi',
    sourceCode: 'Kaynak kod',
    license: 'MIT Lisansı',
    legal:
      'Arclight bağımsız, açık kaynaklı bir projedir. Valve, Epic Games, Microsoft, CD PROJEKT, Ubisoft veya Electronic Arts ile hiçbir bağlantısı yoktur. Oyun adları ve ticari markalar sahiplerine aittir.',
  },

  titles: {
    home: 'Arclight — Your Games. One Universe.',
    download: 'Windows için Arclight’ı indir',
    changelog: 'Sürüm notları — Arclight',
    'not-found': 'Sayfa bulunamadı — Arclight',
  },

  notFound: {
    title: 'Bu sayfa yok',
    body: 'Bağlantı eski olabilir. Ana sayfaya dön.',
    action: 'Ana sayfaya git',
  },

  hero: {
    lead: 'Bilgisayarındaki oyunları tek ve güzel bir kütüphanede topla.',
    explore: 'Özellikleri keşfet',
    note: 'Ücretsiz ve açık kaynak; Windows 10 ve 11 için.',
    screenshotAlt: 'Arclight’ın Ana Sayfası: öne çıkan bir oyun, hızlı istatistikler, Oynamaya Devam Et ve Son Eklenenler.',
  },

  platforms: {
    title: 'Her başlatıcı, tek kütüphane',
    intro:
      'Oyunları Tara, her başlatıcının bilgisayarında zaten tuttuğu kurulum kayıtlarını okur. Başlatıcı şifresi istemez, başlatıcı dosyalarını değiştirmez ve tüm diskini taramaz. Bugün tam olarak neyin çalıştığı aşağıda.',
    columns: {
      platform: 'Platform',
      source: 'Arclight’ın okuduğu',
      launch: 'Oyunlar nasıl başlar',
      status: 'Algılama',
    },
    status: {
      supported: 'Otomatik algılanır',
      experimental: 'Deneysel',
      manual: 'Elle eklenir',
    },
    rows: {
      steam: { name: 'Steam', source: 'Steam kurulumundaki kütüphane klasörleri ve uygulama manifestleri', launch: 'Steam üzerinden' },
      epic: { name: 'Epic Games', source: 'Epic Games Launcher’ın kurulum manifestleri', launch: 'Epic Games Launcher üzerinden' },
      gog: { name: 'GOG', source: 'GOG kurulum programlarının ve GOG Galaxy’nin yazdığı kurulum kayıtları', launch: 'Doğrudan oyunun çalıştırılabilir dosyasından' },
      xbox: { name: 'Xbox (PC Game Pass)', source: 'XboxGames klasörlerindeki oyunlar', launch: 'Windows uygulaması olarak' },
      ubisoft: { name: 'Ubisoft Connect', source: 'Ubisoft Connect’in kurulum kayıtları', launch: 'Ubisoft Connect üzerinden' },
      ea: { name: 'EA app', source: 'Algılanır, ancak oyun listesi henüz okunamıyor', launch: 'Oyunları elle ekle' },
      other: { name: 'Diğer tüm oyunlar', source: 'Seçtiğin klasörler ya da tek bir .exe', launch: 'Doğrudan ya da bir başlatıcı bağlantısıyla' },
    },
    footnote:
      'Deneysel tarayıcılar belgelenmemiş başlatıcı verilerine dayanır ve bazı oyunları kaçırabilir. Arclight’ın bulamadığı her şey Oyun Ekle ile eklenebilir — .exe dosyasını ya da bir başlatıcı bağlantısını seç.',
  },

  features: {
    title: 'Her gün kullanmak için tasarlandı',
    intro: 'İlk açtığın başlatıcı: aramada hızlı, bildikleri konusunda dürüst ve dilediğin gibi düzenlenebilir.',
    counts: 'Sayılır',
    countsBody: 'Oyunun kendi süreci, çalıştığı sürece',
    neverCounts: 'Asla sayılmaz',
    neverCountsBody: 'Başlatıcılar, çökme raporlayıcıları, hile önleyici yardımcılar, bilgisayarın uykuda geçirdiği süre',
    chartLabel: 'Takip edilen oynama süresinden örnek bir hafta',
    chartCaption: 'Örnek hafta. Gerçek toplamlar yalnızca Arclight’ın bilgisayarında doğruladığı oturumlardan gelir.',
    weekdays: ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'],
    items: {
      library: {
        title: 'Birleşik oyun kütüphanesi',
        body: 'Steam, Epic Games, GOG, Xbox, Ubisoft Connect ve bağımsız oyunlar yan yana; büyük kapaklar, anında arama ve birleştirilebilen filtrelerle.',
      },
      launch: {
        title: 'Hızlı oyun başlatma',
        body: 'Oyna, oyunu kendi başlatıcısıyla ya da doğrudan çalıştırılabilir dosyasından başlatır. Bir oyun taşındıysa ya da kaldırıldıysa Arclight ne olduğunu ve nasıl düzelteceğini söyler.',
      },
      tracking: {
        title: 'Oynama süresi takibi',
        body: 'Süre yalnızca oyunun kendi süreci çalışırken sayılır. Başlatıcılar, çökme raporlayıcıları ve uyku süresi asla sayılmaz; hiçbir şey tahmin edilmez ya da içe aktarılmaz.',
      },
      collections: {
        title: 'Kendi koleksiyonların',
        body: 'Sıradakiler, Bitirilenler, Hafta Sonu Oyunları — kendi koleksiyonlarını oluştur, sürükleyerek sırala ve bir oyunu istediğin kadar koleksiyona ekle.',
      },
      notes: {
        title: 'Kişisel oyun notları',
        body: 'Nerede kaldığın, bitirilecek görevler, işe yarayan ayarlar. Notlar sen yazarken kaydedilir ve bilgisayarında kalır.',
      },
      stats: {
        title: 'Oyun istatistikleri',
        body: 'Haftalık ve aylık toplamlar, 30 günlük etkinlik grafiği, en çok oynadığın oyunlar ve platforma göre kütüphanen.',
      },
      local: {
        title: 'Yerel öncelikli kütüphane',
        body: 'Hesap yok, bulut yok. Her şey, istediğin zaman JSON olarak dışa aktarabileceğin yerel bir veritabanında durur.',
      },
      custom: {
        title: 'Özelleştirilebilir arayüz',
        body: 'Izgara, sıkı ızgara ya da liste. Yedi vurgu rengi, OLED siyahı tema, kart boyutları ve azaltılmış hareket modu.',
      },
    },
  },

  screens: {
    title: 'Arclight iş başında',
    intro: 'Arclight 0.1’in gerçek ekran görüntüleri; uygulamanın hayali oyunlardan oluşan yerleşik örnek kütüphanesiyle, İngilizce arayüzde çekildi.',
    tabsLabel: 'Ekran görüntüleri',
    items: {
      library: { label: 'Kütüphane', alt: 'Izgara görünümünde Kütüphanem: filtre çipleri, sıralama ve görünüm seçenekleri.' },
      detail: { label: 'Oyun sayfası', alt: 'Oynama süresi, son oynanma, oturumlar, puan ve kişisel notlarla bir oyun sayfası.' },
      stats: { label: 'İstatistikler', alt: 'Toplamlar, 30 günlük etkinlik grafiği ve en çok oynanan oyunlarla İstatistikler.' },
      collections: { label: 'Koleksiyonlar', alt: 'İçlerindeki oyunların kapaklarından oluşan kutucuklar olarak Koleksiyonlar.' },
    },
  },

  privacy: {
    title: 'Kütüphanen bilgisayarında kalır',
    intro:
      'Arclight yerel önceliklidir. Arclight hesabı da Arclight sunucusu da yoktur: oyunların, notların, etiketlerin, koleksiyonların ve oynama süren, istediğin zaman JSON olarak dışa aktarabileceğin ya da silebileceğin yerel bir veritabanında durur.',
    columns: [
      {
        title: 'Yalnızca bu bilgisayarda saklananlar',
        items: ['Oyun listen, görsel önbelleği ve kişisel notların', 'Oyun oturumları ve istatistikler', 'Ayarlar ve uygulama günlükleri (14 gün saklanır)'],
      },
      {
        title: 'Arclight’ın internete çıktığı tek durumlar',
        items: [
          'Eksik kapak görsellerini Steam, Epic ve Microsoft Store görsel sunucularından indirmek (diğer oyunlar Steam mağazasında adlarıyla eşleştirilir). Bunu kapatabilirsin.',
          'GitHub’da yeni sürüm olup olmadığına bakmak — yalnızca Güncellemeleri kontrol et’e bastığında ya da başlangıç kontrolünü açtığında.',
        ],
      },
      {
        title: 'İzleyicinin baktığı şeyler',
        items: [
          'Oyunlarından birinin çalışıp çalışmadığını anlamak için çalışan programların adları ve dosya yolları.',
          'Tuş vuruşlarına, ekran içeriğine, pencere başlıklarına ya da diğer programlara asla bakmaz.',
        ],
      },
    ],
  },

  closing: {
    title: 'Tüm oyunlarını bir araya getir.',
    body: 'Arclight ücretsizdir, açık kaynaktır ve tamamen bilgisayarında çalışır.',
  },

  download: {
    title: 'Windows için Arclight’ı indir',
    lead: 'Ücretsiz ve açık kaynak. Hesap yok, oturum açma yok, reklam yok.',
    requirements: {
      title: 'Gereksinimler',
      items: [
        'Windows 10 (sürüm 1809 veya üstü) ya da Windows 11, 64 bit',
        'Yaklaşık 200 MB disk alanı; .NET çalışma zamanı dahildir',
        'Arclight’ı çalıştırmak için yönetici hakları gerekmez',
      ],
    },
    installing: {
      title: 'Kurulum',
      steps: [
        'Yükleyiciyi indirip çalıştır. Yalnızca senin kullanıcı hesabına kurulur.',
        'Arclight henüz kod imzalı değil, bu yüzden Windows SmartScreen bir uyarı gösterebilir. Ek bilgi’yi, ardından Yine de çalıştır’ı seç — bunu yalnızca resmî GitHub sürümünden indirilen dosyalar için yap.',
        'Arclight’ı aç ve Oyunları Tara’ya bas ya da bir oyunu kendin ekle.',
      ],
    },
    portable: {
      title: 'Taşınabilir sürüm',
      body: 'İstediğin yere çıkar — bir USB bellek de olur — ve Arclight.exe’yi çalıştır. Taşınabilir sürüm kütüphanesini kullanıcı profilinde değil, uygulamanın yanındaki Data klasöründe tutar.',
    },
    uninstalling: {
      title: 'Kaldırma',
      body: 'Arclight’ı Windows Ayarlar › Uygulamalar bölümünden kaldır. Oyunlarına asla dokunulmaz. Kütüphane veritabanın %LOCALAPPDATA%\\Arclight klasöründe kalır, böylece yeniden kurduğunda kaldığın yerden devam edersin; onu da silmek için bu klasörü sil.',
    },
  },

  release: {
    loading: 'En son sürüm için GitHub kontrol ediliyor…',
    released: (date: string) => `${date} tarihinde Windows 10 ve 11 (64 bit) için yayımlandı.`,
    installer: (size: string) => `Yükleyiciyi indir (${size})`,
    portable: (size: string) => `Taşınabilir .zip (${size})`,
    releasePage: 'Sürüm sayfası ve sağlama toplamları',
    noneTitle: 'Henüz bir sürüm yayımlanmadı',
    noneBody: 'İlk yükleyici GitHub’da yayımlanır yayımlanmaz burada görünecek. O zamana kadar Arclight’ı birkaç dakikada kaynak koddan derleyebilirsin.',
    errorTitle: 'GitHub’a ulaşılamadı',
    errorBody: 'En son sürüm şu anda yüklenemedi. Yayımlanan tüm sürümleri GitHub sürümler sayfasında bulabilirsin.',
    openReleases: 'GitHub sürümlerini aç',
    viewSource: 'Kaynak kodu görüntüle',
    buildLabel: 'Kaynak koddan derle',
    buildHint: 'Windows’ta .NET 10 SDK gerektirir.',
  },

  changelog: {
    introBefore: 'Her sürümde neler değişti. Sürümler ',
    introLink: 'GitHub Releases',
    introAfter: ' üzerinde yayımlanır.',
    entries: {
      '0.1.0': {
        summary: 'Arclight’ın ilk herkese açık sürümü: Windows için çalışan bir kütüphane, başlatıcı ve oynama süresi takipçisi.',
        groups: [
          {
            title: 'Kütüphane',
            items: [
              'Oyunları elle ekle ya da Steam, Epic Games, GOG, Xbox (deneysel) ve Ubisoft Connect (deneysel) tara.',
              'Arama, birleştirilebilen filtreler ve sekiz sıralama seçeneğiyle ızgara, sıkı ızgara ve liste görünümleri.',
              'Favoriler, sürükle-bırak ile sıralanan koleksiyonlar, etiketler, kişisel notlar ve 5 yıldızlı puanlama.',
              'Taramalar asla aynı oyunu iki kez eklemez; kaldırılan oyunlar sonraki taramalarda gizlenebilir.',
              'Steam, Epic, Xbox ve diğer oyunlar için resmî kapaklar ve arka planlar (çevrim içi aramalar kapatılabilir).',
            ],
          },
          {
            title: 'Oynama',
            items: [
              'Steam, Epic, Ubisoft ve Windows uygulama bağlantılarıyla ya da doğrudan bir çalıştırılabilir dosyadan başlatma.',
              'Bir oyun başlamadığında Oyunu bul ve Yönetici olarak çalıştır seçenekleriyle anlaşılır açıklamalar.',
              'Çökme kurtarma ve uyku algılamalı doğrulanmış oynama süresi takibi; ayrıca isteğe bağlı arka plan izleyicisi.',
            ],
          },
          {
            title: 'Uygulama',
            items: [
              'Ana Sayfa paneli, Son Oynananlar, İstatistikler ve Ayarlar.',
              'Windows dilini izleyen ve anında değiştirilebilen İngilizce ve Türkçe arayüz.',
              'Yükseltmelerden önce otomatik yedekleme ve JSON dışa aktarma ile yerel SQLite depolama.',
              'Windows 10 ve 11 (x64) için yükleyici ve taşınabilir sürümler.',
            ],
          },
        ],
      },
    },
  },
};
