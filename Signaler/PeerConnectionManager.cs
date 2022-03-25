using Concentus.Oggfile;
using Concentus.Structs;
using FFMpegCore;
using FFMpegCore.Pipes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Signaler.Hubs;
using Signaler.Models;
using SIPSorcery.Net;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Signaler.Mixer;

namespace Signaler
{
    /*
       Instantiate the RTCPeerConnection instance,
       Add the audio and/or video tracks as required,
       Call the createOffer method to acquire an SDP offer that can be sent to the remote peer,
      
    
       Send the SDP offer and get the SDP answer from the remote peer (this exchange is not part of the WebRTC specification and can be done using any signalling layer, examples are SIP, web sockets etc),
       Once the SDP exchange has occurred the ICE checks can start in order to establish the optimal network path between the two peers. ICE candidates typically need to be passed between peers using the signalling layer,
       Once ICE has established a the DTLS handshake will occur,,
       If the DTLS handshake is successful the keying material it produces is used to initialise the SRTP contexts,
       After the SRTP contexts are initialised the RTP media and RTCP packets can be exchanged in the normal manner.
     */
    public class PeerConnectionManager : IPeerConnectionManager
    {
        private readonly IHubContext<WebRTCHub> _webRTCHub;
        private readonly ILogger<PeerConnectionManager> _logger;

        private readonly Mixer _mixer;
       // private readonly Opusenc _opusenc = new Opusenc();
        //private readonly Opusdec _opudec = new Opusdec();

        private readonly List<(ushort, string)> _connectedUsers; //denis

        private AudioExtrasSource _audioExtrasSource;

        public event Action _onSendFromAudioStreamComplete;
        public event Action OnSendFromAudioStreamComplete;

        public int _audioSamplePeriodMilliseconds = 20;

        public event EncodedSampleDelegate OnAudioSourceEncodedSample;

        private int i = 0;
        private readonly object _lock = new { };

        private ConcurrentDictionary<string, List<RTCIceCandidate>> _candidates = new();
        private ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();

        private static RTCConfiguration _config = new()
        {
            X_UseRtpFeedbackProfile = true,
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer
                {
                    urls = "stun:stun1.l.google.com:19302"
                },
                new RTCIceServer
                {
                    username = "webrtc",
                    credential = "webrtc",
                    credentialType = RTCIceCredentialType.password,
                    urls = "turn:turn.anyfirewall.com:443?transport=tcp"
                },
            }
        };


        private static FFOptions options = new()
        {
            UseCache = true,
            TemporaryFilesFolder = @"C:\temp",
            BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
        };

        public object WindowsAudioEndPoint { get; private set; }

        public PeerConnectionManager(ILogger<PeerConnectionManager> logger, IHubContext<WebRTCHub> webRTCHub, Mixer mixer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webRTCHub = webRTCHub ?? throw new ArgumentNullException(nameof(webRTCHub));
            _peerConnections ??= new ConcurrentDictionary<string, RTCPeerConnection>();
            _mixer = mixer;
            _audioExtrasSource = new AudioExtrasSource();

            Task.Run(_mixer.StartAudioProcess);
        }

        public async Task<RTCSessionDescriptionInit> CreateServerOffer(string id)
        {

            var peerConnection = new RTCPeerConnection(_config);

            var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false,
                         //new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2, "minptime=20;maxptime=50;useinbandfec=1;")) }, MediaStreamStatusEnum.SendRecv);
                         new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU) }, MediaStreamStatusEnum.SendRecv);

            peerConnection.addTrack(audioTrack);


            peerConnection.onicegatheringstatechange += (RTCIceGatheringState obj) =>
            {
                if (peerConnection.signalingState == RTCSignalingState.have_local_offer ||
                    peerConnection.signalingState == RTCSignalingState.have_remote_offer)
                {
                    var candidates = _candidates.Where(x => x.Key == id).SingleOrDefault().Value;
                    foreach (var candidate in candidates)
                    {
                        _webRTCHub.Clients.All.SendAsync("IceCandidateResult", candidate).GetAwaiter().GetResult();
                    }
                }
            };

            peerConnection.onicecandidate += (candidate) =>
            {
                if (peerConnection.signalingState == RTCSignalingState.have_local_offer ||
                    peerConnection.signalingState == RTCSignalingState.have_remote_offer)
                {
                    var candidatesList = _candidates.Where(x => x.Key == id).SingleOrDefault();
                    if (candidatesList.Value is null)
                        _candidates.TryAdd(id, new List<RTCIceCandidate> { candidate });
                    else
                        candidatesList.Value.Add(candidate);
                }
            };

            var offerSdp = peerConnection.createOffer(null);
            await peerConnection.setLocalDescription(offerSdp);
            _peerConnections.TryAdd(id, peerConnection);
            return offerSdp;
        }


        public void SetAudioRelay(RTCPeerConnection peerConnection, string connectionId, IList<User> usersToRelay)
        {
            peerConnection.OnRtpPacketReceived += (rep, media, pkt) =>
            {
                _mixer.AddRawPacket(pkt.Payload);
                _mixer.AddRawPacket(pkt);
                //_mixer.FFMpegAmix();
                _mixer.FFMpegAmerge();


                _mixer.HasAudioData += (object e, TesteEventArgs args) =>
                {
                    foreach (var user in usersToRelay)
                    {
                        //user.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, args.bytes, args.Timestamp, 0, 0);
                        user.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                    }

                };

               // _audioExtrasSource.StartAudio();
                

            };


        }

        public void SetRemoteDescription(string id, RTCSessionDescriptionInit rtcSessionDescriptionInit)
        {
            if (!_peerConnections.TryGetValue(id, out var pc)) return;
            pc.setRemoteDescription(rtcSessionDescriptionInit);
        }

        public void AddIceCandidate(string id, RTCIceCandidateInit iceCandidate)
        {
            if (!_peerConnections.TryGetValue(id, out var pc)) return;
            pc.addIceCandidate(iceCandidate);
        }


        public RTCPeerConnection Get(string id)
        {
            var pc = _peerConnections.Where(p => p.Key == id).SingleOrDefault();
            if (pc.Value != null) return pc.Value;
            return null;
        }


    }

}

