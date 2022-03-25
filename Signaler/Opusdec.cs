using Concentus.Oggfile;
using Concentus.Structs;
using System;
using System.IO;

namespace Signaler
{
    public class Opusdec
    {
        private int _pktCounter = 0;
        private object _lock = new { };

        private Stream _streamBuffer;
        private OpusDecoder _decoder;

        public Opusdec()
        {
            _streamBuffer = new MemoryStream();
            _decoder = new OpusDecoder(48000, 2);
            
        }

        public void DecodeRawAudio(byte[] pktPayload)
        {

            //var a = _decoder.LastPacketDuration =  frameSize;

            
            //_streamBuffer.Write(pktPayload, 0, pktPayload.Length);

            //if (_pktCounter == 30)
            //{
            //    using var fileOut = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), $"pkt-{DateTime.Now.ToFileTime()}.opus"), FileMode.Create);

            //    var bytes = ((MemoryStream)_streamBuffer).ToArray();

            //    var frameSize = OpusPacketInfo.GetNumSamples(_decoder, bytes, 0, bytes.Length);

            //    var ms = new MemoryStream(bytes);

            //    var inBuf = new byte[bytes.Length * 1024];

            //    while (ms.Length - ms.Position >= bytes.Length)
            //    {
            //        var pcm = BytesToShorts(inBuf, 0, inBuf.Length);
            //        _decoder.Decode(bytes, 0, bytes.Length, pcm, 0, frameSize, false);
            //        var bytesOut = ShortsToBytes(pcm);
            //        fileOut.Write(bytesOut, 0, bytesOut.Length);
            //    }

            //    _streamBuffer = new MemoryStream();
            //    _pktCounter = 0;
            //}

            //lock(_lock) _pktCounter++;

            // ================================== // 

            //using var fileOut = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), $"pkt-{DateTime.Now.ToFileTime()}.opus"), FileMode.Create);
            //var bytes = pktPayload;

            //var frameSize = OpusPacketInfo.GetNumSamples(_decoder, bytes, 0, bytes.Length);

            //var ms = new MemoryStream(bytes);
            //var inBuf = new byte[frameSize];

            //while (ms.Length - ms.Position >= bytes.Length)
            //{
            //    var pcm = BytesToShorts(inBuf, 0, inBuf.Length);
            //    _decoder.Decode(bytes, 0, bytes.Length, pcm, 0, frameSize, false);
            //    var bytesOut = ShortsToBytes(pcm);
            //    fileOut.Write(bytesOut, 0, bytesOut.Length);
            //}

            //_streamBuffer = new MemoryStream();

            //var oggIn = new OpusOggReadStream(_decoder, new MemoryStream(pktPayload));
            //while (oggIn.HasNextPacket)
            //{
            //    short[] packet = oggIn.DecodeNextPacket();
            //    if (packet != null)
            //    {

            //    }
            //}
        }

        public static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        public static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        internal static byte[] ShortsToBytes(short[] input)
        {
            return ShortsToBytes(input, 0, input.Length);
        }

        internal static byte[] ShortsToBytes(short[] input, int offset, int length)
        {
            byte[] processedValues = new byte[length * 2];
            for (int c = 0; c < length; c++)
            {
                processedValues[c * 2] = (byte)(input[c + offset] & 0xFF);
                processedValues[c * 2 + 1] = (byte)((input[c + offset] >> 8) & 0xFF);
            }

            return processedValues;
        }

        internal static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
