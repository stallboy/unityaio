using System;
using System.Collections.Generic;
using System.Text;

namespace Aio
{

    //IOAction, ConcurrentQueue for .net 2.0  
    internal delegate void IoAction();

    internal sealed class ConcurrentQueue<T>
    {
        private readonly Queue<T> _inner = new Queue<T>();
        private readonly object _obj = new object();

        public bool TryDequeue(out T item)
        {
            lock (_obj)
            {
                if (_inner.Count == 0)
                {
                    item = default(T);
                    return false;
                }
                item = _inner.Dequeue();
                return true;
            }
        }

        public void Enqueue(T item)
        {
            lock (_obj)
            {
                _inner.Enqueue(item);
            }
        }

        public int Count
        {
            get
            {
                lock (_obj)
                {
                    return _inner.Count;
                }
            }
        }

        public void Clear()
        {
            lock (_obj)
            {
                _inner.Clear();
            }
        }
    }

    internal sealed class Protocol
    {
        public readonly int Type;
        public readonly byte[] Data;

        public Protocol(int type, byte[] data)
        {
            Type = type;
            Data = data;
        }
    }

    public class Utils
    {  
        public static int Roundup(int src, int initial)
        {
            var dst = initial;
            while (dst < src)
                dst <<= 1;
            return dst;
        }

        public static string BytesToHexString(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }

        private const string HexDigits = "0123456789abcdef";
        public static byte[] HexStringToBytes(string str)
        {
            var bytes = new byte[str.Length >> 1];
            for (var i = 0; i < str.Length; i += 2)
            {
                int highDigit = HexDigits.IndexOf(Char.ToLowerInvariant(str[i]));
                int lowDigit = HexDigits.IndexOf(Char.ToLowerInvariant(str[i + 1]));
                if (highDigit == -1 || lowDigit == -1)
                {
                    throw new ArgumentException("The string contains an invalid digit.");
                }
                bytes[i >> 1] = (byte)((highDigit << 4) | lowDigit);
            }
            return bytes;
        }

    }
}
