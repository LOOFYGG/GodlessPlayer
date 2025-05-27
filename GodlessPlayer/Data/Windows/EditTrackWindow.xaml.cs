using GodlessPlayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Windows;

namespace GodlessPlayer
{
    // Класс EditTrackWindow представляет окно для редактирования информации о треке.
    public partial class EditTrackWindow : Window
    {
        // Приватное поле для хранения редактируемого трека
        private Track _track;

        // Конструктор окна принимает объект Track и заполняет поля ввода соответствующими значениями
        public EditTrackWindow(Track track)
        {
            InitializeComponent(); // Инициализация компонентов окна
            _track = track;

            // Заполнение текстовых полей текущими значениями трека
            TitleBox.Text = _track.Title;
            ArtistBox.Text = _track.Artist?.Name ?? string.Empty; // если Artist == null, используем пустую строку
            AlbumBox.Text = _track.Album?.Name ?? string.Empty;
            GenreBox.Text = _track.Genre?.Name ?? string.Empty;
        }

        // Обработчик события нажатия кнопки "Сохранить"
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Используем контекст базы данных
            using (var db = new LibraryContext())
            {
                // Загружаем трек из базы данных с его связями (жанр, исполнитель, альбом)
                var track = db.Tracks
                    .Include(t => t.Genre)
                    .Include(t => t.Artist)
                    .Include(t => t.Album)
                    .FirstOrDefault(t => t.Id == _track.Id); // ищем по ID

                if (track != null)
                {
                    // Обновляем название трека
                    track.Title = TitleBox.Text;

                    // Обновляем жанр
                    var genreName = GenreBox.Text.Trim();
                    if (!string.IsNullOrEmpty(genreName))
                    {
                        // Пытаемся найти жанр в базе, если нет — создаем новый
                        var genre = db.Genres.FirstOrDefault(a => a.Name == genreName)
                                     ?? new Genre { Name = genreName };
                        track.Genre = genre;
                    }

                    // Обновляем исполнителя
                    var artistName = ArtistBox.Text.Trim();
                    if (!string.IsNullOrEmpty(artistName))
                    {
                        var artist = db.Artists.FirstOrDefault(a => a.Name == artistName)
                                     ?? new Artist { Name = artistName };
                        track.Artist = artist;
                    }

                    // Обновляем альбом
                    var albumTitle = AlbumBox.Text.Trim();
                    if (!string.IsNullOrEmpty(albumTitle))
                    {
                        var album = db.Albums.FirstOrDefault(a => a.Name == albumTitle)
                                    ?? new Album { Name = albumTitle };
                        track.Album = album;
                    }

                    // Сохраняем изменения в базу данных
                    db.SaveChanges();
                }
            }

            // Обновляем локальный объект _track (это может быть нужно, если окно вызывается извне и объект еще будет использоваться)
            _track.Title = TitleBox.Text;
            _track.Genre = new Genre { Name = GenreBox.Text.Trim() };
            _track.Artist = new Artist { Name = ArtistBox.Text.Trim() };
            _track.Album = new Album { Name = AlbumBox.Text.Trim() };

            // Устанавливаем результат диалога как успешный и закрываем окно
            this.DialogResult = true;
            this.Close();
        }
    }
}
