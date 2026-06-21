// ============================================================
// PhotoViewer.WPF — Сервис для работы с API
// ApiService инкапсулирует все HTTP-запросы к серверу
// WPF-клиент не работает с HttpClient напрямую — только через этот класс
// Паттерн: Service Layer (Сервисный слой)
// ============================================================

using Newtonsoft.Json;
using PhotoViewer.Shared;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PhotoViewer.WPF.Services
{
    // Сервис для взаимодействия с PhotoViewer REST API
    // Все методы асинхронные (async/await) — не блокируют UI-поток WPF
    // Использует HttpClient для отправки HTTP-запросов
    // и Newtonsoft.Json для десериализации JSON-ответов в C#-объекты
    public class ApiService
    {
        // HttpClient — основной инструмент для HTTP-запросов
        // Создаётся один раз и переиспользуется (best practice)
        private readonly HttpClient _client;

        // Инициализирует сервис с базовым URL сервера
        public ApiService(string baseUrl)
        {
            _client = new HttpClient();
            // BaseAddress — все относительные URL будут дополняться этим адресом
            // Например: GetAsync("api/photos") → GET http://192.168.1.10:5000/api/photos
            _client.BaseAddress = new Uri(baseUrl);
        }

        // ── Методы для работы с фотографиями ──────────────────────────────

        // Получает список всех фотографий с сервера
        // GET /api/photos → JSON → List of PhotoDto
        public async Task<List<PhotoDto>> GetAllPhotosAsync()
        {
            // GetStringAsync — выполняет GET и возвращает тело ответа как строку
            var response = await _client.GetStringAsync("api/photos");
            // DeserializeObject конвертирует JSON-строку в List<PhotoDto>
            return JsonConvert.DeserializeObject<List<PhotoDto>>(response) ?? new List<PhotoDto>();
        }

        // Загружает фотографию на сервер
        // POST /api/photos/upload с типом multipart/form-data
        // multipart/form-data — стандартный способ передачи файлов через HTTP
        // Содержит несколько частей: файл (бинарные данные) + текстовые поля
        public async Task<PhotoDto?> UploadPhotoAsync(string filePath, string description)
        {
            // MultipartFormDataContent — контейнер для multipart/form-data запроса
            using var content = new MultipartFormDataContent();

            // Читаем файл в массив байт
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);

            // Указываем тип содержимого (MIME-тип)
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

            // Добавляем файл: "file" — имя поля (совпадает с параметром IFormFile file в контроллере)
            content.Add(fileContent, "file", Path.GetFileName(filePath));
            // Добавляем текстовое поле описания
            content.Add(new StringContent(description), "description");

            var response = await _client.PostAsync("api/photos/upload", content);
            if (!response.IsSuccessStatusCode) return null; // Ошибка сервера

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PhotoDto>(json);
        }

        // Удаляет фотографию по ID
        // DELETE /api/photos/{id}
        public async Task<bool> DeletePhotoAsync(int id)
        {
            var response = await _client.DeleteAsync($"api/photos/{id}");
            return response.IsSuccessStatusCode;
        }

        // Скачивает файл фотографии по URL
        // URL берётся из PhotoDto.Url — полный адрес файла на сервере
        // Возвращает байты файла для отображения или сохранения
        public async Task<byte[]> DownloadPhotoAsync(string url)
        {
            // GetByteArrayAsync — загружает ответ как массив байт
            return await _client.GetByteArrayAsync(url);
        }

        // Обновляет описание фотографии
        // PUT /api/photos/{id}/description
        // Передаёт строку как JSON в теле запроса
        public async Task<bool> UpdateDescriptionAsync(int id, string description)
        {
            // StringContent с application/json — отправляем строку как JSON
            var content = new StringContent(
                JsonConvert.SerializeObject(description), // "описание" → JSON: "\"описание\""
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PutAsync($"api/photos/{id}/description", content);
            return response.IsSuccessStatusCode;
        }

        // Переименовывает фотографию (меняет OriginalFileName)
        // PUT /api/photos/{id}/rename
        public async Task<bool> RenamePhotoAsync(int id, string newName)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(newName),
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PutAsync($"api/photos/{id}/rename", content);
            return response.IsSuccessStatusCode;
        }

        // Переключает статус "Избранное" для фотографии
        // PUT /api/photos/{id}/favorite — без тела запроса (null)
        // Сервер сам переключает значение (toggle)
        public async Task<bool> ToggleFavoriteAsync(int id)
        {
            var response = await _client.PutAsync($"api/photos/{id}/favorite", null);
            return response.IsSuccessStatusCode;
        }

        // Поворачивает фотографию на указанное количество градусов
        // PUT /api/photos/{id}/rotate
        public async Task<bool> RotateAsync(int id, int degrees)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(degrees), // Число в JSON: 90 или -90
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PutAsync($"api/photos/{id}/rotate", content);
            return response.IsSuccessStatusCode;
        }

        // Получает статистику сервера: количество фото, размер хранилища
        // GET /api/photos/stats
        public async Task<ServerStatsDto?> GetStatsAsync()
        {
            try
            {
                var response = await _client.GetStringAsync("api/photos/stats");
                return JsonConvert.DeserializeObject<ServerStatsDto>(response);
            }
            catch { return null; } // Возвращаем null при любой ошибке сети
        }

        // ── Методы для работы с альбомами ─────────────────────────────────

        // Получает список всех альбомов
        // GET /api/albums
        public async Task<List<AlbumDto>> GetAlbumsAsync()
        {
            var response = await _client.GetStringAsync("api/albums");
            return JsonConvert.DeserializeObject<List<AlbumDto>>(response) ?? new List<AlbumDto>();
        }

        // Создаёт новый альбом с указанным именем
        // POST /api/albums с именем альбома в теле запроса
        public async Task<AlbumDto?> CreateAlbumAsync(string name)
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(name),
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await _client.PostAsync("api/albums", content);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<AlbumDto>(json);
        }

        // Удаляет альбом по ID
        // DELETE /api/albums/{id}
        // Фотографии не удаляются — только сам альбом и его связи
        public async Task<bool> DeleteAlbumAsync(int id)
        {
            var response = await _client.DeleteAsync($"api/albums/{id}");
            return response.IsSuccessStatusCode;
        }

        // Добавляет фотографию в альбом
        // POST /api/albums/{albumId}/photos/{photoId}
        // Создаёт запись в связующей таблице PhotoAlbums
        public async Task<bool> AddPhotoToAlbumAsync(int albumId, int photoId)
        {
            var response = await _client.PostAsync($"api/albums/{albumId}/photos/{photoId}", null);
            return response.IsSuccessStatusCode;
        }

        // Убирает фотографию из альбома (не удаляет саму фотографию)
        // DELETE /api/albums/{albumId}/photos/{photoId}
        public async Task<bool> RemovePhotoFromAlbumAsync(int albumId, int photoId)
        {
            var response = await _client.DeleteAsync($"api/albums/{albumId}/photos/{photoId}");
            return response.IsSuccessStatusCode;
        }

        // Получает все фотографии конкретного альбома
        // GET /api/albums/{albumId}/photos
        public async Task<List<PhotoDto>> GetAlbumPhotosAsync(int albumId)
        {
            var response = await _client.GetStringAsync($"api/albums/{albumId}/photos");
            return JsonConvert.DeserializeObject<List<PhotoDto>>(response) ?? new List<PhotoDto>();
        }

        // Возвращает базовый URL сервера (для отображения в интерфейсе)
        public string GetBaseUrl() => _client.BaseAddress?.ToString() ?? "";
    }
}