

using System;
using System.Net;

namespace Aio
{
    public sealed class Compress
    {
        private enum Mppc 
        {
            CtrlOffEob = 0,
            MppcHistLen = 8192
        }

        private readonly byte[] _history = new byte[(int) Mppc.MppcHistLen];
        private uint _histptr;
        private readonly uint[] _hash = new uint[256];
        private uint _legacyIn;

        private static void put_bits(byte[] buf, ref uint pos, uint val, uint n, ref uint l)
        {
            l += n;
            int t = IPAddress.HostToNetworkOrder((int) (val << (32 - (int) l))) | buf[pos];
            Array.Copy(BitConverter.GetBytes(t), 0, buf, pos, 4);
            pos += l >> 3;
            l &= 7;
        }

        private static void put_lit(byte[] buf, ref uint pos, uint c, ref uint l)
        {
            if (c < 0x80)
                put_bits(buf, ref pos, c, 8, ref l);
            else
                put_bits(buf, ref pos, c & 0x7f | 0x100, 9, ref l);
        }

        private void put_off(byte[] buf, ref uint pos, uint off, ref uint l)
        {
            if (off < 64)
                put_bits(buf, ref pos, 0x3c0 | off, 10, ref l);
            else if (off < 320)
                put_bits(buf, ref pos, 0xe00 | (off - 64), 12, ref l);
            else
                put_bits(buf, ref pos, 0xc000 | (off - 320), 16, ref l);
        }

        private bool compare_short(uint p, uint s)
        {
            return _history[p] == _history[s] && _history[p + 1] == _history[s + 1];
        }

        private void compress_block(byte[] obuf, ref uint pos, uint isize)
        {
            uint r = _histptr + isize;
            uint s = _histptr;
            uint l = 0;
            obuf[pos] = 0;

            while (r - s > 2)
            {
                uint p = _hash[_history[s]];
                _hash[_history[s]] = s;
                if (p >= s)
                {
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                    s = _histptr;
                }
                else if (!compare_short(p, s++))
                {
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                }
                else if (_history[(p+=2)] != _history[++s])
                {
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                    s = _histptr;
                }
                else
                {
                    for (p++, s++; s < r && _history[p] == _history[s]; p++, s++)
                    {
                    }
                    uint len = s - _histptr;
                    _histptr = s;
                    put_off(obuf, ref pos, s - p, ref l);

                    if (len < 4)
                        put_bits(obuf, ref pos, 0, 1, ref l);
                    else if (len < 8)
                        put_bits(obuf, ref pos, 0x08 | (len & 0x03), 4, ref l);
                    else if (len < 16)
                        put_bits(obuf, ref pos, 0x30 | (len & 0x07), 6, ref l);
                    else if (len < 32)
                        put_bits(obuf, ref pos, 0xe0 | (len & 0x0f), 8, ref l);
                    else if (len < 64)
                        put_bits(obuf, ref pos, 0x3c0 | (len & 0x1f), 10, ref l);
                    else if (len < 128)
                        put_bits(obuf, ref pos, 0xf80 | (len & 0x3f), 12, ref l);
                    else if (len < 256)
                        put_bits(obuf, ref pos, 0x3f00 | (len & 0x7f), 14, ref l);
                    else if (len < 512)
                        put_bits(obuf, ref pos, 0xfe00 | (len & 0xff), 16, ref l);
                    else if (len < 1024)
                        put_bits(obuf, ref pos, 0x3fc00 | (len & 0x1ff), 18, ref l);
                    else if (len < 2048)
                        put_bits(obuf, ref pos, 0xff800 | (len & 0x3ff), 20, ref l);
                    else if (len < 4096)
                        put_bits(obuf, ref pos, 0x3ff000 | (len & 0x7ff), 22, ref l);
                    else if (len < (uint) Mppc.MppcHistLen)
                        put_bits(obuf, ref pos, 0xffe000 | (len & 0xfff), 24, ref l);
                }
            }

            switch (r - s)
            {
                case 2:
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                    break;
                case 1:
                    put_lit(obuf, ref pos, _history[_histptr++], ref l);
                    break;
            }
            put_off(obuf, ref pos, (uint) Mppc.CtrlOffEob, ref l);
            if (l != 0)
                put_bits(obuf, ref pos, 0, 8 - l, ref l);
            _legacyIn = 0;
        }


        private Octets Update(Octets oin)
        {
            var oout = new Octets();
            uint ipos = 0, opos = 0;
            byte[] ibuf = oin.ByteArray;
            var isize = (uint) oin.Count;
            uint remain = (uint) Mppc.MppcHistLen - _histptr - _legacyIn;

            if (isize >= remain)
            {
                oout.Resize((int) (isize + _legacyIn)*9/8 + 6);
                byte[] obuf = oout.ByteArray;
                Array.Copy(ibuf, ipos, _history, _histptr + _legacyIn, remain);
                isize -= remain;
                ipos += remain;
                compress_block(obuf, ref opos, remain + _legacyIn);
                _histptr = 0;

                for (;
                    isize >= (uint) Mppc.MppcHistLen;
                    isize -= (uint) Mppc.MppcHistLen, ipos += (uint) Mppc.MppcHistLen)
                {
                    Array.Copy(ibuf, ipos, _history, _histptr, (int) Mppc.MppcHistLen);
                    compress_block(obuf, ref opos, (uint) Mppc.MppcHistLen);
                    _histptr = 0;
                }
                oout.Resize((int) opos);
                
            }

            Array.Copy(ibuf, ipos, _history, _histptr + _legacyIn, isize);
            _legacyIn += isize;
            return oin.Swap(oout);
        }
        

        public Octets Final(Octets oin)
        {
            if (oin.Count == 0 && _legacyIn == 0)
                return oin;

            Octets oout = Update(oin);
            int osize = oout.Count;
            oout.Reserve(osize + (int) _legacyIn*9/8 + 6);
            byte[] obuf = oout.ByteArray;
            var opos = (uint) osize;
            compress_block(obuf, ref opos, _legacyIn);
            oout.Resize((int) opos);
            return oin.Swap(oout);
        }
    }

    public class Decompress
    {
        private enum Mppc
        {
            CtrlOffEob = 0,
            MppcHistLen = 8192
        }

        private readonly byte[] _history = new byte[(int) Mppc.MppcHistLen];
        private uint _histptr;
        private uint _l, _adjustL;
        private uint _blen, _blenTotol;
        private uint _rptr, _adjustRptr;
        private readonly Octets _legacyIn = new Octets();

        private bool Passbits(uint n)
        {
            _l += n;
            _blen += n;
            if (_blen < _blenTotol)
                return true;

            _l = _adjustL;
            _rptr = _adjustRptr;
            return false;
        }

        private uint Fetch()
        {
            _rptr += _l >> 3;
            _l &= 7;
            return
                (uint) (IPAddress.HostToNetworkOrder(BitConverter.ToInt32(_legacyIn.ByteArray, (int) _rptr)) << (int) _l);
        }


        private void LameCopy(byte[] arry, int dst, int src, int len)
        {
            while (len-- > 0) 
                arry[dst++] = arry[src++];
        }

        public Octets Update(Octets oin)
        {
            _legacyIn.Append(oin);
            _blenTotol = (uint) (_legacyIn.Count*8 - _l);
            _legacyIn.Reserve(_legacyIn.Count + 3);

            _rptr = 0;
            _blen = 7;
            Octets oout = oin;
            oout.Clear();
            uint histhead = _histptr;

            while (_blenTotol > _blen)
            {
                _adjustL = _l;
                _adjustRptr = _rptr;
                uint val = Fetch();

                if (val < 0x80000000)
                {
                    if (!Passbits(8))
                        break;
                    _history[_histptr++] = (byte) (val >> 24);
                    continue;
                }
                if (val < 0xc0000000)
                {
                    if (!Passbits(9))
                        break;
                    _history[_histptr++] = (byte) (((val >> 23) | 0x80) & 0xff);
                    continue;
                }

                uint len;
                uint off = 0;
                if (val >= 0xf0000000)
                {
                    if (!Passbits(10))
                        break;
                    off = (val >> 22) & 0x3f;
                    if (off == (uint) Mppc.CtrlOffEob)
                    {
                        uint advance = 8 - (_l & 7);
                        if (advance < 8)
                            if (!Passbits(advance))
                                break;
                        oout.Append(_history, (int) histhead, (int) (_histptr - histhead));
                        if (_histptr == (uint) Mppc.MppcHistLen)
                            _histptr = 0;
                        histhead = _histptr;
                        continue;
                    }
                }
                else if (val >= 0xe0000000)
                {
                    if (!Passbits(12))
                        break;
                    off = ((val >> 20) & 0xff) + 64;
                }
                else if (val >= 0xc0000000)
                {
                    if (!Passbits(16))
                        break;
                    off = ((val >> 16) & 0x1fff) + 320;
                }


                val = Fetch();
                if (val < 0x80000000)
                {
                    if (!Passbits(1))
                        break;
                    len = 3;
                }
                else if (val < 0xc0000000)
                {
                    if (!Passbits(4))
                        break;
                    len = 4 | ((val >> 28) & 3);
                }
                else if (val < 0xe0000000)
                {
                    if (!Passbits(6))
                        break;
                    len = 8 | ((val >> 26) & 7);
                }
                else if (val < 0xf0000000)
                {
                    if (!Passbits(8))
                        break;
                    len = 16 | ((val >> 24) & 15);
                }
                else if (val < 0xf8000000)
                {
                    if (!Passbits(10))
                        break;
                    len = 32 | ((val >> 22) & 0x1f);
                }
                else if (val < 0xfc000000)
                {
                    if (!Passbits(12))
                        break;
                    len = 64 | ((val >> 20) & 0x3f);
                }
                else if (val < 0xfe000000)
                {
                    if (!Passbits(14))
                        break;
                    len = 128 | ((val >> 18) & 0x7f);
                }
                else if (val < 0xff000000)
                {
                    if (!Passbits(16))
                        break;
                    len = 256 | ((val >> 16) & 0xff);
                }
                else if (val < 0xff800000)
                {
                    if (!Passbits(18))
                        break;
                    len = 0x200 | ((val >> 14) & 0x1ff);
                }
                else if (val < 0xffc00000)
                {
                    if (!Passbits(20))
                        break;
                    len = 0x400 | ((val >> 12) & 0x3ff);
                }
                else if (val < 0xffe00000)
                {
                    if (!Passbits(22))
                        break;
                    len = 0x800 | ((val >> 10) & 0x7ff);
                }
                else if (val < 0xfff00000)
                {
                    if (!Passbits(24))
                        break;
                    len = 0x1000 | ((val >> 8) & 0xfff);
                }
                else
                {
                    _l = _adjustL;
                    _rptr = _adjustRptr;
                    break;
                }

                if (_histptr < off || _histptr + len > (uint) Mppc.MppcHistLen)
                    break;
                // Array.Copy(history, histptr - off, history, histptr, histptr - histhead);
                LameCopy(_history, (int) _histptr, (int) (_histptr - off), (int) len);
                // Array.Copy(history, histptr - off, history, histptr, len);
                _histptr += len;
            }

            oout.Append(_history, (int) histhead, (int) (_histptr - histhead));
            _legacyIn.Erase(0, (int) _rptr);
            return oout;
        }
    }
}
