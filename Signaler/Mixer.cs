using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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
            //Task.Run(Merge2Audio);
            Task.Run(ProcessRTPPacket);
            Task.Run(FFMpegAmix);
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



        public void FFMpegAmix()
        {
            var ffOptions = new FFOptions
            {
                BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
                TemporaryFilesFolder = @"C:\temp\wavs\"
            };

            FFMpegArguments audioFileInput = null;

            var audioFileNames = Directory.GetFiles(@"C:\temp\wavs\audio");
            var audioDurations = new List<TimeSpan>();

            foreach (var filename in audioFileNames)
            {
                var bytes = File.ReadAllBytes(filename);
                var audioStream = new MemoryStream(bytes);
                var audioAnalysis = FFProbe.Analyse(audioStream, int.MaxValue, ffOptions);
                audioDurations.Add(audioAnalysis.Duration);

                if (audioFileInput == null)
                {
                    audioFileInput = FFMpegArguments.
                        FromFileInput(filename, true, options =>
                         {
                             options.WithDuration(audioAnalysis.Duration);
                         });

                }
                else
                {
                    audioFileInput
                        .AddFileInput(filename, true, options =>
                        {
                            options.WithDuration(audioAnalysis.Duration);
                        });
                }

            }
            var outputAudioStream = new MemoryStream();
            audioFileInput
                                   .OutputToPipe(new StreamPipeSink(outputAudioStream), options =>
                                   {
                                       options.ForceFormat("mp3");
                                       options.WithCustomArgument(@$"-filter_complex amix=inputs={audioDurations.Count}:duration=longest -ac 2 -vol 256"); //256 = normal volume
                                   })
                                .NotifyOnOutput((str, dt) =>
                                {
                                    Console.WriteLine(str);
                                })
                                .ProcessSynchronously(true, ffOptions);


            FileStream fileStream = File.Create(@"C:\temp\wavs\audio\output.mp3"); //FileName.FormatoDesejado
            fileStream.Write(outputAudioStream.GetBuffer(), 0, (int)outputAudioStream.Length);

            fileStream.Flush();
            fileStream.Close();


        }


        public void Merge2Audio() //substituido pelo FFMpeg amix
        {

            try
            {
                var audioFilenames = Directory.GetFiles(@"C:\temp\wavs\audio");
                var ffOptions = new FFOptions
                {
                    BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
                    TemporaryFilesFolder = @"C:\temp\wavs\"
                };
                var audioDurations = new List<TimeSpan>();

                FFMpegArguments audioPipeline = null;

                foreach (var filename in audioFilenames)
                {
                    var bytes = File.ReadAllBytes(filename);
                    var audioStream = new MemoryStream(bytes);
                    var audioAnalysis = FFProbe.Analyse(audioStream, int.MaxValue, ffOptions);
                    audioDurations.Add(audioAnalysis.Duration);

                    if (audioPipeline == null)
                    {
                        audioPipeline = FFMpegArguments
                            .FromPipeInput(new StreamPipeSource(audioStream), options =>
                            {
                                options.WithDuration(audioAnalysis.Duration);
                            });
                    }
                    else
                    {
                        audioPipeline
                            .AddPipeInput(new StreamPipeSource(audioStream), options =>
                            {
                                options.WithDuration(audioAnalysis.Duration);
                            });
                    }
                }

                var outputAudioStream = new MemoryStream();

                audioPipeline
                    .OutputToPipe(new StreamPipeSink(outputAudioStream), options =>
                    {
                        options.WithDuration(audioDurations.OrderByDescending(d => d.TotalMilliseconds).FirstOrDefault());
                        options.ForceFormat("mp3");
                        options.WithCustomArgument(@$"-filter_complex amerge=inputs={audioDurations.Count} -ac 2");
                    })
                    .NotifyOnOutput((str, dt) =>
                    {
                        Console.WriteLine(str);
                    })
                    .ProcessSynchronously(true, ffOptions);

                FileStream fileStream = File.Create(@"C:\temp\wavs\audio\mixResult.mp3");
                fileStream.Write(outputAudioStream.GetBuffer(), 0, (int)outputAudioStream.Length);
                fileStream.Flush();
                fileStream.Close();


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
                    for (int i = 0; i < 2500; i++) // 5000 = 10s gravação
                    {
                        tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());
                        reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                        using var convertedstream = WaveFormatConversionStream.CreatePcmStream(reader);
                        WaveFileWriter.CreateWaveFile(@"c:\\temp\\wavs\\teste" + fileCount.ToString() + ".mp3", convertedstream);

                        //    ////salva file .raw
                        //    //using var fstream = new FileStream(@"C:\\temp\\wavs\\teste" + fileCount.ToString() + ".raw", FileMode.OpenOrCreate);
                        //    //tmpMemStream.CopyTo(fstream);
                        //    //fstream.Flush();

                    }
                    fileCount++;

                    //tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());
                    ////salva file.wav
                    //reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                    //using var convertedStream = WaveFormatConversionStream.CreatePcmStream(reader);
                    //WaveFileWriter.CreateWaveFile(@"C:\\temp\\wavs\\Wav.wav", convertedStream);


                    //salva file.raw
                    //using var fstream = new FileStream(@"C:\\temp\\wavs\\Raw.raw", FileMode.OpenOrCreate);
                    //tmpMemStream.CopyTo(fstream);
                    //fstream.Flush();
                    //tmpMemStream.Flush();



                }

                _audioBuffer = new MemoryStream(); // RESETA O BUFFER
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

