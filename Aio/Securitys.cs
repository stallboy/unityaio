namespace Aio
{
    public interface ISecurity
    {
        Octets Parameter { set; }
        Octets Update(Octets o); //after Update, o is changed == return value;
    }

    public sealed class NullSecurity : ISecurity
    {
        public static readonly NullSecurity Instance = new NullSecurity();

        private NullSecurity()
        {    
        }

        public Octets Parameter
        {
            set {}
        }

        public Octets Update(Octets o)
        {
            return o;
        }
    }

    
    public sealed class Arc4Security : ISecurity
    {
        private readonly byte[] _perm = new byte[256];
        private byte _index1;
        private byte _index2;

        public Octets Parameter
        {
            set
            {
                int keylen = value.Count;
                for (int i = 0; i < 256; i++)
                {
                    _perm[i] = (byte)i;
                }
                byte j = 0;
                for (int i = 0; i < 256; i++)
                {
                    j += _perm[i];
                    j += value.ByteArray[i % keylen];
                    byte k = _perm[i]; _perm[i] = _perm[j]; _perm[j] = k;
                }
                _index1 = _index2 = 0;
            }
        }

        public Octets Update(Octets o)
        {
            for (int i = 0; i < o.Count; i++)
            {
                byte j1 = _perm[++_index1];
                byte j2 = _perm[_index2 += j1];
                _perm[_index2] = j1;
                _perm[_index1] = j2;
                o.ByteArray[i] ^= _perm[(byte) (j1 + j2)];
            }
            return o;
        }
    }

    public sealed class CompressArc4Security : ISecurity
    {
        private readonly Arc4Security _arc4 = new Arc4Security();
        private readonly Compress _compress = new Compress();

        public Octets Parameter
        {
            set { _arc4.Parameter = value; }
        }

        public Octets Update(Octets o)
        {
            return _arc4.Update(_compress.Final(o));
        }
    }

    public sealed class DecompressArc4Security : ISecurity
    {
        private readonly Arc4Security _arc4 = new Arc4Security();
        private readonly Decompress _decompress = new Decompress();

        public Octets Parameter
        {
            set { _arc4.Parameter = value; }
        }

        public Octets Update(Octets o)
        {
            return _decompress.Update(_arc4.Update(o));
        }
    }

}
