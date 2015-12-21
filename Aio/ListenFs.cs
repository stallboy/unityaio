using System.Diagnostics;
using System.IO;

namespace Aio
{
    public class ListenFs
    {
        public delegate void FsChangedHandler();

        private readonly int _delay;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly FileSystemWatcher _watcher;
        private bool _changed;
        private bool _inChangeDelay;
        public FsChangedHandler ChangedHander;

        public ListenFs(string path, string filter, int delayMillis)
        {
            _watcher = new FileSystemWatcher(path, filter);
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _delay = delayMillis;
        }

        public void Update()
        {
            if (ChangedHander == null)
                return;

            bool changed;
            lock (this)
            {
                changed = _changed;
                _changed = false;
            }

            if (_delay == 0)
            {
                if (changed)
                    ChangedHander();
            }
            else if (changed)
            {
                _inChangeDelay = true;
                _stopwatch.Reset();
                _stopwatch.Start();
            }
            else if (_inChangeDelay && _stopwatch.ElapsedMilliseconds >= _delay)
            {
                ChangedHander();
                _inChangeDelay = false;
            }
        }

        public void Watch()
        {
            _watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (this)
            {
                _changed = true;
            }
        }
    }
}