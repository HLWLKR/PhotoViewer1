// ============================================================
// PhotoViewer.API — Контроллер фотографий
// Обрабатывает все HTTP-запросы к /api/photos
// Реализует CRUD: Create, Read, Update, Delete для фотографий
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoViewer.API.Models;
using PhotoViewer.Shared;

namespace PhotoViewer.API.Controllers
{
    // REST API контроллер для управления фотографиями
    // [ApiController] — включает автоматическую валидацию модели,
    // автоматические HTTP 400 при ошибках, привязку из тела запроса
    // [Route("api/[controller]")] — маршрут: api/photos
    // [controller] заменяется именем класса без "Controller"
    [ApiController]
    [Route("api/[controller]")]
    public class PhotosController : ControllerBase
    {
        // Контекст базы данных — внедряется через Dependency Injection
        // Используется для всех операций с PostgreSQL
        private readonly AppDbContext _db;

        // Окружение хоста — нужно для получения пути к wwwroot
        private readonly IWebHostEnvironment _env;

        // Конструктор с Dependency Injection
        // ASP.NET Core автоматически передаёт зависимости из DI-контейнера
        public PhotosController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // Возвращает путь к папке хранения фотографий на сервере
        // Если WebRootPath не задан (wwwroot не существует) — создаём вручную
        private string GetUploadsFolder() =>
            Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "photos");

        // Конвертирует модель БД (Photo) в DTO для клиента (PhotoDto)
        // URL формируется динамически из текущего хоста и имени файла
        // Request.Scheme — "http" или "https", Request.Host — "192.168.1.10:5000"
        private PhotoDto MapToDto(Photo p) => new PhotoDto
        {
            Id = p.Id,
            FileName = p.FileName,
            OriginalFileName = p.OriginalFileName,
            Description = p.Description,
            UploadedAt = p.UploadedAt,
            // URL по которому клиент скачивает файл напрямую через StaticFiles middleware
            Url = $"{Request.Scheme}://{Request.Host}/photos/{p.FileName}",
            FileSize = p.FileSize,
            Width = p.Width,
            Height = p.Height,
            IsFavorite = p.IsFavorite,
            Rotation = p.Rotation,
            // Извлекаем ID альбомов из навигационного свойства
            AlbumIds = p.PhotoAlbums.Select(pa => pa.AlbumId).ToList()
        };

        // ── GET /api/photos ────────────────────────────────────────────────

        // Возвращает список всех фотографий
        // Include(p => p.PhotoAlbums) — загружает связанные альбомы (Eager Loading),
        // иначе PhotoAlbums будет null (Lazy Loading отключен по умолчанию)
        [HttpGet]
        public async Task<ActionResult<List<PhotoDto>>> GetAll()
        {
            // ToListAsync() — асинхронный запрос SELECT * FROM Photos JOIN PhotoAlbums
            var photos = await _db.Photos
                .Include(p => p.PhotoAlbums)
                .ToListAsync();

            // Конвертируем каждую Photo в PhotoDto и возвращаем HTTP 200 OK
            return Ok(photos.Select(MapToDto).ToList());
        }

        // ── GET /api/photos/{id} ───────────────────────────────────────────

        // Возвращает одну фотографию по ID
        // Если не найдена — возвращает HTTP 404 Not Found
        [HttpGet("{id}")]
        public async Task<ActionResult<PhotoDto>> GetById(int id)
        {
            // FirstOrDefaultAsync с условием — поиск по первичному ключу + Include
            var p = await _db.Photos
                .Include(p => p.PhotoAlbums)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (p == null) return NotFound(); // HTTP 404
            return Ok(MapToDto(p));           // HTTP 200
        }

        // ── GET /api/photos/stats ──────────────────────────────────────────

        // Возвращает статистику хранилища: количество фото, общий размер, дату последней загрузки
        // Используется WPF-клиентом для отображения в строке статистики
        [HttpGet("stats")]
        public async Task<ActionResult<ServerStatsDto>> GetStats()
        {
            var photos = await _db.Photos.ToListAsync();
            return Ok(new ServerStatsDto
            {
                TotalPhotos = photos.Count,
                TotalSize = photos.Sum(p => p.FileSize),
                TotalSizeFormatted = FormatSize(photos.Sum(p => p.FileSize)),
                // Any() проверяет есть ли хоть одно фото, чтобы не вызывать Max() на пустом списке
                LastUpload = photos.Any() ? photos.Max(p => p.UploadedAt) : null
            });
        }

        // ── POST /api/photos/upload ────────────────────────────────────────

        // Загружает новую фотографию на сервер
        // Принимает multipart/form-data с файлом и опциональным описанием
        // Алгоритм:
        // 1. Проверка что файл не пустой
        // 2. Генерация UUID-имени для хранения на диске
        // 3. Сохранение файла в wwwroot/photos/
        // 4. Извлечение метаданных (размеры) через ImageSharp
        // 5. Сохранение записи в PostgreSQL
        [HttpPost("upload")]
        public async Task<ActionResult<PhotoDto>> Upload(IFormFile file, [FromForm] string description = "")
        {
            // Проверка входных данных
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран"); // HTTP 400

            var uploadsFolder = GetUploadsFolder();
            // Создаём папку если не существует (при первом запуске)
            Directory.CreateDirectory(uploadsFolder);

            // Генерируем уникальное имя файла: UUID + оригинальное расширение
            // Это исключает конфликты при загрузке файлов с одинаковыми именами
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Сохраняем файл на диск асинхронно
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Получаем размеры изображения через SixLabors.ImageSharp
            // try-catch — если файл не является изображением, размеры останутся 0
            int width = 0, height = 0;
            try
            {
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(filePath);
                width = image.Width;
                height = image.Height;
            }
            catch { /* Игнорируем ошибки определения размеров */ }

            // Создаём запись в базе данных
            var photo = new Photo
            {
                FileName = fileName,
                OriginalFileName = file.FileName, // Оригинальное имя для отображения
                Description = description,
                UploadedAt = DateTime.UtcNow,    // Всегда UTC
                FileSize = file.Length,
                Width = width,
                Height = height,
                IsFavorite = false,
                Rotation = 0
            };

            _db.Photos.Add(photo);
            await _db.SaveChangesAsync(); // Выполняет INSERT INTO Photos ...

            return Ok(MapToDto(photo)); // Возвращаем созданный объект с присвоенным ID
        }

        // ── PUT /api/photos/{id}/description ──────────────────────────────

        // Обновляет описание фотографии
        // [FromBody] — описание передаётся в теле запроса как JSON-строка
        [HttpPut("{id}/description")]
        public async Task<IActionResult> UpdateDescription(int id, [FromBody] string description)
        {
            var photo = await _db.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            photo.Description = description;
            await _db.SaveChangesAsync(); // UPDATE Photos SET Description = ... WHERE Id = id
            return NoContent(); // HTTP 204 — успех без тела ответа
        }

        // ── PUT /api/photos/{id}/rename ────────────────────────────────────

        // Переименовывает фотографию (меняет отображаемое имя, не имя файла на диске)
        [HttpPut("{id}/rename")]
        public async Task<IActionResult> Rename(int id, [FromBody] string newName)
        {
            var photo = await _db.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            photo.OriginalFileName = newName;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ── PUT /api/photos/{id}/favorite ─────────────────────────────────

        // Переключает статус избранного (toggle)
        // Если было true — становится false, и наоборот
        // Возвращает новое значение IsFavorite в JSON
        [HttpPut("{id}/favorite")]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var photo = await _db.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            photo.IsFavorite = !photo.IsFavorite; // Toggle
            await _db.SaveChangesAsync();
            return Ok(new { isFavorite = photo.IsFavorite }); // Возвращаем новое значение
        }

        // ── PUT /api/photos/{id}/rotate ────────────────────────────────────

        // Поворачивает фотографию на указанное количество градусов
        // degrees: +90 (вправо) или -90 (влево)
        // Формула (Rotation + degrees + 360) % 360 гарантирует диапазон 0-359:
        // 0 + (-90) + 360 = 270 → 270 % 360 = 270 ✓
        [HttpPut("{id}/rotate")]
        public async Task<IActionResult> Rotate(int id, [FromBody] int degrees)
        {
            var photo = await _db.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            photo.Rotation = (photo.Rotation + degrees + 360) % 360;
            await _db.SaveChangesAsync();
            return Ok(new { rotation = photo.Rotation });
        }

        // ── DELETE /api/photos/{id} ────────────────────────────────────────

        // Удаляет фотографию: сначала файл с диска, затем запись из БД
        // Порядок важен — если сначала удалить из БД, потеряем путь к файлу
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var photo = await _db.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            // Удаляем физический файл с диска
            var filePath = Path.Combine(GetUploadsFolder(), photo.FileName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            // Удаляем запись из БД (CASCADE удалит записи в PhotoAlbums автоматически)
            _db.Photos.Remove(photo);
            await _db.SaveChangesAsync(); // DELETE FROM Photos WHERE Id = id
            return NoContent(); // HTTP 204
        }

        // ── Вспомогательный метод ──────────────────────────────────────────

        // Форматирует размер файла в читаемый вид: Б, КБ или МБ
        // Используется в GetStats() для TotalSizeFormatted
        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024):F1} МБ";
        }
    }
}