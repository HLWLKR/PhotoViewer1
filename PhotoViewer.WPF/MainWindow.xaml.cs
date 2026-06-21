// ============================================================
// PhotoViewer.WPF — Code-behind главного окна
// Содержит всю бизнес-логику WPF-клиента:
// подключение к серверу, загрузка фото, навигация,
// слайд-шоу, зум, альбомы, темы и многое другое
// ============================================================

using Microsoft.Win32;
using PhotoViewer.Shared;
using PhotoViewer.WPF.Services;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoViewer.WPF
{
    // Code-behind главного окна приложения
    // Взаимодействует с MainWindow.xaml (UI) через именованные элементы (x:Name)
    // Использует ApiService для всех запросов к серверу
    public partial class MainWindow : Window
    {
        // ── Поля состояния ─────────────────────────────────────────────────

        // Сервис API — null до первого подключения
        private ApiService? _apiService;

        // Все загруженные фотографии с сервера
        private List<PhotoDto> _photos = new();

        // Фотографии после применения поиска, фильтра и сортировки
        private List<PhotoDto> _filteredPhotos = new();

        // Все альбомы с сервера
        private List<AlbumDto> _albums = new();

        // Текущая выбранная фотография (отображается в области просмотра)
        private PhotoDto? _currentPhoto;

        // Текущий выбранный альбом (null = показываем все фото)
        private AlbumDto? _currentAlbum;

        // Текущий масштаб изображения (1.0 = 100%, 2.0 = 200%)
        private double _zoomLevel = 1.0;

        // Таймер для слайд-шоу — null когда слайд-шоу не запущено
        private DispatcherTimer? _slideshowTimer;

        // Флаги состояния
        private bool _slideshowRunning = false;   // Запущено ли слайд-шоу
        private bool _showOnlyFavorites = false;  // Фильтр "только избранные"
        private bool _isGridView = true;          // true = сетка, false = список
        private bool _isDarkTheme = true;         // true = тёмная, false = светлая

        public MainWindow()
        {
            InitializeComponent(); // Загружает MainWindow.xaml и создаёт UI элементы
            StatusText.Text = "Введите адрес сервера и нажмите «Подключиться»";
        }

        // ── Инициализация и автоподключение ───────────────────────────────

        // Вызывается после полной загрузки окна (событие Loaded в XAML)
        // Пытается автоматически подключиться к последнему серверу
        // Адрес сохраняется в файле settings.json в AppData
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var savedUrl = LoadServerUrl(); // Читаем сохранённый URL
            if (!string.IsNullOrEmpty(savedUrl))
            {
                ServerUrlBox.Text = savedUrl;
                _apiService = new ApiService(savedUrl);
                try
                {
                    await LoadPhotosAsync();
                    await LoadAlbumsAsync();
                    await LoadStatsAsync();
                    SetConnectionIndicator(true); // Зелёный индикатор
                }
                catch
                {
                    SetConnectionIndicator(false); // Красный индикатор
                }
            }
        }

        // ── Подключение к серверу ─────────────────────────────────────────

        // Обработчик кнопки "Подключиться"
        // Создаёт ApiService с введённым URL и загружает данные
        // При успехе сохраняет URL для автоподключения
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ServerUrlBox.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            _apiService = new ApiService(url);
            try
            {
                await LoadPhotosAsync();
                await LoadAlbumsAsync();
                await LoadStatsAsync();
                SetConnectionIndicator(true);
                SaveServerUrl(url); // Сохраняем для следующего запуска
            }
            catch
            {
                SetConnectionIndicator(false);
            }
        }

        // Обновляет цвет индикатора подключения
        // Зелёный = сервер доступен, Красный = нет связи
        // ConnectionIndicator — элемент Ellipse в шапке окна
        private void SetConnectionIndicator(bool online)
        {
            ConnectionIndicator.Fill = online
                ? new SolidColorBrush(Color.FromRgb(0, 200, 100))   // Зелёный
                : new SolidColorBrush(Color.FromRgb(200, 50, 50));   // Красный
        }

        // ── Загрузка данных с сервера ─────────────────────────────────────

        // Загружает фотографии с сервера
        // Если выбран альбом — загружает только фото этого альбома
        // После загрузки применяет поиск и сортировку
        private async Task LoadPhotosAsync()
        {
            try
            {
                StatusText.Text = "Загрузка...";
                // Если выбран альбом — загружаем его фото, иначе все фото
                if (_currentAlbum != null)
                    _photos = await _apiService!.GetAlbumPhotosAsync(_currentAlbum.Id);
                else
                    _photos = await _apiService!.GetAllPhotosAsync();

                ApplySearchAndSort(); // Обновляем отображаемый список
                StatusText.Text = $"Загружено: {_photos.Count}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
                SetConnectionIndicator(false);
            }
        }

        // Загружает список альбомов и обновляет AlbumsListBox
        private async Task LoadAlbumsAsync()
        {
            if (_apiService == null) return;
            try
            {
                _albums = await _apiService.GetAlbumsAsync();
                // Сброс и переприсвоение ItemsSource обновляет список в UI
                AlbumsListBox.ItemsSource = null;
                AlbumsListBox.ItemsSource = _albums;
            }
            catch { }
        }

        // Загружает статистику сервера и обновляет строку статистики
        private async Task LoadStatsAsync()
        {
            if (_apiService == null) return;
            try
            {
                var stats = await _apiService.GetStatsAsync();
                if (stats == null) return;
                StatsTotalText.Text = $"📊 Фотографий: {stats.TotalPhotos}";
                StatsSizeText.Text = $"💾 Размер: {stats.TotalSizeFormatted}";
                StatsLastUploadText.Text = stats.LastUpload.HasValue
                    ? $"🕐 {stats.LastUpload.Value.ToLocalTime():dd.MM.yyyy HH:mm}"
                    : "";
            }
            catch { }
        }

        // ── Поиск, фильтрация и сортировка ───────────────────────────────

        // Вызывается при изменении текста в поле поиска
        // Управляет видимостью подсказки "Поиск по названию..."
        // и применяет фильтрацию
        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Показываем подсказку только когда поле пустое
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            ApplySearchAndSort();
        }

        // Вызывается при изменении выбора в выпадающем списке сортировки
        private void SortComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ApplySearchAndSort();
        }

        // Применяет поиск, фильтр избранных и сортировку к списку фотографий
        // Обновляет PhotoListBox и счётчик PhotoCountText
        // Вызывается при: изменении поиска, смене сортировки,
        // переключении фильтра избранных, загрузке новых фото
        private void ApplySearchAndSort()
        {
            // Null-проверка (метод может вызваться до полной инициализации UI)
            if (PhotoListBox == null || SortComboBox == null) return;

            var query = SearchBox.Text;

            // Шаг 1: Фильтрация по поисковому запросу
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _photos.ToList()
                : _photos.Where(p =>
                    // Поиск по имени файла (оригинальному) или описанию, без учёта регистра
                    p.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            // Шаг 2: Фильтр "только избранные"
            if (_showOnlyFavorites)
                filtered = filtered.Where(p => p.IsFavorite).ToList();

            // Шаг 3: Сортировка (switch expression — C# 8.0)
            // SelectedIndex соответствует порядку ComboBoxItem в XAML
            _filteredPhotos = SortComboBox.SelectedIndex switch
            {
                0 => filtered.OrderByDescending(p => p.UploadedAt).ToList(), // Новые первыми
                1 => filtered.OrderBy(p => p.UploadedAt).ToList(),           // Старые первыми
                2 => filtered.OrderBy(p => p.OriginalFileName).ToList(),     // А → Я
                3 => filtered.OrderByDescending(p => p.OriginalFileName).ToList(), // Я → А
                _ => filtered
            };

            // Обновляем список в UI
            PhotoListBox.ItemsSource = null;
            PhotoListBox.ItemsSource = _filteredPhotos;
            PhotoCountText.Text = _filteredPhotos.Count.ToString();
        }

        // Переключает фильтр "только избранные"
        // Меняет цвет кнопки ★ на золотой/серый
        private void FavoriteFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _showOnlyFavorites = !_showOnlyFavorites;
            // Меняем цвет звёздочки: золотой = активен, серый = неактивен
            FavoriteFilterButton.Foreground = _showOnlyFavorites
                ? new SolidColorBrush(Color.FromRgb(255, 215, 0))  // Золотой
                : new SolidColorBrush(Color.FromRgb(85, 85, 85));  // Серый
            ApplySearchAndSort();
        }

        // Переключает вид галереи: сетка ↔ список
        // Заменяет ItemTemplate у PhotoListBox
        // DataTemplate определены в MainWindow.xaml как ресурсы
        private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isGridView = !_isGridView;
            // Получаем шаблон по ключу из ресурсов окна
            var template = _isGridView
                ? (DataTemplate)Resources["GridItemTemplate"]
                : (DataTemplate)Resources["ListItemTemplate"];
            PhotoListBox.ItemTemplate = template;
            ViewToggleButton.Content = _isGridView ? "▦" : "☰";
        }

        // ── Переключение темы ─────────────────────────────────────────────

        // Переключает между тёмной и светлой темой
        // Механизм: все цвета UI определены как DynamicResource в XAML
        // При изменении значений ресурсов в словаре Resources
        // WPF автоматически обновляет все привязанные элементы
        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            if (_isDarkTheme)
            {
                // Тёмная тема
                Resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(26, 26, 46));
                Resources["HeaderBrush"] = new SolidColorBrush(Color.FromRgb(22, 33, 62));
                Resources["PanelBrush"] = new SolidColorBrush(Color.FromRgb(15, 52, 96));
                Resources["StatsBrush"] = new SolidColorBrush(Color.FromRgb(18, 25, 43));
                Resources["TextBrush"] = new SolidColorBrush(Colors.White);
                Resources["SubTextBrush"] = new SolidColorBrush(Color.FromRgb(160, 160, 176));
                Resources["InputBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(15, 52, 96));
                Resources["InputForegroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["LogoTextBrush"] = new SolidColorBrush(Colors.White);
                Resources["InputBorderBrush"] = new SolidColorBrush(Color.FromRgb(233, 69, 96));
                Resources["ButtonPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(233, 69, 96));
                Resources["ButtonSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(15, 52, 96));
                Resources["ButtonGreenBrush"] = new SolidColorBrush(Color.FromRgb(27, 107, 58));
                Resources["ButtonOrangeBrush"] = new SolidColorBrush(Color.FromRgb(184, 92, 0));
                Resources["HoverBrush"] = new SolidColorBrush(Color.FromRgb(15, 52, 96));
                Resources["ScrollThumbBrush"] = new SolidColorBrush(Color.FromRgb(233, 69, 96));
                Resources["ScrollTrackBrush"] = new SolidColorBrush(Color.FromRgb(22, 33, 62));
                ThemeButton.Content = "🌙";
            }
            else
            {
                // Светлая тема
                Resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(235, 244, 255));
                Resources["HeaderBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                Resources["PanelBrush"] = new SolidColorBrush(Color.FromRgb(204, 223, 245));
                Resources["StatsBrush"] = new SolidColorBrush(Color.FromRgb(220, 236, 255));
                Resources["TextBrush"] = new SolidColorBrush(Color.FromRgb(26, 42, 74));
                Resources["SubTextBrush"] = new SolidColorBrush(Color.FromRgb(74, 96, 128));
                Resources["InputBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(245, 249, 255));
                Resources["InputForegroundBrush"] = new SolidColorBrush(Color.FromRgb(26, 42, 74));
                Resources["LogoTextBrush"] = new SolidColorBrush(Color.FromRgb(2, 119, 189));
                Resources["InputBorderBrush"] = new SolidColorBrush(Color.FromRgb(2, 119, 189));
                Resources["ButtonPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(2, 119, 189));
                Resources["ButtonSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(1, 87, 155));
                Resources["ButtonGreenBrush"] = new SolidColorBrush(Color.FromRgb(2, 136, 100));
                Resources["ButtonOrangeBrush"] = new SolidColorBrush(Color.FromRgb(0, 131, 176));
                Resources["HoverBrush"] = new SolidColorBrush(Color.FromRgb(179, 212, 240));
                Resources["ScrollThumbBrush"] = new SolidColorBrush(Color.FromRgb(2, 119, 189));
                Resources["ScrollTrackBrush"] = new SolidColorBrush(Color.FromRgb(220, 236, 255));
                ThemeButton.Content = "☀️";
            }
        }

        // ── Управление альбомами ──────────────────────────────────────────

        // Создаёт новый альбом. Запрашивает имя через диалоговое окно InputBox
        private async void CreateAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null) return;
            // InputBox из Microsoft.VisualBasic — простой диалог ввода текста
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название альбома:", "Новый альбом", "");
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                var album = await _apiService.CreateAlbumAsync(name);
                if (album != null)
                {
                    StatusText.Text = $"Альбом «{album.Name}» создан";
                    await LoadAlbumsAsync();
                }
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Удаляет альбом. ID альбома передаётся через Tag кнопки в DataTemplate
        // sender as Button — получаем кнопку из DataTemplate альбома
        // btn.Tag = {Binding Id} — ID альбома установлен в XAML
        private async void DeleteAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null) return;
            if (sender is not System.Windows.Controls.Button btn) return;
            var albumId = (int)btn.Tag;
            var album = _albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null) return;

            var confirm = MessageBox.Show($"Удалить альбом «{album.Name}»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _apiService.DeleteAlbumAsync(albumId);
                // Если удалили текущий альбом — возвращаемся к "все фото"
                if (_currentAlbum?.Id == albumId)
                {
                    _currentAlbum = null;
                    GalleryHeaderText.Text = "Все фото";
                    await LoadPhotosAsync();
                }
                await LoadAlbumsAsync();
                StatusText.Text = "Альбом удалён";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Добавляет текущую выбранную фотографию в альбом
        // ID альбома передаётся через Tag кнопки ＋ в DataTemplate альбома
        private async void AddToAlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null)
            {
                StatusText.Text = "Сначала выберите фото";
                return;
            }
            if (sender is not System.Windows.Controls.Button btn) return;
            var albumId = (int)btn.Tag;
            var album = _albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null) return;

            try
            {
                await _apiService.AddPhotoToAlbumAsync(albumId, _currentPhoto.Id);
                StatusText.Text = $"Фото добавлено в альбом «{album.Name}»";
                await LoadAlbumsAsync(); // Обновляем счётчик фото в альбоме
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Фильтрует галерею по выбранному альбому
        private async void AlbumsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AlbumsListBox.SelectedItem is not AlbumDto album) return;
            _currentAlbum = album;
            GalleryHeaderText.Text = album.Name;
            await LoadPhotosAsync(); // Загружаем только фото этого альбома
        }

        // Сбрасывает фильтр альбома — показывает все фотографии
        private async void ShowAllPhotosButton_Click(object sender, RoutedEventArgs e)
        {
            _currentAlbum = null;
            GalleryHeaderText.Text = "Все фото";
            AlbumsListBox.SelectedItem = null;
            await LoadPhotosAsync();
        }

        // ── Просмотр фотографий ───────────────────────────────────────────

        // Вызывается при выборе фото в галерее (ListBox)
        // Обновляет счётчик выделенных элементов и загружает фото для просмотра
        private async void PhotoListBox_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Показываем сколько фото выделено (Ctrl+Click для множественного выбора)
            var selected = PhotoListBox.SelectedItems.Count;
            SelectionText.Text = selected > 1 ? $"Выбрано: {selected}" : "";

            if (PhotoListBox.SelectedItem is not PhotoDto photo) return;
            if (_apiService == null) return;
            await ShowPhotoAsync(photo);
        }

        // Загружает и отображает фотографию в области просмотра
        // Обновляет все информационные поля: описание, дату, разрешение, размер
        private async Task ShowPhotoAsync(PhotoDto photo)
        {
            try
            {
                _currentPhoto = photo;
                var bytes = await _apiService!.DownloadPhotoAsync(photo.Url);

                // Создаём BitmapImage из байт в памяти
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Кэшируем полностью
                bitmap.StreamSource = ms;
                bitmap.EndInit();

                PreviewImage.Source = bitmap;
                ResetZoom(); // Сбрасываем зум при смене фото
                // Применяем сохранённый угол поворота из БД
                ImageRotateTransform.Angle = photo.Rotation;

                // Обновляем информационную панель
                PhotoNameText.Text = string.IsNullOrEmpty(photo.OriginalFileName)
                    ? photo.FileName : photo.OriginalFileName;
                DescriptionText.Text = string.IsNullOrEmpty(photo.Description)
                    ? "Без описания" : photo.Description;
                DateText.Text = $"Загружено: {photo.UploadedAt.ToLocalTime():dd.MM.yyyy HH:mm}";
                // Отображаем разрешение и размер если известны
                ResolutionText.Text = photo.Width > 0 ? $"📐 {photo.Width} × {photo.Height} px" : "";
                FileSizeText.Text = photo.FileSize > 0 ? $"📦 {FormatSize(photo.FileSize)}" : "";
                // Обновляем кнопку избранного
                FavoriteButton.Content = photo.IsFavorite ? "★" : "☆";
                StatusText.Text = photo.OriginalFileName;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
            }
        }

        // ── Зум (масштабирование) ─────────────────────────────────────────

        // Обработчик колёсика мыши для зума изображения
        // ScaleTransform масштабирует изображение относительно центра (RenderTransformOrigin=0.5,0.5)
        // Диапазон: 50% - 500%
        private void PreviewBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (PreviewImage.Source == null) return;
            // e.Delta > 0 — прокрутка вверх (увеличение), < 0 — вниз (уменьшение)
            _zoomLevel += e.Delta > 0 ? 0.1 : -0.1;
            // Ограничиваем диапазон зума
            _zoomLevel = Math.Max(0.5, Math.Min(5.0, _zoomLevel));
            ImageScaleTransform.ScaleX = _zoomLevel;
            ImageScaleTransform.ScaleY = _zoomLevel;
            StatusText.Text = $"Масштаб: {_zoomLevel:P0}";
        }

        // Сбрасывает масштаб до 100%
        private void ResetZoom()
        {
            _zoomLevel = 1.0;
            ImageScaleTransform.ScaleX = 1.0;
            ImageScaleTransform.ScaleY = 1.0;
        }

        // Открывает полноэкранный просмотр по двойному клику на изображение
        // Передаёт список текущих отфильтрованных фото и индекс выбранного
        // ShowDialog() — блокирует основное окно до закрытия полноэкранного
        private void PreviewImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && _apiService != null && _filteredPhotos.Count > 0)
            {
                var idx = _currentPhoto != null ? _filteredPhotos.IndexOf(_currentPhoto) : 0;
                new FullScreenWindow(_apiService, _filteredPhotos, idx).ShowDialog();
            }
        }

        // ── Навигация по галерее ──────────────────────────────────────────

        // Переход к предыдущей фотографии
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredPhotos.Count == 0) return;
            var idx = _currentPhoto != null ? _filteredPhotos.IndexOf(_currentPhoto) : -1;
            if (idx > 0) PhotoListBox.SelectedItem = _filteredPhotos[idx - 1];
        }

        // Переход к следующей фотографии
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredPhotos.Count == 0) return;
            var idx = _currentPhoto != null ? _filteredPhotos.IndexOf(_currentPhoto) : -1;
            if (idx < _filteredPhotos.Count - 1) PhotoListBox.SelectedItem = _filteredPhotos[idx + 1];
        }

        // Глобальный обработчик клавиатуры для всего окна
        // Позволяет управлять приложением без мыши
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left: PrevButton_Click(sender, e); break; // ← предыдущее фото
                case Key.Right: NextButton_Click(sender, e); break; // → следующее фото
                case Key.Escape: if (_slideshowRunning) StopSlideshow(); break; // Esc = стоп слайд-шоу
                case Key.Delete: if (_currentPhoto != null) DeleteButton_Click(sender, e); break; // Del = удалить
                case Key.D0: case Key.NumPad0: ResetZoom(); break; // 0 = сброс зума
            }
        }

        // ── Слайд-шоу ─────────────────────────────────────────────────────

        // Запускает или останавливает слайд-шоу
        private void SlideshowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_slideshowRunning) StopSlideshow();
            else StartSlideshow();
        }

        // Запускает слайд-шоу
        // DispatcherTimer работает в UI-потоке — безопасно обновляет WPF-элементы
        // Интервал: 3 секунды между фотографиями
        private void StartSlideshow()
        {
            if (_filteredPhotos.Count == 0) return;
            _slideshowRunning = true;
            SlideshowButton.Content = "⏹  Стоп";
            _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _slideshowTimer.Tick += SlideshowTimer_Tick; // Подписываемся на событие таймера
            _slideshowTimer.Start();
            StatusText.Text = "Слайд-шоу запущено";
        }

        // Останавливает слайд-шоу и освобождает таймер
        private void StopSlideshow()
        {
            _slideshowRunning = false;
            SlideshowButton.Content = "▶  Слайд-шоу";
            _slideshowTimer?.Stop();  // ?. — безопасный вызов если timer == null
            _slideshowTimer = null;
            StatusText.Text = "Слайд-шоу остановлено";
        }

        // Вызывается каждые 3 секунды таймером слайд-шоу
        // Переходит к следующему фото, при достижении конца — начинает сначала
        private void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            if (_filteredPhotos.Count == 0) return;
            var idx = _currentPhoto != null ? _filteredPhotos.IndexOf(_currentPhoto) : -1;
            // % _filteredPhotos.Count — цикличный переход (после последнего → первое)
            PhotoListBox.SelectedItem = _filteredPhotos[(idx + 1) % _filteredPhotos.Count];
        }

        // ── Действия с фотографиями ───────────────────────────────────────

        // Переключает статус "Избранное" для текущего фото
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            try
            {
                await _apiService.ToggleFavoriteAsync(_currentPhoto.Id);
                _currentPhoto.IsFavorite = !_currentPhoto.IsFavorite;
                FavoriteButton.Content = _currentPhoto.IsFavorite ? "★" : "☆";
                StatusText.Text = _currentPhoto.IsFavorite ? "Добавлено в избранное" : "Удалено из избранного";
                ApplySearchAndSort(); // Обновляем список (для фильтра избранных)
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Поворачивает фото на 90° влево (-90°)
        private async void RotateLeftButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            try
            {
                await _apiService.RotateAsync(_currentPhoto.Id, -90);
                _currentPhoto.Rotation = (_currentPhoto.Rotation - 90 + 360) % 360;
                ImageRotateTransform.Angle = _currentPhoto.Rotation; // Обновляем трансформацию
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Поворачивает фото на 90° вправо (+90°)
        private async void RotateRightButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            try
            {
                await _apiService.RotateAsync(_currentPhoto.Id, 90);
                _currentPhoto.Rotation = (_currentPhoto.Rotation + 90) % 360;
                ImageRotateTransform.Angle = _currentPhoto.Rotation;
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Копирует текущее фото в буфер обмена Windows
        // Clipboard.SetImage — стандартный метод WPF для копирования изображений
        // После этого фото можно вставить в Paint, Word и т.д. через Ctrl+V
        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            try
            {
                var bytes = await _apiService.DownloadPhotoAsync(_currentPhoto.Url);
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                Clipboard.SetImage(bitmap); // Копируем в буфер обмена
                StatusText.Text = "Скопировано в буфер";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Открывает диалог редактирования описания фото
        private async void EditDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            var newDesc = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите описание:", "Описание", _currentPhoto.Description);
            if (newDesc == null) return;
            try
            {
                await _apiService.UpdateDescriptionAsync(_currentPhoto.Id, newDesc);
                _currentPhoto.Description = newDesc;
                DescriptionText.Text = string.IsNullOrEmpty(newDesc) ? "Без описания" : newDesc;
                StatusText.Text = "Описание обновлено";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Открывает диалог переименования фотографии
        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите новое имя:", "Переименовать", _currentPhoto.OriginalFileName);
            if (string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                await _apiService.RenamePhotoAsync(_currentPhoto.Id, newName);
                _currentPhoto.OriginalFileName = newName;
                PhotoNameText.Text = newName;
                StatusText.Text = "Фото переименовано";
                ApplySearchAndSort(); // Обновляем список с новым именем
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // ── Загрузка и экспорт ────────────────────────────────────────────

        // Открывает диалог выбора файлов и загружает их на сервер
        // При выборе одного файла — запрашивает описание
        // При выборе нескольких — загружает без описания с прогресс-баром
        // Если открыт альбом — автоматически добавляет фото в текущий альбом
        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null) return;
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Multiselect = true // Разрешаем выбор нескольких файлов
            };
            if (dialog.ShowDialog() != true) return;

            ProgressPanel.Visibility = Visibility.Visible; // Показываем прогресс-бар
            int success = 0;

            if (dialog.FileNames.Length == 1)
            {
                // Одиночная загрузка — запрашиваем описание
                var desc = Microsoft.VisualBasic.Interaction.InputBox(
                    "Описание (необязательно):", "Описание", "");
                try
                {
                    UploadProgressBar.Value = 0;
                    ProgressText.Text = "Загрузка...";
                    var result = await _apiService.UploadPhotoAsync(dialog.FileName, desc);
                    if (result != null)
                    {
                        success = 1;
                        // Если открыт альбом — добавляем в него
                        if (_currentAlbum != null)
                            await _apiService.AddPhotoToAlbumAsync(_currentAlbum.Id, result.Id);
                    }
                    UploadProgressBar.Value = 100;
                    ProgressText.Text = "Готово";
                }
                catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
            }
            else
            {
                // Множественная загрузка — обновляем прогресс-бар
                for (int i = 0; i < dialog.FileNames.Length; i++)
                {
                    try
                    {
                        // Прогресс в процентах: (текущий + 1) / всего * 100
                        UploadProgressBar.Value = (i + 1) * 100 / dialog.FileNames.Length;
                        ProgressText.Text = $"{i + 1} из {dialog.FileNames.Length}";
                        var result = await _apiService.UploadPhotoAsync(dialog.FileNames[i], "");
                        if (result != null)
                        {
                            success++;
                            if (_currentAlbum != null)
                                await _apiService.AddPhotoToAlbumAsync(_currentAlbum.Id, result.Id);
                        }
                    }
                    catch { }
                }
            }

            ProgressPanel.Visibility = Visibility.Collapsed; // Скрываем прогресс-бар
            StatusText.Text = $"Загружено {success} из {dialog.FileNames.Length}";
            await LoadPhotosAsync();
            await LoadAlbumsAsync();
            await LoadStatsAsync();
        }

        // Обработчик перетаскивания файлов в окно (Drag and Drop)
        // Фильтрует только изображения, загружает их на сервер
        // AllowDrop="True" в XAML разрешает принимать файлы
        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (_apiService == null) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            // Получаем массив путей перетащенных файлов
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Фильтруем только файлы изображений по расширению
            var imageFiles = files.Where(f =>
                new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }
                .Contains(Path.GetExtension(f).ToLower())).ToList();
            if (imageFiles.Count == 0) return;

            ProgressPanel.Visibility = Visibility.Visible;
            int success = 0;
            for (int i = 0; i < imageFiles.Count; i++)
            {
                try
                {
                    UploadProgressBar.Value = (i + 1) * 100 / imageFiles.Count;
                    ProgressText.Text = $"{i + 1} из {imageFiles.Count}";
                    var result = await _apiService.UploadPhotoAsync(imageFiles[i], "");
                    if (result != null)
                    {
                        success++;
                        if (_currentAlbum != null)
                            await _apiService.AddPhotoToAlbumAsync(_currentAlbum.Id, result.Id);
                    }
                }
                catch { }
            }
            ProgressPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Загружено: {success}";
            await LoadPhotosAsync();
            await LoadAlbumsAsync();
            await LoadStatsAsync();
        }

        // Вызывается при перетаскивании файлов над окном
        // Устанавливает курсор Copy если перетаскиваются файлы
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy   // Показываем иконку "копировать"
                : DragDropEffects.None;  // Запрещаем Drop если не файлы
            e.Handled = true;
        }

        // Скачивает текущую фотографию на локальный ПК
        // SaveFileDialog — диалог выбора пути для сохранения
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _currentPhoto == null) return;
            var dialog = new SaveFileDialog
            {
                FileName = _currentPhoto.OriginalFileName,
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var bytes = await _apiService.DownloadPhotoAsync(_currentPhoto.Url);
                await File.WriteAllBytesAsync(dialog.FileName, bytes); // Сохраняем на диск
                StatusText.Text = $"Сохранено: {dialog.FileName}";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
        }

        // Экспортирует фотографии в ZIP-архив
        // Если выделено несколько фото (Ctrl+Click) — экспортирует выделенные
        // Иначе экспортирует все отфильтрованные фото
        // ZipArchive из System.IO.Compression — стандартный .NET инструмент для ZIP
        private async void ExportZipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null || _filteredPhotos.Count == 0) return;

            // Определяем что экспортировать: выделенные или все
            var photosToExport = PhotoListBox.SelectedItems.Count > 1
                ? PhotoListBox.SelectedItems.Cast<PhotoDto>().ToList()
                : _filteredPhotos;

            var dialog = new SaveFileDialog
            {
                FileName = "photos_export.zip",
                Filter = "ZIP архив|*.zip"
            };
            if (dialog.ShowDialog() != true) return;

            ProgressPanel.Visibility = Visibility.Visible;
            try
            {
                // Создаём ZIP-архив в потоке файла
                using var zipStream = new FileStream(dialog.FileName, FileMode.Create);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

                for (int i = 0; i < photosToExport.Count; i++)
                {
                    var photo = photosToExport[i];
                    UploadProgressBar.Value = (i + 1) * 100 / photosToExport.Count;
                    ProgressText.Text = $"{i + 1} из {photosToExport.Count}";

                    var bytes = await _apiService.DownloadPhotoAsync(photo.Url);
                    // Используем оригинальное имя файла внутри архива
                    var entryName = string.IsNullOrEmpty(photo.OriginalFileName)
                        ? photo.FileName : photo.OriginalFileName;

                    // Создаём запись в архиве и записываем байты
                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(bytes);
                }
                StatusText.Text = $"Экспортировано {photosToExport.Count} фото";
            }
            catch (Exception ex) { StatusText.Text = $"Ошибка: {ex.Message}"; }
            finally
            {
                ProgressPanel.Visibility = Visibility.Collapsed;
            }
        }

        // ── Удаление ──────────────────────────────────────────────────────

        // Удаляет одну или несколько фотографий
        // Если выделено несколько (Ctrl+Click) — удаляет все выделенные
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null) return;

            // Определяем что удалять
            var selectedPhotos = PhotoListBox.SelectedItems.Count > 1
                ? PhotoListBox.SelectedItems.Cast<PhotoDto>().ToList()
                : _currentPhoto != null ? new List<PhotoDto> { _currentPhoto } : new List<PhotoDto>();

            if (selectedPhotos.Count == 0) return;

            // Подтверждение: разный текст для одного и нескольких фото
            var confirm = MessageBox.Show(
                selectedPhotos.Count == 1
                    ? $"Удалить фото «{selectedPhotos[0].OriginalFileName}»?"
                    : $"Удалить {selectedPhotos.Count} фотографий?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int deleted = 0;
            foreach (var photo in selectedPhotos)
            {
                try { if (await _apiService.DeletePhotoAsync(photo.Id)) deleted++; }
                catch { }
            }

            // Очищаем область просмотра
            PreviewImage.Source = null;
            PhotoNameText.Text = "";
            DescriptionText.Text = "";
            DateText.Text = "";
            ResolutionText.Text = "";
            FileSizeText.Text = "";
            _currentPhoto = null;

            await LoadPhotosAsync();
            await LoadStatsAsync();
            StatusText.Text = $"Удалено: {deleted}";
        }

        // ── Сохранение настроек ───────────────────────────────────────────

        // Возвращает путь к файлу настроек settings.json
        // %AppData%\PhotoViewer\settings.json
        // AppData — стандартная папка для пользовательских данных приложения
        private static string GetSettingsPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PhotoViewer", "settings.json");

        // Читает сохранённый URL сервера из файла настроек
        // Возвращает пустую строку если файл не найден или произошла ошибка
        private static string LoadServerUrl()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return "";
                var json = File.ReadAllText(path);
                var dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return dict?.GetValueOrDefault("LastServerUrl") ?? "";
            }
            catch { return ""; }
        }

        // Сохраняет URL сервера в файл настроек
        // Создаёт папку если не существует
        private static void SaveServerUrl(string url)
        {
            try
            {
                var path = GetSettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); // Создаём папку
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    new Dictionary<string, string> { { "LastServerUrl", url } });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        // ── Вспомогательные методы ────────────────────────────────────────

        // Форматирует размер файла в читаемый вид
        // Используется для отображения FileSize в информационной панели
        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024):F1} МБ";
        }

        // Обработчик кнопки "Обновить"
        // Перезагружает фотографии, альбомы и статистику с сервера
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_apiService == null) return;
            await LoadPhotosAsync();
            await LoadAlbumsAsync();
            await LoadStatsAsync();
        }
    }
}