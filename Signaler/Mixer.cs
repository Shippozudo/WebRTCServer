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
        /// 

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
            //Task.Run(FFMpegAmerge);
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


        //FFMpegArguments audioPipeline = null;
        //int count = 1;
        //public void FFMpegAmerge()
        //{
        //    var audioStreamDefault = new MemoryStream();
        //    var audioStream = new MemoryStream();


        //    try
        //    {
        //        //var audioFilenames = @"C:\temp\wavs\audio\123.m4a";
        //        //var readByteFromFile = File.ReadAllBytes(audioFilenames);
        //        //audioStreamDefault = new MemoryStream(readByteFromFile);

        //        var ffOptions = new FFOptions
        //        {
        //            BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
        //            TemporaryFilesFolder = @"C:\temp\wavs"
        //        };
        //        var audioDurations = new List<TimeSpan>();

        //        audioStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());

        //        //var audioAnalysis = FFProbe.Analyse(audioStream, int.MaxValue, ffOptions);
        //        //audioDurations.Add(audioAnalysis.Duration);

        //        Task.Delay(20000);
        //        {
        //            if (audioPipeline is null)
        //            {
        //                audioPipeline = FFMpegArguments
        //                    .FromPipeInput(new StreamPipeSource(audioStream), options =>
        //                    {
        //                    });
        //                count++;
        //            }
        //            else
        //            {
        //                audioPipeline
        //                    .AddPipeInput(new StreamPipeSource(audioStream), options =>
        //                    {
        //                    });

        //                count++;

        //                if (count > 1500)
        //                {
        //                    var outputAudioStream = new MemoryStream();
        //                    audioPipeline
        //                        .OutputToPipe(new StreamPipeSink(outputAudioStream), options =>
        //                        {
        //                            options.ForceFormat("mp3");
        //                            options.WithCustomArgument(@$"-filter_complex amix=inputs={count}-ac 2 -vol 256");
        //                        })
        //                        .NotifyOnOutput((str, dt) =>
        //                        {
        //                            Console.WriteLine(str);
        //                        })
        //                        .ProcessSynchronously(true, ffOptions);

        //                    FileStream filestream = File.Create(@$"c:\temp\wavs\OutStream.mp3");
        //                    var file = @$"c:\temp\wavs\OutStream.mp3";

        //                    filestream.Write(outputAudioStream.GetBuffer(), 0, (int)outputAudioStream.Length);
        //                    filestream.Flush();
        //                    filestream.Close();
        //                    count = 1;
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex);
        //    }
        //}

        int i = 0;
        int outPutCount = 1;
        int audioRecordCounter = 5;

        public void FFMpegAmix()
        {
            var outputAudioStream = new MemoryStream();
            FFMpegArguments audioFileInput = null;
            //List<TimeSpan> audioDurations = new List<TimeSpan>();

            var ffOptions = new FFOptions
            {
                BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
                TemporaryFilesFolder = @"C:\temp\wavs\"
            };

            var audioFile = Directory.GetFiles(@"C:\temp\wavs\record");
            audioFile = audioFile.OrderBy(x => x[x.Length - 9]).ToArray();

            if (audioFile.Count() > audioRecordCounter)
            {
                audioFileInput = null;

                for (; i < audioRecordCounter; i++)
                {
                    var bytes = File.ReadAllBytes(audioFile[i]);
                    var audioStream = new MemoryStream(bytes);
                    //var audioAnalysis = FFProbe.Analyse(audioStream, int.MaxValue, ffOptions);
                    //audioDurations.Add(audioAnalysis.Duration);

                    if (audioFileInput == null)
                    {
                        audioFileInput = FFMpegArguments.
                            FromFileInput(audioFile[i], true, options =>
                            {
                                //options.WithDuration(audioAnalysis.Duration);
                            });
                    }
                    else
                    {
                        audioFileInput
                            .AddFileInput(audioFile[i], true, options =>
                            {
                                //options.WithDuration(audioAnalysis.Duration);
                            });
                    }
                    if (i == audioRecordCounter - 1)
                    {
                        outputAudioStream = new MemoryStream();
                        audioFileInput
                                   .OutputToPipe(new StreamPipeSink(outputAudioStream), options =>
                                   {
                                       options.ForceFormat("mp3");
                                       options.WithCustomArgument(@$"-filter_complex amix=inputs=5:duration=longest -ac 2 -vol 256"); //256 = normal volume
                                   })
                                   .NotifyOnOutput((str, dt) =>
                                   {
                                       Console.WriteLine(str);
                                   })
                                   .ProcessSynchronously(true, ffOptions);


                        FileStream fileStream = File.Create(@$"C:\temp\wavs\output\Output0{outPutCount}.mp3");
                        fileStream.Write(outputAudioStream.GetBuffer(), 0, (int)outputAudioStream.Length);
                        fileStream.Flush();
                        fileStream.Close();

                        audioRecordCounter += 5;
                        outPutCount++;

                        break;

                    }
                }
            }
        }
        private void ProcessAudio()
        {
            int fileCount = 1;
            int audioRecordCounter = 5;
            var waveFormat = WaveFormat.CreateMuLawFormat(8000, 1);
            var tmpMemStream = new MemoryStream();
            var reader = new RawSourceWaveStream();

            while (true)
            {
                if (_audioBuffer.Length > 0)
                {
                    Task.Delay(100).Wait();
                    tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());
                    reader = new RawSourceWaveStream(tmpMemStream, waveFormat);
                    using var convertedstream = WaveFormatConversionStream.CreatePcmStream(reader);
                    WaveFileWriter.CreateWaveFile(@$"C:\temp\wavs\record\RecordFile" + fileCount.ToString() + ".mp3", convertedstream);


                    var audioFileNames = Directory.GetFiles(@"C:\temp\wavs\Record");
                    if (audioFileNames.Count() > audioRecordCounter)
                    {
                        FFMpegAmix();

                        audioRecordCounter += 5;
                    }

                    fileCount++;
                    //tmpMemStream = new MemoryStream((_audioBuffer as MemoryStream).ToArray());

                    //salva file.wav
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



