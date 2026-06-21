// ============================================================
// PhotoViewer.Shared — Общие модели (DTO)
// Этот проект подключён и к серверу (API), и к клиенту (WPF),
// чтобы не дублировать классы в двух местах
// DTO (Data Transfer Object) — объекты для передачи данных
// между слоями приложения через HTTP в формате JSON
// ============================================================

namespace PhotoViewer.Shared
{
    // DTO для передачи данных о фотографии от сервера клиенту
    // Не совпадает с моделью базы данных (Photo.cs) —
    // здесь добавлено поле Url, которого нет в БД
    public class PhotoDto
    {
        // Уникальный идентификатор фотографии в базе данных
        public int Id { get; set; }

        // UUID-имя файла на сервере (например: a3f1c2d4-...-jpg)
        // Генерируется автоматически при загрузке для гарантии уникальности
        public string FileName { get; set; } = string.Empty;

        // Оригинальное имя файла, заданное пользователем
        // Отображается в интерфейсе вместо UUID
        public string OriginalFileName { get; set; } = string.Empty;

        // Описание фотографии, введённое пользователем
        public string Description { get; set; } = string.Empty;

        // Дата и время загрузки фотографии на сервер (UTC)
        public DateTime UploadedAt { get; set; }

        // Полный URL для скачивания файла фотографии
        // Формируется на сервере динамически: http://[host]/photos/[FileName]
        // Не хранится в базе данных
        public string Url { get; set; } = string.Empty;

        // Размер файла в байтах
        public long FileSize { get; set; }

        // Ширина изображения в пикселях (определяется через ImageSharp)
        public int Width { get; set; }

        // Высота изображения в пикселях
        public int Height { get; set; }

        // Признак избранного. True — фото помечено звёздочкой
        // Сохраняется в базе данных
        public bool IsFavorite { get; set; }

        // Угол поворота изображения в градусах (0, 90, 180, 270)
        // Применяется через RotateTransform в WPF при отображении
        public int Rotation { get; set; }

        // Список ID альбомов, в которые добавлено это фото
        // Реализует связь многие-ко-многим через таблицу PhotoAlbums
        public List<int> AlbumIds { get; set; } = new();
    }

    // DTO для статистики сервера
    // Возвращается эндпоинтом GET /api/photos/stats
    public class ServerStatsDto
    {
        // Общее количество фотографий на сервере
        public int TotalPhotos { get; set; }

        // Суммарный размер всех файлов в байтах
        public long TotalSize { get; set; }

        // Отформатированный размер хранилища (например: "15.3 МБ")
        // Форматируется на сервере методом FormatSize()
        public string TotalSizeFormatted { get; set; } = string.Empty;

        // Дата и время последней загрузки фото. Null если фото нет
        public DateTime? LastUpload { get; set; }
    }

    // DTO для передачи данных об альбоме
    // Возвращается эндпоинтами AlbumsController
    public class AlbumDto
    {
        // Уникальный идентификатор альбома
        public int Id { get; set; }

        // Название альбома, заданное пользователем
        public string Name { get; set; } = string.Empty;

        // Дата и время создания альбома (UTC)
        public DateTime CreatedAt { get; set; }

        // Количество фотографий в альбоме
        // Вычисляется при запросе через PhotoAlbums.Count
        public int PhotoCount { get; set; }
    }
}