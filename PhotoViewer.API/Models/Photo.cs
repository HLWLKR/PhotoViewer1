// ============================================================
// PhotoViewer.API — Модель базы данных
// Этот класс описывает таблицу "Photos" в PostgreSQL
// Entity Framework Core использует его для создания таблицы
// и выполнения запросов к базе данных
// ============================================================

namespace PhotoViewer.API.Models
{
    // Модель таблицы "Photos" в базе данных PostgreSQL
    // EF Core автоматически создаёт таблицу по этому классу
    // при вызове db.Database.EnsureCreated() в Program.cs
    public class Photo
    {
        // Первичный ключ таблицы
        // EF Core автоматически настраивает его как SERIAL (автоинкремент)
        public int Id { get; set; }

        // UUID-имя файла на диске (например: "a3f1c2d4-5678-...-ef12.jpg")
        // Генерируется через Guid.NewGuid() при загрузке
        // Гарантирует уникальность даже для файлов с одинаковыми именами
        // Файл хранится в папке: wwwroot/photos/[FileName]
        public string FileName { get; set; } = string.Empty;

        // Оригинальное имя файла, заданное пользователем или при загрузке
        // Может быть изменено через PUT /api/photos/{id}/rename
        // Именно это имя отображается в интерфейсе WPF-клиента
        public string OriginalFileName { get; set; } = string.Empty;

        // Текстовое описание фотографии
        // Задаётся при загрузке или через PUT /api/photos/{id}/description
        public string Description { get; set; } = string.Empty;

        // Дата и время загрузки на сервер в UTC
        // Устанавливается автоматически: DateTime.UtcNow при загрузке
        public DateTime UploadedAt { get; set; }

        // Размер файла в байтах
        // Берётся из IFormFile.Length при загрузке
        // Используется для статистики и отображения в клиенте
        public long FileSize { get; set; }

        // Ширина изображения в пикселях
        // Определяется через SixLabors.ImageSharp после сохранения файла
        public int Width { get; set; }

        // Высота изображения в пикселях
        public int Height { get; set; }

        // Признак избранного. true — фото помечено звёздочкой
        // Переключается через PUT /api/photos/{id}/favorite
        // По умолчанию false при загрузке
        public bool IsFavorite { get; set; }

        // Угол поворота в градусах: 0, 90, 180 или 270
        // Изменяется через PUT /api/photos/{id}/rotate
        // Формула: (Rotation + degrees + 360) % 360 — гарантирует диапазон 0-359
        public int Rotation { get; set; }

        // Навигационное свойство EF Core для связи многие-ко-многим с альбомами
        // EF Core использует его для JOIN-запросов через таблицу PhotoAlbums
        public List<PhotoAlbum> PhotoAlbums { get; set; } = new();
    }
}