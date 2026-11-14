using System;

namespace Astra.Core.Progress
{
    /// <summary>
    /// Saves progress as one JSON string through caller-supplied read/write functions, e.g. a single PlayerPrefs key.
    /// Use it where files are not kept between sessions, such as the WebGL build, whose files only reach the browser's
    /// storage after an explicit sync. Core never references Unity: the caller passes the functions, for example
    /// <c>() =&gt; PlayerPrefs.GetString(key, "")</c> and <c>json =&gt; { PlayerPrefs.SetString(key, json); PlayerPrefs.Save(); }</c>.
    /// <para>Load never throws (CONTRACT §6): nothing saved → fresh state; unreadable → fresh state, and the reason is
    /// in <see cref="LastLoadWarning"/>. The unreadable value is left in place until the next save replaces it.</para>
    /// </summary>
    public sealed class KeyValueProgressStore : IProgressStore
    {
        private readonly Func<string> _read;
        private readonly Action<string> _write;
        private readonly Func<ProgressState, string> _toJson;
        private readonly Func<string, ProgressState> _fromJson;

        public KeyValueProgressStore(Func<string> read, Action<string> write,
            Func<ProgressState, string> toJson, Func<string, ProgressState> fromJson)
        {
            _read = read ?? throw new ArgumentNullException(nameof(read));
            _write = write ?? throw new ArgumentNullException(nameof(write));
            _toJson = toJson ?? throw new ArgumentNullException(nameof(toJson));
            _fromJson = fromJson ?? throw new ArgumentNullException(nameof(fromJson));
        }

        /// <summary>Why the last Load() returned a fresh state instead of the saved one; null if it loaded normally or nothing was saved.</summary>
        public string LastLoadWarning { get; private set; }

        public ProgressState Load()
        {
            LastLoadWarning = null;
            string json;
            try
            {
                json = _read();
            }
            catch (Exception ex)
            {
                LastLoadWarning = $"progress could not be read ({ex.GetType().Name}: {ex.Message}); progress starts fresh";
                return new ProgressState();
            }

            if (string.IsNullOrWhiteSpace(json))
                return new ProgressState();

            ProgressState state = null;
            try
            {
                state = _fromJson(json);
            }
            catch (Exception)
            {
                // handled below as unreadable
            }
            if (state == null)
            {
                LastLoadWarning = "saved progress could not be read; progress starts fresh";
                return new ProgressState();
            }
            return ProgressRules.Sanitize(state);
        }

        /// <exception cref="ArgumentNullException">state is null.</exception>
        public void Save(ProgressState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var json = _toJson(state);
            if (json == null)
                throw new InvalidOperationException("toJson returned null");
            _write(json);
        }
    }
}
