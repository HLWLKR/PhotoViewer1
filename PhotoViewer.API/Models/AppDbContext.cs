// ============================================================
// PhotoViewer.API — Контекст базы данных Entity Framework Core
// AppDbContext — это "мост" между C# кодом и PostgreSQL
// Через него выполняются все операции с базой данных
// Регистрируется в Program.cs через builder.Services.AddDbContext()
// ============================================================

using Microsoft.EntityFrameworkCore;

namespace PhotoViewer.API.Models
{
    // Контекст базы данных Entity Framework Core
    // Наследует DbContext — базовый класс EF Core
    // Отвечает за:
    // 1. Подключение к PostgreSQL через строку из appsettings.json
    // 2. Маппинг C#-классов на таблицы базы данных
    // 3. Выполнение LINQ-запросов (транслирует их в SQL)
    // 4. Автосоздание таблиц через EnsureCreated() в Program.cs
    public class AppDbContext : DbContext
    {
        // Конструктор получает настройки через Dependency Injection
        // ASP.NET Core автоматически передаёт DbContextOptions,
        // настроенные в Program.cs (строка подключения, провайдер PostgreSQL)
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Представляет таблицу "Photos" в базе данных
        // Позволяет выполнять запросы: _db.Photos.ToListAsync(),
        // _db.Photos.FindAsync(id), _db.Photos.Add(photo) и т.д
        public DbSet<Photo> Photos { get; set; }

        // Представляет таблицу "Albums" в базе данных
        public DbSet<Album> Albums { get; set; }

        // Представляет связующую таблицу "PhotoAlbums"
        // Используется для операций с отношением фото-альбом
        public DbSet<PhotoAlbum> PhotoAlbums { get; set; }

        // Настройка модели базы данных (Fluent API)
        // Вызывается EF Core один раз при инициализации контекста
        // Здесь настраивается составной первичный ключ таблицы PhotoAlbums
        // и связи между таблицами (Foreign Keys)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Составной первичный ключ для связующей таблицы:
            // пара (PhotoId + AlbumId) должна быть уникальной —
            // одно фото нельзя добавить в один альбом дважды
            modelBuilder.Entity<PhotoAlbum>()
                .HasKey(pa => new { pa.PhotoId, pa.AlbumId });

            // Настройка связи: PhotoAlbum → Photo (многие к одному)
            // Одна запись PhotoAlbum относится к одной фотографии (HasOne),
            // одна фотография может иметь много связей (WithMany)
            modelBuilder.Entity<PhotoAlbum>()
                .HasOne(pa => pa.Photo)
                .WithMany(p => p.PhotoAlbums)
                .HasForeignKey(pa => pa.PhotoId);

            // Настройка связи: PhotoAlbum → Album (многие к одному)
            modelBuilder.Entity<PhotoAlbum>()
                .HasOne(pa => pa.Album)
                .WithMany(a => a.PhotoAlbums)
                .HasForeignKey(pa => pa.AlbumId);
        }
    }
}