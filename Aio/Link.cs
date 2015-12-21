using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;

namespace Aio
{
    public sealed class Link
    {
        public delegate void NetExceptionHandler(string detail);

        public delegate void RecvProtocolHandler(int type, byte[] data);

        public delegate void ConnectedHandler();
        
        private const int InputSize = 4096;
        private const int ReserveInputBufSize = 8192;
        private const int ReserveOutputBufSize = 1024;

        private readonly Octets _inputBuf = new Octets(ReserveInputBufSize);
        private readonly Octets _outputBuf = new Octets(ReserveOutputBufSize);
        private readonly byte[] _input = new byte[InputSize];
        private ISecurity _inputSecurity = NullSecurity.Instance;
        private ISecurity _outputSecurity = NullSecurity.Instance;

        private readonly ConcurrentQueue<IoAction> _actions = new ConcurrentQueue<IoAction>();
        private readonly Queue<Protocol> _protocols = new Queue<Protocol>();


        private Socket _socket;
        private bool _startReconnect;
        private readonly Stopwatch _reconnectWatcher = new Stopwatch();
        private int _reconnectDelay;
        private int _reconnectDelayMin = 1000;
        private int _reconnectDelayMax = 60000;
        private bool _autoReconnect;

        private readonly Stopwatch _frameWatcher = new Stopwatch();

        public string Host { get; private set; }

        public int Port { get; private set; }

        public int ReceiveBufferSize { get; private set; }

        public int SendBufferSize { get; private set; }

        public int OutputBufferSize { get; private set; }


        public ConnectedHandler OnConnected;

        public NetExceptionHandler OnNetException;

        public RecvProtocolHandler OnRecvProtocol;


        public Link(string host, int port, int receiveBufferSize, int sendBufferSize, int outputBufferSize)
        {
            Host = host;
            Port = port;
            ReceiveBufferSize = receiveBufferSize;
            SendBufferSize = sendBufferSize;
            OutputBufferSize = outputBufferSize;
        }

        public bool Connected
        {
            get { return null != _socket && _socket.Connected; }
        }

        public bool AutoReconnect
        {
            get { return _autoReconnect; }
            set
            {
                _autoReconnect = value;
                if (_autoReconnect)
                {
                    if (!Connected)
                    {
                        _reconnectDelay = 0;
                        _startReconnect = true;
                        _reconnectWatcher.Reset();
                        _reconnectWatcher.Start();
                    }
                }
                else
                {
                    _startReconnect = false;
                }
            }
        }

        public int ReconnectDelayMin
        {
            get { return _reconnectDelayMin; }
            set
            {
                if (value > 0)
                    _reconnectDelayMin = value;
            }
        }


        public int ReconnectDelayMax
        {
            get { return _reconnectDelayMax; }
            set
            {
                if (value > 0)
                    _reconnectDelayMax = value;
            }
        }

        public byte[] OutputSecurity
        {
            set
            {
                _outputSecurity = new Arc4Security { Parameter = new Octets(value) };
            }
        }

        public byte[] InputSecurity
        {
            set
            {
                _inputSecurity = new DecompressArc4Security { Parameter = new Octets(value) };
            }
        }

        public void Close()
        {
            while (_protocols.Count > 0)
            {
                var p = _protocols.Dequeue();
                if (OnRecvProtocol != null)
                    OnRecvProtocol(p.Type, p.Data);
            }
            
            _protocols.Clear();
            
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;                
            }

            _actions.Clear();
            
            _startReconnect = false;

            _inputBuf.Clear();
            _outputBuf.Clear();
            _inputSecurity = NullSecurity.Instance;
            _outputSecurity = NullSecurity.Instance;
        }


        private void Close(Socket sock, Exception e)
        {
            if (sock != _socket) //socket.close后仍然有可能会有系统回调向_actions里塞。这时我们要Close的时候对比一下。
            {
                return;
            }

            if (OnNetException != null)
                OnNetException(e.Message);

            Close();

            if (_autoReconnect)
            {
                if (_reconnectDelay == 0)
                {
                    _reconnectDelay = _reconnectDelayMin;
                }
                else
                {
                    _reconnectDelay *= 2;
                    if (_reconnectDelay > _reconnectDelayMax)
                        _reconnectDelay = _reconnectDelayMax;
                }
                _startReconnect = true;
                _reconnectWatcher.Reset();
                _reconnectWatcher.Start();
            }
        }


        public void Connect()
        {
            if (_socket != null)
                return;
            
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    SendBufferSize = SendBufferSize,
                    ReceiveBufferSize = ReceiveBufferSize
                };

                Socket sock = _socket;
                sock.BeginConnect(Host, Port, ar => _actions.Enqueue(() =>
                {
                    try
                    {
                        sock.EndConnect(ar);
                        _inputBuf.Clear();
                        _outputBuf.Clear();
                        if (OnConnected != null)
                            OnConnected();
                        BeginReceive();
                        _reconnectDelay = 0;
                        _startReconnect = false;
                    }
                    catch (Exception e)
                    {
                        Close(sock, e);
                    }
                }), _socket);
            }
            catch (Exception e)
            {
                Close(_socket, e);
            }
        }

        private void BeginReceive()
        {
            Socket sock = _socket; //closure 的问题，需要这句
            try
            {
                sock.BeginReceive(_input, 0, InputSize, SocketFlags.None, ar =>
                {
                    _actions.Enqueue(() =>
                    {
                        try
                        {
                            int received = sock.EndReceive(ar);
                            if (received > 0)
                            {
                                _inputBuf.Append(_inputSecurity.Update(Octets.Wrap(_input, received)));
         
                                var os = OctetsStream.Wrap(_inputBuf);
                                while (os.Remaining > 0)
                                {
                                    int tranpos = os.Begin();
                                    try
                                    {
                                        int type = os.UnmarshalSize();
                                        int size = os.UnmarshalSize();
                                        if (size > os.Remaining)
                                        {
                                            os.Rollback(tranpos);
                                            break; // not enough
                                        }
                                        _protocols.Enqueue(new Protocol(type, os.UnmarshalFixedSizeBytes(size)));
                                    }
                                    catch (MarshalException)
                                    {
                                        os.Rollback(tranpos);
                                        break;
                                    }
                                }

                                if (os.Position != 0)
                                {
                                    _inputBuf.EraseAndCompact(os.Position, ReserveInputBufSize);
                                }
                                BeginReceive();
                            }
                            else
                            {
                                Close(sock, new Exception("the socket channel has reached end-of-stream"));
                            }
                        }
                        catch (Exception e)
                        {
                            Close(sock, e);
                        }
                    });
                   
                    
                },null);
            }
            catch (Exception e)
            {
                Close(sock, e);
            }
        }

        private void BeginSend()
        {
            Socket sock = _socket;
            try
            {
                sock.BeginSend(_outputBuf.ByteArray, 0, _outputBuf.Count, SocketFlags.None, ar =>
                {
                    _actions.Enqueue(() =>
                    {
                        try
                        {
                            var sent = sock.EndSend(ar);
                            _outputBuf.EraseAndCompact(sent, ReserveOutputBufSize);

                            if (_outputBuf.Count > 0)
                                BeginSend();
                        }
                        catch (Exception e)
                        {
                            Close(sock, e);
                        }
                    });
                }, null);
            }
            catch (Exception e)
            {
                Close(sock, e);
            }
        }


        //0: ok, 1: NetUnconnected, 2: OutputBufferExceed
        public int SendProtocol(int type, byte[] data)
        {
            if (!Connected)
                return 1;
            
            if (_outputBuf.Count >= OutputBufferSize)
                return 2;

            var os = new OctetsStream();
            os.MarshalSize(type).Marshal(data);
            var emptyBeforeAdd = (_outputBuf.Count == 0);
            _outputBuf.Append(_outputSecurity.Update(os.Data));
            if (emptyBeforeAdd)
                BeginSend();
            return 0;
        }


        public void Process(long maxMilliseconds)
        {
            _frameWatcher.Reset();
            _frameWatcher.Start();
            if (_startReconnect && _reconnectWatcher.ElapsedMilliseconds >= _reconnectDelay)
            {
                Connect();
            }

            while (_frameWatcher.ElapsedMilliseconds < maxMilliseconds)
            {
                IoAction ioAction;
                if (_actions.TryDequeue(out ioAction))
                {
                    ioAction();
                }
                else
                {
                    break;
                }
            }

            while (_frameWatcher.ElapsedMilliseconds < maxMilliseconds)
            {
                if (_protocols.Count > 0)
                {
                    var p = _protocols.Dequeue();
                    if (OnRecvProtocol != null)
                        OnRecvProtocol(p.Type, p.Data);
                }
                else
                {
                    break;
                }
            }
            
        }

        public void LateProcess(long maxMilliseconds)
        {
            while (_frameWatcher.ElapsedMilliseconds < maxMilliseconds)
            {
                IoAction ioAction;
                if (_actions.TryDequeue(out ioAction))
                {
                    ioAction();
                }
                else
                {
                    break;
                }
            }
        }

    }
}
