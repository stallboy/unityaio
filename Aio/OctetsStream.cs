using System;
using System.Text;

namespace Aio
{
    public sealed class MarshalException : Exception
    {
    }

    public sealed class OctetsStream
    {
        public Octets Data { get; private set; }

        public int Position { get; private set; }

        public int Remaining
        {
            get { return Data.Count - Position; }
        }

        public bool Eos
        {
            get { return Position == Data.Count; }
        }


        public OctetsStream()
        {
            Data = new Octets();
        }

        public OctetsStream(int size)
        {
            Data = new Octets(size);
        }

        public static OctetsStream Wrap(Octets o)
        {
            return new OctetsStream(o);
        }

        private OctetsStream(Octets o)
        {
            Data = o;
        }

        public int Begin()
        {
            return Position;
        }

        public OctetsStream Rollback(int tranpos)
        {
            Position = tranpos;
            return this;
        }

        public OctetsStream Marshal(bool b)
        {
            Data.Append((byte)(b ? 1 : 0));
            return this;
        }

        public OctetsStream Marshal(byte x)
        {
            Data.Append(x);
            return this;
        }

        public OctetsStream Marshal(sbyte x)
        {
            Data.Append((byte)x);
            return this;
        }

        public OctetsStream Marshal(ushort x)
        {
            return
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(short x)
        {
            return
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(uint x)
        {
            return
            Marshal((byte)(x >> 24)).
            Marshal((byte)(x >> 16)).
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(int x)
        {
            return
            Marshal((byte)(x >> 24)).
            Marshal((byte)(x >> 16)).
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(ulong x)
        {
            return
            Marshal((byte)(x >> 56)).
            Marshal((byte)(x >> 48)).
            Marshal((byte)(x >> 40)).
            Marshal((byte)(x >> 32)).
            Marshal((byte)(x >> 24)).
            Marshal((byte)(x >> 16)).
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(long x)
        {
            return
            Marshal((byte)(x >> 56)).
            Marshal((byte)(x >> 48)).
            Marshal((byte)(x >> 40)).
            Marshal((byte)(x >> 32)).
            Marshal((byte)(x >> 24)).
            Marshal((byte)(x >> 16)).
            Marshal((byte)(x >> 8)).
            Marshal((byte)(x));
        }

        public OctetsStream Marshal(float x)
        {
            byte[] tmp = BitConverter.GetBytes(x);
            if (BitConverter.IsLittleEndian)
            {
                return Marshal(tmp[3]).
                    Marshal(tmp[2]).
                    Marshal(tmp[1]).
                    Marshal(tmp[0]);
            }
            return Marshal(tmp[0]).
                Marshal(tmp[1]).
                Marshal(tmp[2]).
                Marshal(tmp[3]);
        }

        public OctetsStream Marshal(double x)
        {
            byte[] tmp = BitConverter.GetBytes(x);
            if (BitConverter.IsLittleEndian)
            {
                return
                    Marshal(tmp[7]).
                    Marshal(tmp[6]).
                    Marshal(tmp[5]).
                    Marshal(tmp[4]).
                    Marshal(tmp[3]).
                    Marshal(tmp[2]).
                    Marshal(tmp[1]).
                    Marshal(tmp[0]);
            }
            return
                Marshal(tmp[0]).
                    Marshal(tmp[1]).
                    Marshal(tmp[2]).
                    Marshal(tmp[3]).
                    Marshal(tmp[4]).
                    Marshal(tmp[5]).
                    Marshal(tmp[6]).
                    Marshal(tmp[7]);
        }


        public OctetsStream Marshal(Octets o)
        {
            MarshalSize(o.Count);
            Data.Append(o);
            return this;
        }

        public OctetsStream Marshal(byte[] bytes)
        {
            MarshalSize(bytes.Length);
            Data.Append(bytes);
            return this;
        }

        public OctetsStream Marshal(string str)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(str);
            return Marshal(bytes);
        }

        public OctetsStream MarshalSize(int size)
        {
            var x = (uint)size;
            if (x < 0x80)
                return Marshal((byte)x);
            if (x < 0x4000)
                return Marshal((short)(x | 0x8000));
            if (x < 0x20000000)
                return Marshal(x | 0xc0000000);
            return Marshal((byte)0xe0).Marshal(x);
        }


        public bool UnmarshalBool()
        {
            return UnmarshalByte() == 1;
        }

        public byte UnmarshalByte()
        {
            if (Position + 1 > Data.Count)
                throw new MarshalException();
            return Data.GetByte(Position++);
        }

        public sbyte UnmarshalSbyte()
        {
            if (Position + 1 > Data.Count)
                throw new MarshalException();
            return (sbyte)Data.GetByte(Position++);
        }

        public ushort UnmarshalUshort()
        {
            if (Position + 2 > Data.Count)
                throw new MarshalException();
            byte b0 = Data.GetByte(Position++);
            byte b1 = Data.GetByte(Position++);
            return (ushort)((b0 << 8) | (b1 & 0xff));
        }

        public short UnmarshalShort()
        {
            if (Position + 2 > Data.Count)
                throw new MarshalException();
            byte b0 = Data.GetByte(Position++);
            byte b1 = Data.GetByte(Position++);
            return (short)((b0 << 8) | (b1 & 0xff));
        }

        public uint UnmarshalUint()
        {
            if (Position + 4 > Data.Count)
                throw new MarshalException();
            byte b0 = Data.GetByte(Position++);
            byte b1 = Data.GetByte(Position++);
            byte b2 = Data.GetByte(Position++);
            byte b3 = Data.GetByte(Position++);
            return (uint)(
                ((b0 & 0xff) << 24) |
                ((b1 & 0xff) << 16) |
                ((b2 & 0xff) << 8) |
                ((b3 & 0xff) << 0));
        }

        public int UnmarshalInt()
        {
            if (Position + 4 > Data.Count)
                throw new MarshalException();
            byte b0 = Data.GetByte(Position++);
            byte b1 = Data.GetByte(Position++);
            byte b2 = Data.GetByte(Position++);
            byte b3 = Data.GetByte(Position++);
            return ((b0 & 0xff) << 24) |
                   ((b1 & 0xff) << 16) |
                   ((b2 & 0xff) << 8) |
                   ((b3 & 0xff) << 0);
        }

        public ulong UnmarshalUlong()
        {
            if (Position + 8 > Data.Count)
                throw new MarshalException();
            byte b0 = Data.GetByte(Position++);
            byte b1 = Data.GetByte(Position++);
            byte b2 = Data.GetByte(Position++);
            byte b3 = Data.GetByte(Position++);
            byte b4 = Data.GetByte(Position++);
            byte b5 = Data.GetByte(Position++);
            byte b6 = Data.GetByte(Position++);
            byte b7 = Data.GetByte(Position++);
            return ((((ulong)b0) & 0xff) << 56) |
                   ((((ulong)b1) & 0xff) << 48) |
                   ((((ulong)b2) & 0xff) << 40) |
                   ((((ulong)b3) & 0xff) << 32) |
                   ((((ulong)b4) & 0xff) << 24) |
                   ((((ulong)b5) & 0xff) << 16) |
                   ((((ulong)b6) & 0xff) << 8) |
                   ((((ulong)b7) & 0xff) << 0);
        }

        public long UnmarshalLong()
        {
            return (long)UnmarshalUlong();
        }

        public float UnmarshalFloat()
        {
            if (Position + 4 > Data.Count)
                throw new MarshalException();
            var tmp = new byte[4];
            for (int i = 1; i <= 4; ++i)
            {
                tmp[4 - i] = UnmarshalByte();
            }
            return BitConverter.ToSingle(tmp, 0);
        }

        public double UnmarshalDouble()
        {
            if (Position + 8 > Data.Count)
                throw new MarshalException();
            var tmp = new byte[8];
            for (int i = 1; i <= 8; ++i)
            {
                tmp[8 - i] = UnmarshalByte();
            }
            return BitConverter.ToDouble(tmp, 0);
        }



        public Octets UnmarshalOctets()
        {
            int size = UnmarshalSize();
            if (Position + size > Data.Count)
                throw new MarshalException();
            var o = new Octets(Data, Position, size);
            Position += size;
            return o;
        }

        public byte[] UnmarshalBytes()
        {
            int size = UnmarshalSize();
            return UnmarshalFixedSizeBytes(size);
        }

        internal byte[] UnmarshalFixedSizeBytes(int size)
        {
            if (Position + size > Data.Count)
                throw new MarshalException();
            var copy = new byte[size];
            Buffer.BlockCopy(Data.ByteArray, Position, copy, 0, size);
            Position += size;
            return copy;
        }
        
        public string UnmarshalString()
        {
            byte[] bytes = UnmarshalBytes();
            return Encoding.Unicode.GetString(bytes);
        }


        public int UnmarshalSize()
        {
            if (Position == Data.Count)
                throw new MarshalException();
            switch (Data.GetByte(Position) & 0xe0)
            {
                case 0xe0:
                    UnmarshalByte();
                    return (int)UnmarshalUint();
                case 0xc0:
                    return (int)(UnmarshalUint() & ~0xc0000000);
                case 0xa0:
                case 0x80:
                    return UnmarshalUshort() & 0x7fff;
            }
            return UnmarshalByte();
        }

    }
}
