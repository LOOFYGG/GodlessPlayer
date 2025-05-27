using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using GodlessPlayer.Data;
using System.Windows.Controls;

namespace GodlessPlayer
{
    public partial class MainWindow : Window
    {
        // Контекст базы данных
        private readonly LibraryContext _libraryContext = new();

        // Полный список треков и отфильтрованный (например, для поиска)
        private List<Track> allTracks = new();
        private List<Track> filteredTracks = new();

        // Проигрыватель и управление воспроизведением
        private MediaPlayer mediaPlayer = new();
        private int currentIndex = -1;      // Индекс текущего воспроизводимого трека
        private bool isPlaying = false;     // Играет ли сейчас музыка
        private DispatcherTimer timer = new();  // Таймер для обновления прогресс-бара
        private bool isDragging = false;    // Перетаскивает ли пользователь слайдер прогресса

        // Конструктор окна
        public MainWindow()
        {
            InitializeComponent();
            LoadTracksFromDatabase(); // Загрузка треков из БД
            RefreshTrackListView();   // Обновление списка
            TrackListView.ItemsSource = allTracks; // Сначала привязываем полный список
            TrackListView.ItemsSource = filteredTracks; // Потом — отфильтрованный (только он используется)

            // Подписка на события проигрывателя и таймера
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.Volume = VolumeSlider.Value;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        // Воспроизведение трека
        private void PlayTrack(Track track)
        {
            if (track == null) return;

            mediaPlayer.Open(new Uri(track.Path));
            mediaPlayer.Play();
            isPlaying = true;

            NowPlayingText.Text = $"Сейчас играет: {track.Title} — {track.Artist?.Name ?? "Неизвестен"}";

            // Когда файл откроется, обновим длительность
            mediaPlayer.MediaOpened += (s, e) =>
            {
                if (mediaPlayer.NaturalDuration.HasTimeSpan)
                {
                    TrackProgressSlider.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                    TotalTimeText.Text = mediaPlayer.NaturalDuration.TimeSpan.ToString("mm\\:ss");
                }
            };

            timer.Start();
        }

        // Загрузка MP3-файлов из проводника
        private void LoadFiles_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "MP3 файлы (*.mp3)|*.mp3",
                Title = "Выберите MP3-файлы"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var filePath in dialog.FileNames)
                {
                    if (!File.Exists(filePath)) continue;

                    // Задание значений по умолчанию
                    string defaultArtistName = "Неизвестен";
                    string defaultAlbumName = "Без альбома";
                    string defaultGenreName = "Какой?";

                    // Поиск существующих или создание новых объектов
                    var artist = _libraryContext.Artists.FirstOrDefault(a => a.Name == defaultArtistName)
                                 ?? new Artist { Name = defaultArtistName };

                    var album = _libraryContext.Albums.FirstOrDefault(a => a.Name == defaultAlbumName)
                                ?? new Album { Name = defaultAlbumName };

                    var genre = _libraryContext.Genres.FirstOrDefault(g => g.Name == defaultGenreName)
                                 ?? new Genre { Name = defaultGenreName };

                    // Создание нового трека
                    var track = new Track
                    {
                        Path = filePath,
                        Title = Path.GetFileNameWithoutExtension(filePath),
                        Artist = artist,
                        Album = album,
                        Genre = genre
                    };

                    allTracks.Add(track);
                    filteredTracks.Add(track);
                    _libraryContext.Tracks.Add(track);
                }

                _libraryContext.SaveChanges();
                RefreshTrackListView();
            }
        }

        // Удаление выбранного трека
        private void DeleteTrack_Click(object sender, RoutedEventArgs e)
        {
            if (TrackListView.SelectedItem is Track selected)
            {
                if (MessageBox.Show($"Удалить трек \"{selected.Title}\"?", "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    filteredTracks.Remove(selected);
                    allTracks.Remove(selected);
                    _libraryContext.Tracks.Remove(selected);
                    _libraryContext.SaveChanges();
                    RefreshTrackListView();
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите трек для удаления.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Редактирование выбранного трека
        private void EditTrack_Click(object sender, RoutedEventArgs e)
        {
            if (TrackListView.SelectedItem is Track selected)
            {
                var dialog = new EditTrackWindow(selected);
                if (dialog.ShowDialog() == true)
                {
                    RefreshTrackListView(); // обновим список после изменений
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите трек для редактирования.", "Редактирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Обновление отображения списка треков
        private void RefreshTrackListView()
        {
            if (TrackListView == null) return;
            TrackListView.ItemsSource = null;        // Сброс
            TrackListView.ItemsSource = filteredTracks; // Установка отфильтрованного списка
            TrackListView.Items.Refresh();           // Принудительное обновление
        }

        // Поиск визуального родителя в дереве визуальных элементов
        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                    return (T)current;

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // Обработка двойного клика по треку — начать воспроизведение
        private void TrackListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var header = FindAncestor<GridViewColumnHeader>(source);
                if (header != null) return;
            }

            if (TrackListView.SelectedItem is Track selectedTrack)
            {
                int index = filteredTracks.IndexOf(selectedTrack);
                if (index != -1)
                {
                    currentIndex = index;
                    PlayTrack(allTracks[currentIndex]);
                }
            }
        }

        // Кнопка "Предыдущий"
        private void PreviousTrack_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;

            currentIndex = (currentIndex - 1 + allTracks.Count) % allTracks.Count;
            PlayTrack(allTracks[currentIndex]);
        }

        // Кнопка воспроизведения/паузы
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex < 0 && allTracks.Count > 0)
            {
                currentIndex = 0;
                PlayTrack(allTracks[currentIndex]);
                return;
            }

            if (isPlaying)
            {
                mediaPlayer.Pause();
                isPlaying = false;
                timer.Stop();
            }
            else
            {
                mediaPlayer.Play();
                isPlaying = true;
                timer.Start();
            }
        }

        // Кнопка "Следующий"
        private void NextTrack_Click(object sender, RoutedEventArgs e)
        {
            if (ShuffleCheckBox.IsChecked == true)
            {
                var rnd = new Random();
                currentIndex = rnd.Next(allTracks.Count);
            }
            else
            {
                currentIndex = (currentIndex + 1) % allTracks.Count;
            }

            PlayTrack(allTracks[currentIndex]);
        }

        // Событие окончания трека
        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (RepeatCheckBox.IsChecked == true)
            {
                PlayTrack(allTracks[currentIndex]);
            }
            else
            {
                NextTrack_Click(null, null);
            }
        }

        // Изменение громкости
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = VolumeSlider.Value;
        }

        // Таймер обновляет прогресс трека каждую секунду
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TrackProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                CurrentTimeText.Text = mediaPlayer.Position.ToString("mm\\:ss");
            }
        }

        // Загрузка треков из базы данных при запуске
        private void LoadTracksFromDatabase()
        {
            allTracks.Clear();
            var tracksWithIncludes = _libraryContext.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.Genre)
                .ToList();

            allTracks.AddRange(tracksWithIncludes);
            filteredTracks.AddRange(tracksWithIncludes);
            RefreshTrackListView();
        }

        // Обработка перемещения слайдера воспроизведения
        private void TrackProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging)
            {
                CurrentTimeText.Text = TimeSpan.FromSeconds(e.NewValue).ToString(@"mm\:ss");
            }
        }

        private void TrackProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
        }

        private void TrackProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            if (TrackProgressSlider.Maximum > 0)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(TrackProgressSlider.Value);
            }
        }

        // Обработка поиска по полям трека
        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            String query = SearchBox.Text.ToLower();

            filteredTracks = allTracks.Where(track =>
                (!string.IsNullOrEmpty(track.Title) && track.Title.ToLower().Contains(query)) ||
                (!string.IsNullOrEmpty(track.Artist.Name) && track.Artist.Name.ToLower().Contains(query)) ||
                (!string.IsNullOrEmpty(track.Album.Name) && track.Album.Name.ToLower().Contains(query)) ||
                (!string.IsNullOrEmpty(track.Genre.Name) && track.Genre.Name.ToLower().Contains(query))
            ).ToList();

            RefreshTrackListView();
        }
    }

}