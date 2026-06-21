// ============================================================
// PhotoViewer.WPF — Полноэкранный просмотр фотографий
// Открывается по двойному клику на фото в главном окне
// Поддерживает листание стрелками и клавиатурой
// ============================================================

using PhotoViewer.Shared;
using PhotoViewer.WPF.Services;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PhotoViewer.WPF
{
    // Окно полноэкранного просмотра фотографий
    // Открывается через ShowDialog() из MainWindow при двойном клике на фото
    // Особенности:
    // - WindowState="Maximized" + WindowStyle="None" = истинный полный экран
    // - Навигация стрелками влево/вправо и клавишами ← →
    // - Закрытие по Escape или кнопке ✕
    public partial class FullScreenWindow : Window
    {
        // Сервис для загрузки фотографий с сервера
        private readonly ApiService _apiService;

        // Список всех фотографий текущей галереи (переданный из MainWindow)
        private readonly List<PhotoDto> _photos;

        // Индекс текущей отображаемой фотографии в списке _photos
        private int _currentIndex;

        // Конструктор полноэкранного окна
        // Вызывается из MainWindow при двойном клике на изображение
        public FullScreenWindow(ApiService apiService, List<PhotoDto> photos, int startIndex)
        {
            InitializeComponent();
            _apiService = apiService;
            _photos = photos;
            _currentIndex = startIndex;

            // Загружаем первое фото сразу при открытии окна
            // _ = ... — оператор discard, игнорируем Task (не ждём завершения в конструкторе)
            // Это безопасно, так как LoadCurrentPhotoAsync обновит UI через dispatcher
            _ = LoadCurrentPhotoAsync();
        }

        // Загружает и отображает фотографию с текущим индексом
        // Скачивает байты с сервера, создаёт BitmapImage и отображает в FullImage
        private async Task LoadCurrentPhotoAsync()
        {
            // Защита от выхода за границы списка
            if (_currentIndex < 0 || _currentIndex >= _photos.Count) return;

            var photo = _photos[_currentIndex];
            try
            {
                // Скачиваем файл фотографии с сервера как массив байт
                var bytes = await _apiService.DownloadPhotoAsync(photo.Url);

                // Создаём BitmapImage из байт через MemoryStream
                // CacheOption.OnLoad — загружаем полностью в память, закрываем поток сразу
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();

                // Обновляем UI-элементы
                FullImage.Source = bitmap;
                FileNameText.Text = photo.FileName;
                DescriptionText.Text = string.IsNullOrEmpty(photo.Description)
                    ? "Без описания"
                    : photo.Description;
            }
            catch
            {
                // При ошибке загрузки просто не показываем изображение
            }
        }

        // Переход к предыдущей фотографии
        // Вызывается кнопкой ❮ или клавишей ←
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                _ = LoadCurrentPhotoAsync();
            }
        }

        // Переход к следующей фотографии
        // Вызывается кнопкой ❯ или клавишей →
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _photos.Count - 1)
            {
                _currentIndex++;
                _ = LoadCurrentPhotoAsync();
            }
        }

        // Закрывает полноэкранное окно и возвращает управление MainWindow
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Обработчик нажатий клавиш клавиатуры
        // Обеспечивает навигацию без использования мыши
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    // Escape — закрыть полноэкранный режим
                    Close();
                    break;
                case Key.Left:
                    // Стрелка влево — предыдущее фото
                    PrevButton_Click(sender, e);
                    break;
                case Key.Right:
                    // Стрелка вправо — следующее фото
                    NextButton_Click(sender, e);
                    break;
            }
        }
    }
}