using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Aio
{
    public sealed class Logger
    {
        public delegate void ErrorHandler(string message, Exception e = null);

        public ErrorHandler OnError { get; set; }
        public bool Enable { get; set; }

        private readonly UdpClient _sender;
        private readonly IPEndPoint _remoteEp;
        private readonly ConcurrentQueue<IoAction> _ioactions = new ConcurrentQueue<IoAction>();
        private readonly Stopwatch _frameWatcher = new Stopwatch();
        private readonly Queue<string> _retrys = new Queue<string>(RetryCapacity);

        private const int ActionCapacity = 256;
        private const int RetryCapacity = 128;

        private string _identification = "null";

        public Logger(string remoteIp, int remotePort, bool enable)
        {
            _sender = new UdpClient();
            _remoteEp = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            Enable = enable;
        }

        public void Process(long maxMilliseconds)
        {
            _frameWatcher.Reset();
            _frameWatcher.Start();
            while (_frameWatcher.ElapsedMilliseconds < maxMilliseconds)
            {
                IoAction action;
                if (_ioactions.TryDequeue(out action))
                {
                    action();
                }
                else
                {
                    break;
                }
            }
        }

        public void SetIdentification(string identification)
        {
            _identification = identification == null ? "null" : identification;
        }

        public void Log(string message, Exception exception = null)
        {
            if (!Enable)
                return;

            var sb = new StringBuilder();
            sb.Append(_identification).Append("@").Append(DateTime.Now.ToString("HH:mm:ss"))
                .Append("@").Append(message).Append("@").Append(exception);
            var msg = sb.ToString();

            Enqueue(msg, true);
        }

        public void Close()
        {
            _sender.Close();
        }

        private void Enqueue(string msg, bool firstTry)
        {
            if (_ioactions.Count < ActionCapacity)
            {
                _ioactions.Enqueue(() => DoSend(msg, firstTry));
            }
            else if (OnError != null)
            {
                OnError(msg);
            }
        }

        private void DoSend(string msg, bool firstTry)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(msg);
                _sender.BeginSend(bytes, bytes.Length, _remoteEp, ar => _ioactions.Enqueue(() =>
                {
                    try
                    {
                        _sender.EndSend(ar);
                        
                        while (_ioactions.Count < ActionCapacity)
                        {
                            var re = _retrys.Dequeue();
                            _ioactions.Enqueue(() => DoSend(re, false));
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }), null);
            }
            catch (Exception e)
            {
                if (firstTry)
                {
                    if (_retrys.Count >= RetryCapacity)
                    {
                        var firstRetryMsg = _retrys.Dequeue();
                        if (OnError != null)
                        {
                            OnError(firstRetryMsg);
                        }
                    }
                    _retrys.Enqueue(msg);
                }
                else if (OnError != null)
                {
                    OnError(msg, e);
                }
            }
        }
    }
}