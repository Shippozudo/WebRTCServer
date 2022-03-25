using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using System;
using System.IO;

namespace Signaler
{
    public class Opusenc
    {
        private int _pktCounter = 0;
        private object _lock = new { };

        private Stream _streamBuffer;
        private readonly OpusEncoder _encoder = OpusEncoder.Create(48000, 2, OpusApplication.OPUS_APPLICATION_VOIP);

        public Opusenc()
        {
            _encoder.Complexity = 0;
            _encoder.Bitrate = 128000;
            _encoder.UseInbandFEC = true;
            _encoder.PacketLossPercent = 0;
            _encoder.EnableAnalysis = true;
            _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
           


            _streamBuffer = new MemoryStream();
        }

        public void EncodeRawAudioPacket(byte[] pktPayload)
        {
            _streamBuffer.Write(pktPayload, 0, pktPayload.Length);

            if (_pktCounter == 30)
            {
                using var fileOut = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), $"pkt-{DateTime.Now.ToFileTime()}.opus"), FileMode.Create);

                var tags = new OpusTags();

                tags.Fields[OpusTagName.Title] = "A";
                tags.Fields[OpusTagName.Artist] = "B";

                var oggOut = new OpusOggWriteStream(_encoder, fileOut, tags);

                short[] samples = BytesToShorts(((MemoryStream)_streamBuffer).ToArray());

                oggOut.WriteSamples(samples, 0, samples.Length);
                oggOut.Finish();

                _streamBuffer = new MemoryStream();
                _pktCounter = 0;
            }

            lock (_lock) _pktCounter++;
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
    }
}
