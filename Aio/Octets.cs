using System;

namespace Aio
{
    public sealed class Octets : IComparable<Octets>
    {
        private const int DefaultSize = 128;


        public byte[] ByteArray { get; private set; }

        public int Count { get; private set; }

        public int Capacity
        {
            get { return ByteArray.Length; }
        }

        public int Remaining
        {
            get { return ByteArray.Length - Count; }
        }


        public static Octets Wrap(byte[] bytes, int length)
        {
            return new Octets(bytes, length);
        }

        public static Octets Wrap(byte[] bytes)
        {
            return new Octets(bytes, bytes.Length);
        }

        private Octets(byte[] bytes, int length)
        {
            ByteArray = bytes;
            Count = length;
        }

        public Octets()
        {
            Reserve(DefaultSize);
        }

        public Octets(int size)
        {
            Reserve(size);
        }

        public Octets(Octets rhs)
        {
            Replace(rhs);
        }

        public Octets(byte[] rhs)
        {
            Replace(rhs);
        }

        public Octets(byte[] rhs, int pos, int size)
        {
            Replace(rhs, pos, size);
        }

        public Octets(Octets rhs, int pos, int size)
        {
            Replace(rhs, pos, size);
        }

        public void Reserve(int size)
        {
            if (ByteArray == null)
            {
                ByteArray = new byte[Utils.Roundup(size, DefaultSize)];
            }
            else if (size > ByteArray.Length)
            {
                var tmp = new byte[Utils.Roundup(size, DefaultSize)];
                Buffer.BlockCopy(ByteArray, 0, tmp, 0, Count);
                ByteArray = tmp;
            }
        }


        public Octets Replace(byte[] data, int pos, int size)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (size < 0 || pos < 0)
                throw new ArgumentOutOfRangeException();
            if (data.Length < pos + size)
                throw new ArgumentException();

            Reserve(size);
            Count = size;
            Buffer.BlockCopy(data, pos, ByteArray, 0, size); 
            return this;
        }

        public Octets Replace(Octets data, int pos, int size)
        {
            return Replace(data.ByteArray, pos, size);
        }

        public Octets Replace(byte[] data)
        {
            return Replace(data, 0, data.Length);
        }

        public Octets Replace(Octets data)
        {
            return Replace(data.ByteArray, 0, data.Count);
        }

        public Octets Resize(int size)
        {
            Reserve(size);
            Count = size;
            return this;
        }

        public Octets Clear() 
        { 
            Count = 0; 
            return this; 
        }

        public Octets Swap(Octets rhs)
        {
            int size = Count; 
            Count = rhs.Count; 
            rhs.Count = size;
            byte[] tmp = rhs.ByteArray; 
            rhs.ByteArray = ByteArray; 
            ByteArray = tmp;
            return this;
        }

        public Octets Append(byte data)
        {
            Reserve(Count + 1);
            ByteArray[Count++] = data;
            return this;
        }

        public Octets Append(byte[] data, int pos, int size)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (size < 0 || pos < 0)
                throw new ArgumentOutOfRangeException();
            if (data.Length < pos + size)
                throw new ArgumentException();

            Reserve(Count + size);
            Buffer.BlockCopy(data, pos, ByteArray, Count, size);
            Count += size;
            return this;
        }

        public Octets Append(Octets data, int pos, int size)
        {
            return Append(data.ByteArray, pos, size);
        }

        public Octets Append(byte[] data)
        {
            return Append(data, 0, data.Length);
        }

        public Octets Append(Octets data)
        {
            return Append(data.ByteArray, 0, data.Count);
        }


        public Octets Erase(int from, int to)
        {
            if (from < 0 || from > Count || to < 0 || to > Count)
                throw new ArgumentOutOfRangeException();
            if (from > to) //allow from == to
                throw new ArgumentException();

            Buffer.BlockCopy(ByteArray, to, ByteArray, from, Count - to);
            Count -= to - from;
            return this;
        }

        public void EraseAndCompact(int position, int reserveSize)
        {
            if (position < 0 || position > Count)
                throw new ArgumentOutOfRangeException();

            Count -= position;
            int upSize = Utils.Roundup(Count, reserveSize);
            if (ByteArray.Length > upSize)
            {
                var tmp = new byte[upSize];
                Buffer.BlockCopy(ByteArray, position, tmp, 0, Count);
                ByteArray = tmp;
            }
            else
            {
                Buffer.BlockCopy(ByteArray, position, ByteArray, 0, Count);
            }
        }

        public Octets Insert(int from, byte[] data, int pos, int size)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (size < 0 || pos < 0)
                throw new ArgumentOutOfRangeException();
            if (data.Length < pos + size)
                throw new ArgumentException();
            if (from < 0 || from > Count)
                throw new ArgumentOutOfRangeException();

            Reserve(Count + size);

            Buffer.BlockCopy(ByteArray, from, ByteArray, from + size, Count - from);
            Buffer.BlockCopy(data, pos, ByteArray, from, size);
            Count += size;
            return this;
        }

        public Octets Insert(int from, Octets data, int pos, int size)
        {
            return Insert(from, data.ByteArray, pos, size);
        }

        public Octets Insert(int from, byte[] data)
        {
            return Insert(from, data, 0, data.Length);
        }

        public Octets Insert(int from, Octets data)
        {
            return Insert(from, data.ByteArray, 0, data.Count);
        }

        public byte[] GetBytes()
        {
            var tmp = new byte[Count];
            Buffer.BlockCopy(ByteArray, 0, tmp, 0, Count);
            return tmp;
        }

        public byte GetByte(int pos)
        {
            return ByteArray[pos];
        }

        public void SetByte(int pos, byte b)
        {
            ByteArray[pos] = b;
        }

        public int CompareTo(Octets rhs)
        {   
            int c = Count - rhs.Count;
            if (c != 0)
                return c;

            byte[] v1 = ByteArray;
            byte[] v2 = rhs.ByteArray;
            for (int i = 0; i < Count; i++)
            {
                int v = v1[i] - v2[i];
                if (v != 0)
                    return v;
            }
            return 0;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != GetType())
                return false;
            var other = (Octets)obj;
            return 0 == CompareTo(other);
        }

        public override int GetHashCode()
        {
            var result = 1;
            for (var i = 0; i < Count; i++)
                result = 31 * result + ByteArray[i];
            return result;
        }

        public string ToHexString()
        {
            return Utils.BytesToHexString(GetBytes());
        }
    }
}
