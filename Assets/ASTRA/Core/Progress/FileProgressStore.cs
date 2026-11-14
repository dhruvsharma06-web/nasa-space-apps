using System;
using System.IO;
using System.Text;

namespace Astra.Core.Progress
{
    /// <summary>
    /// Saves progress to &lt;directory&gt;/progress.json so a crash or a broken file never loses or blocks it
    /// (CONTRACT.md §6, AT-06). JSON conversion is passed in (e.g. JsonUtility.ToJson / FromJson&lt;ProgressState&gt;),
    /// so Core never references Unity. This is the only Core class that touches the file system.
    /// <para>Save: write progress.json.tmp and flush it to disk → delete progress.json → move the tmp file into place.</para>
    /// <para>Load never throws. Missing file → fresh state (only level 0 unlocked). Unreadable file → renamed to
    /// progress.json.bak, fresh state. progress.json missing but progress.json.tmp present (a save was cut off
    /// between the delete and the move) → the tmp file is loaded.</para>
    /// </summary>
    public sealed class FileProgressStore : IProgressStore
    {
        public const string FileName = "progress.json";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _directory;
        private readonly string _path;
        private readonly string _tmpPath;
        private readonly string _bakPath;
        private readonly Func<ProgressState, string> _toJson;
        private readonly Func<string, ProgressState> _fromJson;

        public FileProgressStore(string directory, Func<ProgressState, string> toJson, Func<string, ProgressState> fromJson)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("directory is required", nameof(directory));
            _toJson = toJson ?? throw new ArgumentNullException(nameof(toJson));
            _fromJson = fromJson ?? throw new ArgumentNullException(nameof(fromJson));

            _directory = directory;
            _path = Path.Combine(directory, FileName);
            _tmpPath = _path + ".tmp";
            _bakPath = _path + ".bak";
        }

        /// <summary>Full path of progress.json.</summary>
        public string FilePath => _path;

        /// <summary>
        /// Why the last Load() did not simply read progress.json (corrupt file backed up, recovered from the tmp
        /// file, or a read error), for the caller to log. Null when it loaded normally or nothing was saved yet.
        /// </summary>
        public string LastLoadWarning { get; private set; }

        public ProgressState Load()
        {
            LastLoadWarning = null;
            try
            {
                if (File.Exists(_path))
                {
                    if (TryParse(File.ReadAllText(_path, Utf8NoBom), out var saved))
                        return saved;

                    if (File.Exists(_bakPath))
                        File.Delete(_bakPath);
                    File.Move(_path, _bakPath);
                    LastLoadWarning = $"{FileName} could not be read; it was moved to {FileName}.bak and progress starts fresh";
                    return new ProgressState();
                }

                if (File.Exists(_tmpPath))
                {
                    if (TryParse(File.ReadAllText(_tmpPath, Utf8NoBom), out var recovered))
                    {
                        LastLoadWarning = $"a save was interrupted; progress was recovered from {FileName}.tmp";
                        return recovered;
                    }
                    LastLoadWarning = $"an interrupted first save left an unreadable {FileName}.tmp; progress starts fresh";
                    return new ProgressState();
                }

                return new ProgressState();
            }
            catch (Exception ex)
            {
                // Load must never throw (CONTRACT.md §6). The reason is kept for the caller to log.
                LastLoadWarning = $"progress could not be read ({ex.GetType().Name}: {ex.Message}); progress starts fresh";
                return new ProgressState();
            }
        }

        /// <exception cref="ArgumentNullException">state is null.</exception>
        /// <exception cref="IOException">The file system refused the write; the previous save is untouched or recoverable.</exception>
        public void Save(ProgressState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var json = _toJson(state);
            if (json == null)
                throw new InvalidOperationException("toJson returned null");

            Directory.CreateDirectory(_directory);

            // 1. Write the new state to the tmp file and force it onto the disk.
            using (var stream = new FileStream(_tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Utf8NoBom.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            // 2. Remove the old file, 3. move the new one into place. A crash between 2 and 3 leaves only the tmp
            //    file, which Load picks up.
            File.Delete(_path);
            File.Move(_tmpPath, _path);
        }

        private bool TryParse(string json, out ProgressState state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                state = _fromJson(json);
            }
            catch (Exception)
            {
                return false; // Unreadable JSON is the "corrupt file" case; Load reports it via LastLoadWarning.
            }
            if (state == null)
                return false;
            ProgressRules.Sanitize(state);
            return true;
        }
    }
}
