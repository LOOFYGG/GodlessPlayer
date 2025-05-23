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

namespace GodlessPlayer
{
    public partial class MainWindow : Window
    {
        private readonly LibraryContext _libraryContext = new();
        private  List<Track> allTracks = new();
        private List<Track> filteredTracks = new();
        private MediaPlayer mediaPlayer = new();
        private int currentIndex = -1;
        private bool isPlaying = false;
        private DispatcherTimer timer = new();
        private bool isDragging = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadTracksFromDatabase();
            RefreshTrackListView();
            TrackListView.ItemsSource = allTracks;
            TrackListView.ItemsSource = filteredTracks;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.Volume = VolumeSlider.Value;
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        private void PlayTrack(Track track)
        {
            if (track == null) return;

            mediaPlayer.Open(new Uri(track.Path));
            mediaPlayer.Play();
            isPlaying = true;

            NowPlayingText.Text = $"Сейчас играет: {track.Title} — {track.Artist?.Name ?? "Неизвестен"}";

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

                    string defaultArtistName = "Неизвестен";
                    string defaultAlbumName = "Без альбома";
                    string defaultGenreName = "Какой?";

                    var artist = _libraryContext.Artists.FirstOrDefault(a => a.Name == defaultArtistName)
                                 ?? new Artist { Name = defaultArtistName };

                    var album = _libraryContext.Albums.FirstOrDefault(a => a.Name == defaultAlbumName)
                                ?? new Album { Name = defaultAlbumName };

                    var genre = _libraryContext.Genres.FirstOrDefault(g => g.Name == defaultGenreName)
                                 ?? new Genre { Name = defaultGenreName };

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

        private void EditTrack_Click(object sender, RoutedEventArgs e)
        {
            if (TrackListView.SelectedItem is Track selected)
            {
                var dialog = new EditTrackWindow(selected);
                if (dialog.ShowDialog() == true)
                {
                    RefreshTrackListView();
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите трек для редактирования.", "Редактирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshTrackListView()
        {
            if (TrackListView == null) return;
            TrackListView.ItemsSource = allTracks;
            TrackListView.ItemsSource = filteredTracks;
            TrackListView.Items.Refresh();
        }

        private void TrackListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
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

        private void PreviousTrack_Click(object sender, RoutedEventArgs e)
        {
            if (allTracks.Count == 0) return;

            currentIndex = (currentIndex - 1 + allTracks.Count) % allTracks.Count;
            PlayTrack(allTracks[currentIndex]);
        }

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

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = VolumeSlider.Value;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!isDragging && mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TrackProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                CurrentTimeText.Text = mediaPlayer.Position.ToString("mm\\:ss");
            }
        }

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