// ============================================================
// PhotoViewer.API — Точка входа и конфигурация приложения
// Program.cs — главный файл сервера
// Здесь регистрируются все сервисы и настраивается HTTP-конвейер
// Использует минимальный API (.NET 6+) без явного класса Startup
// ============================================================

using Microsoft.EntityFrameworkCore;
using PhotoViewer.API.Models;

// Создание строителя приложения
// args — аргументы командной строки (не используются, но нужны по шаблону)
var builder = WebApplication.CreateBuilder(args);

// ── Регистрация сервисов (Dependency Injection контейнер) ──────────────────

// Регистрация контроллеров API
// Позволяет ASP.NET Core находить классы PhotosController, AlbumsController
// и автоматически маршрутизировать к ним HTTP-запросы
builder.Services.AddControllers();

// Регистрация сервисов для Swagger/OpenAPI документации
// AddEndpointsApiExplorer — обнаруживает эндпоинты для Swagger
// AddSwaggerGen — генерирует документацию на основе аннотаций
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрация контекста базы данных с PostgreSQL провайдером
// Строка подключения берётся из appsettings.json → ConnectionStrings → DefaultConnection
// Npgsql — официальный .NET провайдер для PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Настройка Kestrel (встроенный веб-сервер ASP.NET Core) ────────────────

builder.WebHost.ConfigureKestrel(options =>
{
    // ListenAnyIP(5000) — слушать на ВСЕХ сетевых интерфейсах на порту 5000 (HTTP)
    // Это ключевая настройка для сетевого доступа!
    // Без неё сервер слушал бы только localhost — другие ПК не смогли бы подключиться
    options.ListenAnyIP(5000);

    // ListenAnyIP(5001) — порт для HTTPS (защищённое соединение)
    options.ListenAnyIP(5001, listenOptions => listenOptions.UseHttps());
});

// Сборка приложения из зарегистрированных сервисов
var app = builder.Build();

// ── HTTP конвейер (Middleware Pipeline) ────────────────────────────────────
// Middleware обрабатывают запрос последовательно в порядке регистрации

// Swagger доступен всегда — сервер работает только в локальной сети,
// поэтому нет смысла прятать документацию API только за Development
// Открыть: http://localhost:5000/swagger
app.UseSwagger();       // Генерирует JSON-схему API
app.UseSwaggerUI();     // Отображает красивый веб-интерфейс

// Middleware для отдачи статических файлов из папки wwwroot/
// Именно через него клиенты скачивают фотографии по URL вида:
// http://[ip]:5000/photos/[uuid].jpg
app.UseStaticFiles();

// Middleware авторизации (не настроена, но нужна по порядку pipeline)
app.UseAuthorization();

// Маршрутизация запросов к контроллерам
// Атрибуты [Route] и [HttpGet/Post/Put/Delete] на контроллерах
// определяют какой метод вызывается для какого URL
app.MapControllers();

// ── Инициализация базы данных ──────────────────────────────────────────────

// Создаём временную область жизни (scope) для получения сервисов
// DbContext нельзя получить напрямую — только через scope
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // EnsureCreated() — создаёт базу данных и все таблицы,
    // если они ещё не существуют
    db.Database.EnsureCreated();
}

// Запуск HTTP-сервера. Приложение начинает принимать запросы
app.Run();
