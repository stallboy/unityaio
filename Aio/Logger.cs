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
        public delegate void LoggerExceptionHandler(string msg, Exception e);

        public delegate void UdpQueueOverflowHandler(string rolename, String msg);

        public bool EnableUdpLogger { get; set; }
        public LoggerExceptionHandler OnException { get; set; }
        public UdpQueueOverflowHandler OnUdpQueueOverflow { get; set; }

        private readonly UdpClient _sender;
        private readonly IPEndPoint _remoteEp;
        private readonly ConcurrentQueue<IoAction> _actions = new ConcurrentQueue<IoAction>();
        private readonly Stopwatch _frameWatcher = new Stopwatch();
        private readonly List<string> _secondChanceList = new List<string>();
 
        private int _maxUdpQueueCount = 512;
        private int _secondChanceCapacity = 512;
        
        public Logger(String remoteIp, int remotePort, bool enableUdpLogger)
        {
            _sender = new UdpClient();
            _remoteEp = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            EnableUdpLogger = enableUdpLogger;
        }

        public void Process(long maxMilliseconds)
        {
            _frameWatcher.Reset();
            _frameWatcher.Start();
            while (_frameWatcher.ElapsedMilliseconds < maxMilliseconds)
            {
                IoAction action;
                if (_actions.TryDequeue(out action))
                {
                    action();
                }
                else
                {
                    break;
                }
            }
        }

        public void Log(string rolename, string s)
        {
            LogError(rolename, s, null);
        }
        
        public void LogError(string rolename, string s, Exception e)
        {
            if (!EnableUdpLogger)
            {
                return;
            }
            var sb = new StringBuilder();
            if (s != null)
            {
                sb.Append(s);
            }
            if (e != null)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" ");
                }
                sb.Append(e);
            }
            DoLog(rolename, sb.ToString());
        }

        
        public void Close() 
        {
            _sender.Close();
        }



        private void DoLog(string rolename, string msg)
        {
            if (EnableUdpLogger)
            {
                if (_actions.Count > _maxUdpQueueCount)
                {
                    if (OnUdpQueueOverflow != null)
                    {
                        OnUdpQueueOverflow(rolename, msg);
                    }
                }
                else
                {
                    _actions.Enqueue(() => DoSend(rolename + "@" + msg + "@" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true));
                }
            }
        }

        private void DoSend(string str, bool firstTime)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(str);
                _sender.Connect(_remoteEp);
                _sender.BeginSend(bytes, bytes.Length, ar =>
                {
                    try
                    {
                        _sender.EndSend(ar);

                        foreach (var s in _secondChanceList)
                        {
                            var second = s;
                             _actions.Enqueue(() => DoSend(second, false));
                        }
                        _secondChanceList.Clear();
                    }
                    catch (Exception e)
                    {
                        GiveSecondChance(str, e, firstTime);
                    }
                }, null);
            }
            catch (Exception e)
            {
                GiveSecondChance(str, e, firstTime);
            }
        }

        private void GiveSecondChance(string str, Exception cause, bool firstTime)
        {
            if (firstTime)
            {
                _secondChanceList.Add(str);
                if (_secondChanceList.Count > _secondChanceCapacity)
                {
                    _secondChanceList.RemoveAt(0);
                }
            }
            else
            {
                if (OnException != null)
                {
                    OnException(str, cause);
                }
            }
        }


    }

}
