﻿/*
 * Improved version to C# LibLZF Port:
 * Copyright (c) 2010 Roman Atachiants <kelindar@gmail.com>
 * 
 * Original CLZF Port:
 * Copyright (c) 2005 Oren J. Maurice <oymaurice@hazorea.org.il>
 * 
 * Original LibLZF Library & Algorithm:
 * Copyright (c) 2000-2008 Marc Alexander Lehmann <schmorp@schmorp.de>
 * 
 * Redistribution and use in source and binary forms, with or without modifica-
 * tion, are permitted provided that the following conditions are met:
 * 
 *   1.  Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 * 
 *   2.  Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 * 
 *   3.  The name of the author may not be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MER-
 * CHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO
 * EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPE-
 * CIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTH-
 * ERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * Alternatively, the contents of this file may be used under the terms of
 * the GNU General Public License version 2 (the "GPL"), in which case the
 * provisions of the GPL are applicable instead of the above. If you wish to
 * allow the use of your version of this file only under the terms of the
 * GPL and not to allow others to use your version of this file under the
 * BSD license, indicate your decision by deleting the provisions above and
 * replace them with the notice and other provisions required by the GPL. If
 * you do not delete the provisions above, a recipient may use your version
 * of this file under either the BSD or the GPL.
 */

using System;

namespace Gibbed.RED.Pack
{
    /// <summary>
    /// Improved C# LZF Compressor, a very small data compression library.
    /// The compression algorithm is extremely fast. 
    /// </summary>
    public sealed class Lzf
    {
        /// <summary>
        /// Hashtable, that can be allocated only once
        /// </summary>
        private readonly long[] _HashTable = new long[_Hsize];

        private const uint _Hlog = 14;
        private const uint _Hsize = (1 << 14);
        private const uint _MaxLit = (1 << 5);
        private const uint _MaxOff = (1 << 13);
        private const uint _MaxRef = ((1 << 8) + (1 << 3));

        /// <summary>
        /// Compresses the data using LibLZF algorithm
        /// </summary>
        /// <param name="input">Reference to the data to compress</param>
        /// <param name="inputLength">Length of the data to compress</param>
        /// <param name="output">Reference to a buffer which will contain the compressed data</param>
        /// <param name="outputLength">Length of the compression buffer (should be bigger than the input buffer)</param>
        /// <returns>The size of the compressed archive in the output buffer</returns>
        public int Compress(byte[] input, int inputLength, byte[] output, int outputLength)
        {
            Array.Clear(this._HashTable, 0, (int)_Hsize);

            uint iidx = 0;
            uint oidx = 0;

            var hval = (uint)(((input[iidx]) << 8) | input[iidx + 1]); // FRST(in_data, iidx);
            int lit = 0;

            for (;;)
            {
                if (iidx < inputLength - 2)
                {
                    hval = (hval << 8) | input[iidx + 2];
                    long hslot = ((hval ^ (hval << 5)) >> (int)(((3 * 8 - _Hlog)) - hval * 5) & (_Hsize - 1));
                    long reference = this._HashTable[hslot];
                    this._HashTable[hslot] = iidx;


                    long off;
                    if ((off = iidx - reference - 1) < _MaxOff
                        && iidx + 4 < inputLength
                        && reference > 0
                        && input[reference + 0] == input[iidx + 0]
                        && input[reference + 1] == input[iidx + 1]
                        && input[reference + 2] == input[iidx + 2]
                        )
                    {
                        /* match found at *reference++ */
                        uint len = 2;
                        uint maxlen = (uint)inputLength - iidx - len;
                        maxlen = maxlen > _MaxRef ? _MaxRef : maxlen;

                        if (oidx + lit + 1 + 3 >= outputLength)
                        {
                            return 0;
                        }

                        do
                        {
                            len++;
                        }
                        while (len < maxlen && input[reference + len] == input[iidx + len]);

                        if (lit != 0)
                        {
                            output[oidx++] = (byte)(lit - 1);
                            lit = -lit;
                            do
                            {
                                output[oidx++] = input[iidx + lit];
                            }
                            while ((++lit) != 0);
                        }

                        len -= 2;
                        iidx++;

                        if (len < 7)
                        {
                            output[oidx++] = (byte)((off >> 8) + (len << 5));
                        }
                        else
                        {
                            output[oidx++] = (byte)((off >> 8) + (7 << 5));
                            output[oidx++] = (byte)(len - 7);
                        }

                        output[oidx++] = (byte)off;

                        iidx += len - 1;
                        hval = (uint)(((input[iidx]) << 8) | input[iidx + 1]);

                        hval = (hval << 8) | input[iidx + 2];
                        this._HashTable[((hval ^ (hval << 5)) >> (int)(((3 * 8 - _Hlog)) - hval * 5) & (_Hsize - 1))] = iidx;
                        iidx++;

                        hval = (hval << 8) | input[iidx + 2];
                        this._HashTable[((hval ^ (hval << 5)) >> (int)(((3 * 8 - _Hlog)) - hval * 5) & (_Hsize - 1))] = iidx;
                        iidx++;
                        continue;
                    }
                }
                else if (iidx == inputLength)
                {
                    break;
                }

                /* one more literal byte we must copy */
                lit++;
                iidx++;

                if (lit == _MaxLit)
                {
                    if (oidx + 1 + _MaxLit >= outputLength)
                    {
                        return 0;
                    }

                    output[oidx++] = (byte)(_MaxLit - 1);
                    lit = -lit;
                    do
                    {
                        output[oidx++] = input[iidx + lit];
                    }
                    while ((++lit) != 0);
                }
            }

            if (lit != 0)
            {
                if (oidx + lit + 1 >= outputLength)
                {
                    return 0;
                }

                output[oidx++] = (byte)(lit - 1);
                lit = -lit;
                do
                {
                    output[oidx++] = input[iidx + lit];
                }
                while ((++lit) != 0);
            }

            return (int)oidx;
        }
    }
}
