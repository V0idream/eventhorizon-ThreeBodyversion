using System;
using System.IO;
using Ionic.Zlib;
using UnityEngine;
using CommonComponents.Serialization;

namespace GameDatabase.Storage
{
    public class FileDatabaseStorage : IDataStorage
    {
        private const uint _header = 0xDA7ABA5E;
        private readonly string _filename;

        public string Name { get; private set; }
        public string Id { get; private set; }
        public Version Version { get; private set; }
        public bool IsEditable => false;

        public FileDatabaseStorage(string filename)
        {
            _filename = filename;

            using (var file = new FileStream(_filename, FileMode.Open, FileAccess.Read))
            {
                ReadDataTillContent(file);
            }
        }

        public void UpdateItem(string id, string content)
        {
            throw new InvalidOperationException("FileDatabaseStorage.UpdateItem is not supported");
        }

        public void LoadContent(IContentLoader loader)
        {
            using var file = new FileStream(_filename, FileMode.Open, FileAccess.Read);
            var content = ReadDataTillContent(file);

            while (true)
            {
                var type = content.ReadByte();
                if (type == -1) // end of file
                {
                    break;
                }
                else if (type == 1) // json
                {
                    var fileContent = content.ReadString();

                    try
                    {
                        loader.LoadJson(string.Empty, fileContent);
                    }
                    catch (Exception e)
                    {
                        // Compatibility is intentionally permissive. Keep
                        // loading the rest of the package when an entry uses a
                        // schema this build does not understand.
                        Debug.LogWarning(
                            $"Skipping an incompatible JSON entry in mod '{Name}': {e.Message}");
                    }
                }
                else if (type == 2) // image
                {
                    var key = content.ReadString();
                    var image = content.ReadByteArray();
                    loader.LoadImage(key, new LazyImageDataLoader(image));
                }
                else if (type == 3) // localization
                {
                    var key = content.ReadString();
                    var text = content.ReadString();
                    loader.LoadLocalization(key, text);
                }
                else if (type == 4) // wav audioClip
                {
                    var key = content.ReadString();
                    var audioClip = content.ReadByteArray();
                    loader.LoadAudioClip(key, new LazyAudioDataLoader(audioClip, LazyAudioDataLoader.Format.Wav));
                }
                else if (type == 5) // ogg audioClip
                {
                    var key = content.ReadString();
                    var audioClip = content.ReadByteArray();
                    loader.LoadAudioClip(key, new LazyAudioDataLoader(audioClip, LazyAudioDataLoader.Format.Ogg));
                }
            }

        }

        private Stream ReadDataTillContent(FileStream file)
        {
            var obsolete = !TryReadHeader(file);
            var content = UnpackContent(file);

            if (obsolete)
                LoadHeaderDataObsolete(content);
            else
                LoadHeaderData(content);

            return content;
        }

        private static bool TryReadHeader(FileStream file)
        {
            var position = file.Position;
            var header = file.ReadUInt32();
            if (header != _header)
            {
                file.Seek(position, SeekOrigin.Begin);
                return false;
            }

            return true;
        }

        private static Stream UnpackContent(FileStream file)
        {
            var encryptedStream = new Security.EncryptedReadStream(file, (int)(file.Length - file.Position));
            var zlibStream = new ZlibStream(encryptedStream, CompressionMode.Decompress);
            return zlibStream;
        }

        private void LoadHeaderData(Stream stream)
        {
            var formatId = stream.ReadInt32();
            Name = stream.ReadString();
            Id = stream.ReadString();

            var major = stream.ReadInt32();
            var minor = stream.ReadInt32();

            Version = new Version(major, minor);
        }

        private void LoadHeaderDataObsolete(Stream stream)
        {
            Name = stream.ReadString();
            Id = stream.ReadString();

            Version = new Version(1, 0);
        }
    }

    public class PermissiveFileDatabaseStorage : IDataStorage
    {
        private readonly string _filename;

        public PermissiveFileDatabaseStorage(string filename)
        {
            _filename = filename;
            Name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(Name))
                Name = Path.GetFileName(filename);

            Id = CreateStableId(filename);
            Version = new Version(1, 7);
        }

        public string Name { get; }
        public string Id { get; }
        public Version Version { get; }
        public bool IsEditable => false;

        public void UpdateItem(string id, string content)
        {
            throw new InvalidOperationException("PermissiveFileDatabaseStorage.UpdateItem is not supported");
        }

        public void LoadContent(IContentLoader loader)
        {
            Debug.LogWarning(
                $"External mod file '{_filename}' could not be decoded as a compiled Event Horizon mod. " +
                "It is being treated as an empty mod so compatibility checks cannot block startup.");
        }

        private static string CreateStableId(string filename)
        {
            var name = Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrEmpty(name))
                name = Path.GetFileName(filename);

            if (string.IsNullOrEmpty(name))
                name = "external_mod";

            foreach (var character in Path.GetInvalidFileNameChars())
                name = name.Replace(character, '_');

            return "forced_" + name;
        }
    }

    public class LazyImageDataLoader : Model.IImageData
    {
        private byte[] _rawData;
        private Model.ImageData _imageData;

        public Sprite Sprite
        {
            get
            {
                if (_imageData == null)
                {
                    _imageData = new(_rawData);
                    _rawData = null;
                }

                return _imageData.Sprite;
            }
        }

        public LazyImageDataLoader(byte[] rawData)
        {
            _rawData = rawData;
        }
    }

    public class LazyAudioDataLoader : Model.IAudioClipData
    {
        public enum Format
        {
            Wav,
            Ogg,
        }

        private readonly Format _format;
        private byte[] _rawData;
        private Model.IAudioClipData _audioClipData;

        public AudioClip AudioClip
        {
            get
            {
                if (_audioClipData == null)
                {
                    switch (_format)
                    {
                        case Format.Wav:
                            _audioClipData = new Model.AudioClipData(_rawData);
                            break;
                        case Format.Ogg:
                            _audioClipData = Model.OggAudioClip.Create(_rawData);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    _rawData = null;
                }

                return _audioClipData.AudioClip;
            }
        }

        public LazyAudioDataLoader(byte[] rawData, Format format)
        {
            _rawData = rawData;
            _format = format;
        }
    }
}
