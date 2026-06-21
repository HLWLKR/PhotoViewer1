// ============================================================
// PhotoViewer.WPF — Конвертер URL → BitmapImage
// Используется в DataTemplate галереи для загрузки миниатюр
// Реализует интерфейс IValueConverter для привязки данных WPF
// ============================================================

using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PhotoViewer.WPF.Converters
{
    // WPF-конвертер для привязки данных (Data Binding)
    // Конвертирует URL строку в объект BitmapImage для отображения в Image-контроле
    // Используется в MainWindow.xaml в шаблоне галереи:
    // Source="{Binding Url, Converter={StaticResource UrlToImageConverter}}"
    // IValueConverter — стандартный интерфейс WPF для конвертеров в привязках
    // Метод Convert вызывается автоматически при обновлении данных
    public class UrlToImageConverter : IValueConverter
    {
        // Статический HttpClient — разделяется между всеми экземплярами конвертера
        // Создание нового HttpClient для каждого запроса — плохая практика
        // (исчерпание сокетов). Одного экземпляра достаточно
        private static readonly HttpClient _client = new HttpClient();

        // Конвертирует URL (строку) в BitmapImage для отображения в WPF
        // Вызывается автоматически при привязке данных для каждого элемента галереи
        // BitmapImage для отображения, или null при ошибке
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем что значение является непустой строкой
            if (value is not string url || string.IsNullOrEmpty(url))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); // Начало инициализации объекта BitmapImage

                // UriSource — URI источника изображения. BitmapImage загружает его асинхронно
                bitmap.UriSource = new Uri(url);

                // DecodePixelWidth — декодировать изображение с шириной 200px
                // Это ключевая оптимизация: вместо загрузки полного изображения
                // (например 3000x2000px) декодируется только миниатюра 200px
                // Экономит память и ускоряет отображение галереи
                bitmap.DecodePixelWidth = 200;

                // CacheOption.OnLoad — кэшировать изображение полностью при загрузке
                // Без этого BitmapImage держит поток открытым, что может вызвать ошибки
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                bitmap.EndInit(); // Завершение инициализации — запускает загрузку
                return bitmap;
            }
            catch
            {
                // При любой ошибке (сеть недоступна, неверный URL) возвращаем null
                // WPF покажет пустое место вместо изображения
                return null;
            }
        }

        // Обратная конвертация (BitmapImage → URL)
        // Не реализована, так как нам нужна только односторонняя привязка
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}