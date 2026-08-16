using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UnityEngine;

namespace UniTx.Serialization
{
    /// <summary>
    /// Caches <see cref="ISavedData"/> objects and persists them as JSON files.
    /// </summary>
    internal sealed class Serialiser
    {
        private const string TempExtension = ".tmp";
        private const string BackupExtension = ".bak";

        private readonly Dictionary<string, ISavedData> _cache = new();
        private readonly Dictionary<string, ISavedData> _dirty = new();
        private readonly List<ISavedData> _writeBuffer = new();

        /// <summary>
        /// Gets how many entries are waiting to be written.
        /// </summary>
        public int DirtyCount => _dirty.Count;

        private static string _saveDirectoryPath;

        /// <summary>
        /// Gets the directory save files live in.
        /// </summary>
        /// <remarks>
        /// Cached, because <see cref="Application.persistentDataPath"/> throws when read
        /// off the main thread and the write path runs on the thread pool. The value never
        /// changes during a session, so caching it costs nothing and makes the path
        /// thread-safe. <see cref="Prime"/> populates it from the main thread first.
        /// </remarks>
        public static string SaveDirectoryPath => _saveDirectoryPath ??= BuildSaveDirectoryPath();

        private static string BuildSaveDirectoryPath()
            => Path.Combine(Application.persistentDataPath, "Saves");

        /// <summary>
        /// Reads the save directory on the calling thread so later reads are thread-safe.
        /// </summary>
        internal static void Prime() => _saveDirectoryPath ??= BuildSaveDirectoryPath();

        /// <summary>
        /// Clears the in-memory cache and any pending writes.
        /// </summary>
        public void Reset()
        {
            _dirty.Clear();
            _cache.Clear();
        }

        /// <summary>
        /// Queues the given data for the next write batch.
        /// </summary>
        /// <param name="data">The data to persist.</param>
        public void MarkDirty(ISavedData data)
        {
            if (data?.Id == null) return;

            _cache[data.Id] = data;
            _dirty[data.Id] = data;
        }

        /// <summary>
        /// Writes every queued entry to disk, blocking until done.
        /// </summary>
        /// <returns>How many entries were written.</returns>
        /// <remarks>
        /// Blocking is correct for the shutdown paths — pause, quit and low memory — where
        /// the process may not survive long enough to finish an async write. Use
        /// <see cref="SerialiseDirtyAsync"/> for the periodic autosave, which must not stall
        /// a frame.
        /// </remarks>
        public int SerialiseDirty()
        {
            if (_dirty.Count == 0) return 0;

            var payloads = TakeDirtyPayloads();
            var written = 0;

            foreach (var payload in payloads)
            {
                if (WritePayload(payload, out var error))
                {
                    written++;
                }
                else
                {
                    UniStatics.LogError(error, this);
                    Requeue(payload.Id);
                }
            }

            return written;
        }

        /// <summary>
        /// Writes every queued entry, moving the file I/O off the main thread.
        /// </summary>
        /// <param name="cToken">Token to cancel the write.</param>
        /// <returns>How many entries were written.</returns>
        /// <remarks>
        /// Serialization stays on the main thread because <see cref="JsonUtility"/> touches
        /// Unity objects and is not thread-safe; only the file write is offloaded. That is
        /// the expensive half — a few KB to mobile flash routinely costs tens of
        /// milliseconds, which is a visible hitch if it lands mid-frame.
        /// </remarks>
        public async UniTask<int> SerialiseDirtyAsync(CancellationToken cToken = default)
        {
            if (_dirty.Count == 0) return 0;

            var payloads = TakeDirtyPayloads();
            var failed = new List<(string Id, string Error)>();
            var written = 0;

            try
            {
                await UniTask.RunOnThreadPool(() =>
                {
                    foreach (var payload in payloads)
                    {
                        if (WritePayload(payload, out var error)) written++;
                        else failed.Add((payload.Id, error));
                    }
                }, cancellationToken: cToken);
            }
            catch (OperationCanceledException)
            {
                // The queue was already drained before the await, so cancelling here would
                // otherwise discard those entries outright — they are no longer dirty and
                // were never written. Put them back so the next flush picks them up; losing
                // a player's progress to a cancelled autosave is not an acceptable failure.
                foreach (var payload in payloads)
                {
                    Requeue(payload.Id);
                }

                throw;
            }

            // Back on the main thread — the dirty map and Unity's logger are only ever
            // touched from here.
            foreach (var (id, error) in failed)
            {
                UniStatics.LogError(error, this);
                Requeue(id);
            }

            return written;
        }

        /// <summary>
        /// Loads the saved data with the given id from cache or disk, or creates a new one.
        /// </summary>
        /// <typeparam name="T">The concrete saved-data type.</typeparam>
        /// <param name="id">The unique identifier of the entry.</param>
        public T Deserialise<T>(string id)
            where T : ISavedData, new()
        {
            if (_cache.TryGetValue(id, out var cached))
            {
                return (T)cached;
            }

            var instance = new T();
            var path = FilePath(id);

            // Fall back to the .bak that File.Replace leaves behind, so a write interrupted
            // at the worst possible moment costs the last batch rather than the whole save.
            if (!TryOverwriteFrom(path, instance) && !TryOverwriteFrom(path + BackupExtension, instance))
            {
                // No usable save: `instance` keeps its constructed defaults, which is the
                // correct first-run state.
                UniStatics.LogInfo($"No existing save for '{id}'; starting fresh.", this);
            }

            // Assign explicitly rather than relying on the file to carry it: a fresh instance
            // has no id yet, and without one the first Save would be rejected.
            instance.Id = id;

            _cache[id] = instance;
            return instance;
        }

        /// <summary>
        /// Deletes the save file for the given id and drops it from the cache.
        /// </summary>
        /// <param name="id">The unique identifier of the entry.</param>
        public void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            _cache.Remove(id);
            _dirty.Remove(id);

            foreach (var path in new[] { FilePath(id), FilePath(id) + BackupExtension })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException ex)
                {
                    UniStatics.LogWarning($"Could not delete '{path}': {ex.Message}", this);
                }
            }
        }

        private static bool TryOverwriteFrom(string path, object target)
        {
            try
            {
                if (!File.Exists(path)) return false;

                var json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json)) return false;

                JsonUtility.FromJsonOverwrite(json, target);
                return true;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                UniStatics.LogWarning($"Save file '{path}' could not be read: {ex.Message}", null);
                return false;
            }
        }

        /// <summary>
        /// One entry's serialized form, ready to write without touching Unity objects.
        /// </summary>
        private readonly struct Payload
        {
            public readonly string Id;
            public readonly string Json;

            public Payload(string id, string json)
            {
                Id = id;
                Json = json;
            }
        }

        /// <summary>
        /// Drains the dirty set into serialized payloads. Main thread only.
        /// </summary>
        private List<Payload> TakeDirtyPayloads()
        {
            // Always called from the main thread, and always before any thread-pool work —
            // so this is the right place to resolve the save path while it is still legal
            // to read Application.persistentDataPath.
            Prime();

            _writeBuffer.Clear();
            _writeBuffer.AddRange(_dirty.Values);
            _dirty.Clear();

            var payloads = new List<Payload>(_writeBuffer.Count);

            foreach (var data in _writeBuffer)
            {
                // Stamped and serialized here, on the main thread: JsonUtility is not
                // thread-safe and reads Unity objects.
                data.ModifiedTimestamp = DateTime.UtcNow.ToUnixTimestamp();
                payloads.Add(new Payload(data.Id, JsonUtility.ToJson(data, true)));
            }

            return payloads;
        }

        /// <summary>
        /// Re-queues an entry whose write failed, so the next batch retries it.
        /// </summary>
        private void Requeue(string id)
        {
            if (id != null && _cache.TryGetValue(id, out var data)) _dirty[id] = data;
        }

        /// <summary>
        /// Writes one payload atomically. Safe to call from a background thread.
        /// </summary>
        /// <param name="payload">The serialized entry to write.</param>
        /// <param name="error">The failure reason, or null on success.</param>
        /// <returns><c>true</c> when the file was written.</returns>
        /// <remarks>
        /// Returns the error rather than logging it: this runs on the thread pool, and
        /// Unity's logging wants the main thread.
        /// </remarks>
        private static bool WritePayload(Payload payload, out string error)
        {
            error = null;

            var path = FilePath(payload.Id);
            var temp = path + TempExtension;
            var backup = path + BackupExtension;

            try
            {
                Directory.CreateDirectory(SaveDirectoryPath);

                File.WriteAllText(temp, payload.Json);

                // Write to a temp file, then swap. A direct overwrite that is interrupted —
                // the OS killing a backgrounded mobile app mid-write is the common case —
                // leaves a truncated file and the player loses their progress. File.Replace
                // is atomic and keeps the previous version as a fallback.
                if (File.Exists(path))
                {
                    File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"Failed to save '{payload.Id}': {ex.Message}";

                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch (IOException)
                {
                    // Best effort — a stale temp file is harmless, it is overwritten next time.
                }

                return false;
            }
        }

        private static string FilePath(string id) => Path.Combine(SaveDirectoryPath, $"{id}.json");
    }
}
