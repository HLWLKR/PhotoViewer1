// ============================================================
// PhotoViewer.API — Контроллер альбомов
// Обрабатывает HTTP-запросы к /api/albums
// Управляет альбомами и связями фото-альбом (N:M)
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoViewer.API.Models;
using PhotoViewer.Shared;

namespace PhotoViewer.API.Controllers
{
    // REST API контроллер для управления альбомами
    // Маршрут: /api/albums
    [ApiController]
    [Route("api/[controller]")]
    public class AlbumsController : ControllerBase
    {
        // Контекст базы данных — внедряется через Dependency Injection
        private readonly AppDbContext _db;

        public AlbumsController(AppDbContext db)
        {
            _db = db;
        }

        // ── GET /api/albums ────────────────────────────────────────────────

        // Возвращает список всех альбомов с количеством фотографий в каждом
        // Include загружает связанные PhotoAlbums для подсчёта PhotoCount
        [HttpGet]
        public async Task<ActionResult<List<AlbumDto>>> GetAll()
        {
            var albums = await _db.Albums
                .Include(a => a.PhotoAlbums) // Eager loading связей
                .ToListAsync();

            // Конвертируем в DTO: PhotoCount = количество записей в связующей таблице
            return Ok(albums.Select(a => new AlbumDto
            {
                Id = a.Id,
                Name = a.Name,
                CreatedAt = a.CreatedAt,
                PhotoCount = a.PhotoAlbums.Count // Количество фото в альбоме
            }).ToList());
        }

        // ── POST /api/albums ───────────────────────────────────────────────

        // Создаёт новый альбом
        // [FromBody] string name — название передаётся в теле запроса как JSON-строка
        [HttpPost]
        public async Task<ActionResult<AlbumDto>> Create([FromBody] string name)
        {
            // Валидация входных данных
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Название альбома не может быть пустым");

            var album = new Album
            {
                Name = name,
                CreatedAt = DateTime.UtcNow // Время создания в UTC
            };

            _db.Albums.Add(album);
            await _db.SaveChangesAsync(); // INSERT INTO Albums (Name, CreatedAt) VALUES (...)

            // Возвращаем созданный альбом с присвоенным ID и нулём фотографий
            return Ok(new AlbumDto
            {
                Id = album.Id,
                Name = album.Name,
                CreatedAt = album.CreatedAt,
                PhotoCount = 0
            });
        }

        // ── DELETE /api/albums/{id} ────────────────────────────────────────

        // Удаляет альбом по ID
        // Записи в PhotoAlbums удаляются автоматически через CASCADE
        // (настроено в SQL: ON DELETE CASCADE)
        // Сами фотографии НЕ удаляются — только связи
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var album = await _db.Albums.FindAsync(id);
            if (album == null) return NotFound();

            _db.Albums.Remove(album);
            await _db.SaveChangesAsync(); // DELETE FROM Albums WHERE Id = id
            return NoContent(); // HTTP 204 — успех без тела ответа
        }

        // ── POST /api/albums/{albumId}/photos/{photoId} ───────────────────

        // Добавляет фотографию в альбом (создаёт запись в PhotoAlbums)
        // Проверяет существование и фото, и альбома
        // Если связь уже существует — просто возвращает OK (идемпотентность)
        [HttpPost("{albumId}/photos/{photoId}")]
        public async Task<IActionResult> AddPhoto(int albumId, int photoId)
        {
            // Проверяем существование обоих объектов
            var album = await _db.Albums.FindAsync(albumId);
            var photo = await _db.Photos.FindAsync(photoId);
            if (album == null || photo == null) return NotFound();

            // Проверяем, не добавлено ли фото в альбом уже
            // AnyAsync — SELECT EXISTS (SELECT 1 FROM PhotoAlbums WHERE ...)
            var exists = await _db.PhotoAlbums
                .AnyAsync(pa => pa.AlbumId == albumId && pa.PhotoId == photoId);
            if (exists) return Ok(); // Уже добавлено — ничего не делаем

            // Создаём запись в связующей таблице
            _db.PhotoAlbums.Add(new PhotoAlbum { AlbumId = albumId, PhotoId = photoId });
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ── DELETE /api/albums/{albumId}/photos/{photoId} ─────────────────

        // Убирает фотографию из альбома (удаляет запись из PhotoAlbums)
        // Сама фотография остаётся — удаляется только связь
        [HttpDelete("{albumId}/photos/{photoId}")]
        public async Task<IActionResult> RemovePhoto(int albumId, int photoId)
        {
            // Ищем конкретную связь в таблице PhotoAlbums
            var pa = await _db.PhotoAlbums
                .FirstOrDefaultAsync(pa => pa.AlbumId == albumId && pa.PhotoId == photoId);
            if (pa == null) return NotFound(); // Связь не найдена

            _db.PhotoAlbums.Remove(pa);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ── GET /api/albums/{albumId}/photos ──────────────────────────────

        // Возвращает список фотографий конкретного альбома
        // Запрос:
        // 1. Фильтрует PhotoAlbums по albumId
        // 2. Include загружает связанные Photo
        // 3. Select извлекает только объекты Photo
        [HttpGet("{albumId}/photos")]
        public async Task<ActionResult<List<PhotoDto>>> GetPhotos(int albumId)
        {
            var photos = await _db.PhotoAlbums
                .Where(pa => pa.AlbumId == albumId)  // WHERE AlbumId = albumId
                .Include(pa => pa.Photo)               // JOIN Photos
                .Select(pa => pa.Photo)                // Берём только Photo
                .ToListAsync();

            // Формируем URL для каждого фото через Request.Scheme и Request.Host
            return Ok(photos.Select(p => new PhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                OriginalFileName = p.OriginalFileName,
                Description = p.Description,
                UploadedAt = p.UploadedAt,
                Url = $"{Request.Scheme}://{Request.Host}/photos/{p.FileName}",
                FileSize = p.FileSize,
                Width = p.Width,
                Height = p.Height,
                IsFavorite = p.IsFavorite,
                Rotation = p.Rotation
            }).ToList());
        }
    }
}