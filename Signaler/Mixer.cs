using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SIPSorcery.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Signaler
{
    public class Mixer
    {
        private readonly ILogger<Mixer> _logger;

        private uint _timestamp;
        private Stream _audioBuffer;
        private ConcurrentQueue<RTPPacket> _queue;
        private readonly RTCPeerConnection _peerConnection; // adicionei

        public Mixer(ILogger<Mixer> logger)
        {
            _logger = logger;
            _audioBuffer = new MemoryStream();
            _queue = new ConcurrentQueue<RTPPacket>();
            _timestamp = 1234587;
            _peerConnection = new RTCPeerConnection(); // adicionei
        }

        /// <summary>
        ///     Adiciona um pacote ao buffer
        /// </summary>
        public void AddRawPacket(byte[] pkt)
        {
            _audioBuffer.Write(pkt, 0, pkt.Length);
        }

        public void AddRawPacket(RTPPacket pk)
        {
            _queue.Enqueue(pk);
        }

        public void StartAudioProcess()
        {
            Task.Run(ProcessAudio);
            Task.Run(Merge2Audio);
            Task.Run(ProcessRTPPacket);
        }


        private void ProcessRTPPacket()
        {
            while (true)
            {
                Task.Delay(10); //original 10

                if (!_queue.IsEmpty)
                {
                    try
                    {
                        using var tmpStream = new MemoryStream();
                        //using var tmpStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());

                        while (_queue.TryDequeue(out var pkt))
                            tmpStream.Write(pkt.Payload, 0, pkt.Payload.Length);
                                             

                        var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
                        var reader = new RawSourceWaveStream(tmpStream, waveFormat);

                        using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);

                        var bytes = new byte[convertedStream.Length];
                        convertedStream.Read(bytes, 0, (int)convertedStream.Length);
                        HasAudioData.Invoke(this, new TesteEventArgs { bytes = bytes, Timestamp = _timestamp++ });

                        Task.Delay(10);

                    }
                    catch (Exception e)
                    {

                    }
                }
            }
        }

        public EventHandler<TesteEventArgs> HasAudioData;

        public class TesteEventArgs : EventArgs
        {
            public byte[] bytes { get; set; }
            public uint Timestamp { get; set; }
        }

        public void Merge2Audio()
        {

            try
            {
                var options = new FFOptions
                {
                    BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
                    TemporaryFilesFolder = @"C:\temp\wavs\"
                };

                var audio1 = File.ReadAllBytes(@"C:\temp\wavs\mix1mp3.mp3");
                var audio2 = File.ReadAllBytes(@"C:\temp\wavs\mix2mp3.mp3");

                var audioStream1 = new MemoryStream(audio1);
                var audioStream2 = new MemoryStream(audio2);

                var mediaAnalisys = FFProbe.Analyse(audioStream1, int.MaxValue, options);
                var mediaAnalisys2 = FFProbe.Analyse(audioStream2, int.MaxValue, options);

                var durationAudio1 = mediaAnalisys.Duration;
                var durationAudio2 = mediaAnalisys2.Duration;

                var durations = new List<TimeSpan> { durationAudio1, durationAudio2 };


                audioStream1.Position = 0;
                audioStream2.Position = 0;

                var outStream = new MemoryStream();

                FFMpegArguments
                    .FromPipeInput(new StreamPipeSource(audioStream1), options =>
                    {
                        options.WithDuration(durationAudio1);
                    })
                    .AddPipeInput(new StreamPipeSource(audioStream2), options =>
                    {
                        options.WithDuration(durationAudio2);
                    })
                    .OutputToPipe(new StreamPipeSink(outStream), options =>
                    {
                        options.WithDuration(durations.OrderByDescending(d => d.TotalMilliseconds).First());
                        options.ForceFormat("mp3");
                        options.WithCustomArgument(@"-filter_complex amerge=inputs=2 -ac 2");
                    })
                    .NotifyOnOutput((str, dt) =>
                    {
                        Console.WriteLine(str);
                    })
                    .ProcessSynchronously(true, options);


                using FileStream fl = File.Create(@"C:\temp\wavs\mixResult.mp3");
                fl.Write(outStream.GetBuffer(), 0, (int)outStream.Length);
                fl.Close();
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex);
            }
        }

        private void ProcessAudio()
        {
            int fileCount = 1;
            var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
            var tmpMemStream = new MemoryStream();
            var reader = new RawSourceWaveStream();

            while (true)
            {

                Task.Delay(0);
                if (_audioBuffer.Length > 0)
                {
                    //for (int i = 0; i < 5000; i++) // 5000 = 10s gravação
                    //{
                    //    //tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());
                    //    //reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                    //    //using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    //    //WaveFileWriter.CreateWaveFile(@"C:\\temp\\wavs\\teste" + fileCount.ToString() + ".wav", convertedStream);

                    //    ////salva file .raw
                    //    //using var fstream = new FileStream(@"C:\\temp\\wavs\\teste" + fileCount.ToString() + ".raw", FileMode.OpenOrCreate);
                    //    //tmpMemStream.CopyTo(fstream);
                    //    //fstream.Flush();


                    //}

                    tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());


                    //salva file.raw
                    //using var fstream = new FileStream(@"C:\\temp\\wavs\\Raw.raw", FileMode.OpenOrCreate);
                    //tmpMemStream.CopyTo(fstream);
                    //fstream.Flush();
                    //tmpMemStream.Flush();

                    //salva file.wav
                    reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                    using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    WaveFileWriter.CreateWaveFile(@"C:\\temp\\wavs\\Wav.wav", convertedStream);

                }

                //_audioBuffer = new MemoryStream(); // RESETA O BUFFER
            }
        }
    }
}

public class RawSourceWaveStream : WaveStream
{
    private Stream sourceStream;
    private WaveFormat waveFormat;


    public RawSourceWaveStream()
    {

    }
    public RawSourceWaveStream(Stream sourceStream, WaveFormat waveFormat)
    {
        this.sourceStream = sourceStream;
        this.waveFormat = waveFormat;
    }

    public override WaveFormat WaveFormat
    {
        get { return this.waveFormat; }
    }

    public override long Length
    {
        get { return this.sourceStream.Length; }
    }

    public override long Position
    {
        get
        {
            return this.sourceStream.Position;
        }
        set
        {
            this.sourceStream.Position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return sourceStream.Read(buffer, offset, count);
    }
}

