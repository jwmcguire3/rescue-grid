using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Rescue.Unity.Audio.Tests
{
    public sealed class MusicPlaylistTests
    {
        private readonly System.Collections.Generic.List<UnityObject> cleanup = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < cleanup.Count; i++)
            {
                if (cleanup[i] is not null)
                {
                    UnityObject.DestroyImmediate(cleanup[i]);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void TryGetNextTrack_ReturnsValidRandomTrack()
        {
            MusicPlaylist playlist = CreatePlaylist(
                new MusicTrackEntry(CreateClip("a")),
                new MusicTrackEntry(CreateClip("b")));

            Assert.That(
                playlist.TryGetNextTrack(-1, 0, (_, _) => 1, out MusicTrackSelection selection),
                Is.True);

            Assert.That(selection.Clip, Is.Not.Null);
            Assert.That(selection.TrackIndex, Is.EqualTo(1));
            Assert.That(selection.Volume, Is.EqualTo(1f));
        }

        [Test]
        public void TryGetNextTrack_AvoidsImmediateRepeatWhenMultipleTracksExist()
        {
            MusicPlaylist playlist = CreatePlaylist(
                new MusicTrackEntry(CreateClip("a")),
                new MusicTrackEntry(CreateClip("b")));

            Assert.That(
                playlist.TryGetNextTrack(0, 0, (_, _) => 0, out MusicTrackSelection selection),
                Is.True);

            Assert.That(selection.TrackIndex, Is.EqualTo(1));
        }

        [Test]
        public void TryGetNextTrack_EmptyPlaylistFailsSoft()
        {
            MusicPlaylist playlist = CreatePlaylist();

            Assert.That(
                playlist.TryGetNextTrack(-1, 0, (_, _) => 0, out MusicTrackSelection selection),
                Is.False);
            Assert.That(selection.Clip, Is.Null);
        }

        [Test]
        public void TryGetNextTrack_SingleTrackPlaylistCanRepeat()
        {
            AudioClip clip = CreateClip("solo");
            MusicPlaylist playlist = CreatePlaylist(new MusicTrackEntry(clip));

            Assert.That(
                playlist.TryGetNextTrack(0, 0, (_, _) => 0, out MusicTrackSelection selection),
                Is.True);

            Assert.That(selection.TrackIndex, Is.EqualTo(0));
            Assert.That(selection.Clip, Is.SameAs(clip));
        }

        [Test]
        public void TryGetNextTrack_ResolvesPerTrackAndDefaultVolumeSafely()
        {
            MusicPlaylist playlist = CreatePlaylist(
                new MusicTrackEntry(CreateClip("default-volume")),
                new MusicTrackEntry(CreateClip("track-volume"), 0.25f, overrideDefaultVolume: true));
            playlist.ConfigureForTests(
                new[]
                {
                    playlist.Tracks[0],
                    playlist.Tracks[1],
                },
                newDefaultVolume: 0.6f);

            Assert.That(
                playlist.TryGetNextTrack(-1, 0, (_, _) => 0, out MusicTrackSelection defaultVolumeSelection),
                Is.True);
            Assert.That(
                playlist.TryGetNextTrack(-1, 0, (_, _) => 1, out MusicTrackSelection trackVolumeSelection),
                Is.True);

            Assert.That(defaultVolumeSelection.Volume, Is.EqualTo(0.6f));
            Assert.That(trackVolumeSelection.Volume, Is.EqualTo(0.25f));
        }

        private MusicPlaylist CreatePlaylist(params MusicTrackEntry?[] entries)
        {
            MusicPlaylist playlist = ScriptableObject.CreateInstance<MusicPlaylist>();
            playlist.ConfigureForTests(entries);
            cleanup.Add(playlist);
            return playlist;
        }

        private AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 32, 1, 8000, stream: false);
            cleanup.Add(clip);
            return clip;
        }
    }
}
