using GodlessPlayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Windows;

namespace GodlessPlayer
{
    public partial class EditTrackWindow : Window
    {
        private Track _track;

        public EditTrackWindow(Track track)
        {
            InitializeComponent();
            _track = track;

            TitleBox.Text = _track.Title;
            ArtistBox.Text = _track.Artist?.Name ?? string.Empty;
            AlbumBox.Text = _track.Album?.Name ?? string.Empty;
            GenreBox.Text = _track.Genre?.Name ?? string.Empty;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            using (var db = new LibraryContext())
            {
                var track = db.Tracks
                    .Include(t => t.Genre)
                    .Include(t => t.Artist)
                    .Include(t => t.Album)
                    .FirstOrDefault(t => t.Id == _track.Id);

                if (track != null)
                {
                    track.Title = TitleBox.Text;

                    var genreName = GenreBox.Text.Trim();
                    if (!string.IsNullOrEmpty(genreName))
                    {
                        var genre = db.Genres.FirstOrDefault(a => a.Name == genreName)
                                     ?? new Genre { Name = genreName };
                        track.Genre = genre;
                    }

                    var artistName = ArtistBox.Text.Trim();
                    if (!string.IsNullOrEmpty(artistName))
                    {
                        var artist = db.Artists.FirstOrDefault(a => a.Name == artistName)
                                     ?? new Artist { Name = artistName };
                        track.Artist = artist;
                    }

                    var albumTitle = AlbumBox.Text.Trim();
                    if (!string.IsNullOrEmpty(albumTitle))
                    {
                        var album = db.Albums.FirstOrDefault(a => a.Name == albumTitle)
                                    ?? new Album { Name = albumTitle };
                        track.Album = album;
                    }

                    db.SaveChanges();
                }
            }

            _track.Title = TitleBox.Text;
            _track.Genre = new Genre { Name = GenreBox.Text.Trim() };
            _track.Artist = new Artist { Name = ArtistBox.Text.Trim() };
            _track.Album = new Album { Name = AlbumBox.Text.Trim() };

            this.DialogResult = true;
            this.Close();
        }
    }
}
