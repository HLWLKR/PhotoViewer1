// ============================================================
// PhotoViewer.API — Модели альбомов
// Два класса: Album (таблица альбомов) и PhotoAlbum
// (связующая таблица для отношения многие-ко-многим)
// ============================================================

namespace PhotoViewer.API.Models
{
    // Модель таблицы "Albums" в базе данных PostgreSQL
    // Альбом — это именованная группа фотографий
    // Одна фотография может быть в нескольких альбомах,
    // один альбом может содержать много фотографий — связь N:M
    public class Album
    {
        // Первичный ключ, автоинкремент (SERIAL в PostgreSQL)
        public int Id { get; set; }

        // Название альбома, введённое пользователем
        // Не уникально — можно создать два альбома с одинаковым именем
        public string Name { get; set; } = string.Empty;

        // Дата и время создания альбома в UTC
        // В SQL добавлено DEFAULT now() для обратной совместимости
        public DateTime CreatedAt { get; set; }

        // Навигационное свойство EF Core
        // Содержит записи из таблицы PhotoAlbums, связанные с этим альбомом
        // Используется для подсчёта PhotoCount в AlbumDto
        public List<PhotoAlbum> PhotoAlbums { get; set; } = new();
    }

    // Связующая таблица "PhotoAlbums" — реализует отношение многие-ко-многим
    // между таблицами Photos и Albums
    // Пример: фото ID=5 добавлено в альбомы ID=1 и ID=3
    // В таблице будет две записи: (5,1) и (5,3)
    // Составной первичный ключ (PhotoId + AlbumId) настроен в AppDbContext.OnModelCreating
    public class PhotoAlbum
    {
        // Внешний ключ на таблицу Photos
        // ON DELETE CASCADE — при удалении фото все связи удаляются автоматически
        public int PhotoId { get; set; }

        // Навигационное свойство — объект фотографии
        public Photo Photo { get; set; } = null!;

        // Внешний ключ на таблицу Albums
        // ON DELETE CASCADE — при удалении альбома все связи удаляются
        public int AlbumId { get; set; }

        // Навигационное свойство — объект альбома
        public Album Album { get; set; } = null!;
    }
}