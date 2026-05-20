using System;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    public class AudioRouter
    {
        private WasapiOut? _realSpeakerOut;
        private BufferedWaveProvider? _bufferedWaveProvider;
        private int _captureChannels;
        private int _captureBytesPerSample;
        private int _outputChannels;
        
        // 매 캡처 프레임마다 대량의 byte 배열이 할당되어 GC를 유발하는 현상을 방지하기 위한 공유 캐시 버퍼 (GC Free)
        private byte[] _conversionBuffer = Array.Empty<byte>();

        public void StartRouting(WaveFormat captureFormat)
        {
            var enumerator = new MMDeviceEnumerator();
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // 기본 출력이 실제 장치(Realtek, Voicemeeter 등)이면 오디오가 이미 그쪽으로 재생됩니다.
            // 같은 장치에 WasapiOut까지 열면 WasapiLoopbackCapture와 충돌해 캡처 데이터가 막힙니다.
            // → 라우팅 불필요, 즉시 리턴
            if (!defaultDevice.FriendlyName.Contains("cable", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"ℹ [{defaultDevice.FriendlyName}] 직접 재생 중 — 라우팅 생략");
                return;
            }

            // 기본 출력이 가상 케이블(CABLE Input 등)인 경우:
            // CABLE 자체는 스피커로 소리가 안 나오므로, CABLE 제외 첫 번째 활성 장치에 라우팅합니다.
            var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var realSpeaker = allDevices.FirstOrDefault(d => !d.FriendlyName.Contains("cable", StringComparison.OrdinalIgnoreCase));
            if (realSpeaker == null)
            {
                Console.WriteLine("⚠ CABLE 외 출력 장치를 찾을 수 없습니다 — 라우팅 비활성");
                Console.WriteLine("⚠ 라우팅 비활성 — 시각화/AI는 정상 동작합니다.");
                return;
            }

            // 캡처 포맷 정보 저장 (채널 변환용)
            _captureChannels = captureFormat.Channels;
            _captureBytesPerSample = captureFormat.BitsPerSample / 8;

            // 출력 장치의 Shared 모드 지원 포맷(MixFormat)을 사용
            // captureFormat을 그대로 쓰면 장치가 지원하지 않을 때 Init에서 예외 발생
            var outputFormat = realSpeaker.AudioClient.MixFormat;
            _outputChannels = outputFormat.Channels;

            _bufferedWaveProvider = new BufferedWaveProvider(outputFormat)
            {
                DiscardOnBufferOverflow = true
            };

            // 장치/드라이버마다 허용 latency가 달라 Init 실패가 발생할 수 있어 fallback으로 재시도
            int[] latencies = [50, 100, 200, 500];
            Exception? lastError = null;

            foreach (int latency in latencies)
            {
                try
                {
                    _realSpeakerOut = new WasapiOut(realSpeaker, AudioClientShareMode.Shared, false, latency);
                    _realSpeakerOut.Init(_bufferedWaveProvider);
                    _realSpeakerOut.Play();
                    string mixMode = (_captureChannels == 8 && _outputChannels == 2) ? " [다운믹스]" : "";
                    Console.WriteLine($"🔊 라우팅 시작: [{realSpeaker.FriendlyName}] (캡처: {_captureChannels}ch → 출력: {_outputChannels}ch{mixMode}, latency: {latency}ms)");
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _realSpeakerOut?.Dispose();
                    _realSpeakerOut = null;
                }
            }

            Console.WriteLine($"⚠ 오디오 라우팅 초기화 실패: {lastError?.Message}");
            Console.WriteLine("⚠ 라우팅 비활성 — 시각화/AI는 정상 동작합니다.");
        }

        public void OnDataReceived(object sender, byte[] rawAudioData)
        {
            if (_bufferedWaveProvider == null) return;

            // 캡처-출력 채널 수가 다르면 변환
            if (_captureChannels != _outputChannels)
            {
                int neededSize = ConvertChannels(rawAudioData, _captureChannels, _captureBytesPerSample, _outputChannels, ref _conversionBuffer);
                _bufferedWaveProvider.AddSamples(_conversionBuffer, 0, neededSize);
            }
            else
            {
                _bufferedWaveProvider.AddSamples(rawAudioData, 0, rawAudioData.Length);
            }
        }

        /// <summary>
        /// 채널 수 변환. 8ch→2ch 및 6ch→2ch는 다운믹스, 그 외는 앞쪽 채널 추출/복제 (가비지 할당 0%).
        /// </summary>
        private static int ConvertChannels(byte[] input, int inCh, int bytesPerSample, int outCh, ref byte[] outputBuffer)
        {
            int frames = input.Length / (inCh * bytesPerSample);
            int neededSize = frames * outCh * bytesPerSample;
            if (outputBuffer.Length < neededSize)
            {
                outputBuffer = new byte[neededSize * 2]; // 넉넉하게 2배 스케일링하여 재할당 최소화
            }

            if (inCh == 8 && outCh == 2 && bytesPerSample == 4)
            {
                Downmix8chTo2ch(input, outputBuffer);
                return neededSize;
            }
            if (inCh == 6 && outCh == 2 && bytesPerSample == 4)
            {
                Downmix6chTo2ch(input, outputBuffer);
                return neededSize;
            }

            // 일반 추출/복제 (Span을 이용한 0-allocation 고속 복사)
            var srcSpan = MemoryMarshal.Cast<byte, float>(input.AsSpan());
            var dstSpan = MemoryMarshal.Cast<byte, float>(outputBuffer.AsSpan(0, neededSize));

            for (int i = 0; i < frames; i++)
            {
                for (int ch = 0; ch < outCh; ch++)
                {
                    int srcCh = ch < inCh ? ch : 0;
                    dstSpan[i * outCh + ch] = srcSpan[i * inCh + srcCh];
                }
            }
            return neededSize;
        }

        /// <summary>
        /// 7.1ch float32 → 2ch float32 다운믹스 (ITU-R BS.775 기반 - GC Free)
        /// L = FL + 0.707*FC + 0.707*SL + 0.5*BL
        /// R = FR + 0.707*FC + 0.707*SR + 0.5*BR
        /// </summary>
        private static void Downmix8chTo2ch(byte[] input, byte[] output)
        {
            const float kCenter = 0.707f; // -3dB
            const float kSide   = 0.707f; // -3dB
            const float kBack   = 0.500f; // -6dB

            var src = MemoryMarshal.Cast<byte, float>(input.AsSpan());
            var dst = MemoryMarshal.Cast<byte, float>(output.AsSpan());
            int frames = src.Length / 8;

            for (int i = 0; i < frames; i++)
            {
                int srcOffset = i * 8;
                float fl = src[srcOffset + 0];
                float fr = src[srcOffset + 1];
                float fc = src[srcOffset + 2];
                // srcOffset + 3 (LFE) -> 제외
                float sl = src[srcOffset + 4];
                float sr = src[srcOffset + 5];
                float bl = src[srcOffset + 6];
                float br = src[srcOffset + 7];

                dst[i * 2 + 0] = Math.Clamp(fl + kCenter * fc + kSide * sl + kBack * bl, -1f, 1f);
                dst[i * 2 + 1] = Math.Clamp(fr + kCenter * fc + kSide * sr + kBack * br, -1f, 1f);
            }
        }

        /// <summary>
        /// 5.1ch float32 → 2ch float32 다운믹스 (GC Free)
        /// </summary>
        private static void Downmix6chTo2ch(byte[] input, byte[] output)
        {
            const float kCenter = 0.707f; // -3dB
            const float kSurround = 0.707f; // -3dB

            var src = MemoryMarshal.Cast<byte, float>(input.AsSpan());
            var dst = MemoryMarshal.Cast<byte, float>(output.AsSpan());
            int frames = src.Length / 6;

            for (int i = 0; i < frames; i++)
            {
                int srcOffset = i * 6;
                float fl = src[srcOffset + 0];
                float fr = src[srcOffset + 1];
                float fc = src[srcOffset + 2];
                // LFE는 제외
                float sl = src[srcOffset + 4];
                float sr = src[srcOffset + 5];

                dst[i * 2 + 0] = Math.Clamp(fl + kCenter * fc + kSurround * sl, -1f, 1f);
                dst[i * 2 + 1] = Math.Clamp(fr + kCenter * fc + kSurround * sr, -1f, 1f);
            }
        }

        public void StopRouting()
        {
            if (_realSpeakerOut != null)
            {
                _realSpeakerOut.Stop();
                _realSpeakerOut.Dispose();
                _bufferedWaveProvider = null;
                Console.WriteLine("🛑 오디오 출력 라우팅 중지.");
            }
        }
    }
}