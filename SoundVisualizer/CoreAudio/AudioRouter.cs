using System;
using System.Linq;
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

        public void StartRouting(WaveFormat captureFormat)
        {
            var enumerator = new MMDeviceEnumerator();
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // 기본 출력이 실제 장치(Realtek, Voicemeeter 등)이면 오디오가 이미 그쪽으로 재생됩니다.
            // 같은 장치에 WasapiOut까지 열면 WasapiLoopbackCapture와 충돌해 캡처 데이터가 막힙니다.
            // → 라우팅 불필요, 즉시 리턴
            if (!defaultDevice.FriendlyName.Contains("CABLE"))
            {
                Console.WriteLine($"ℹ [{defaultDevice.FriendlyName}] 직접 재생 중 — 라우팅 생략");
                return;
            }

            // 기본 출력이 가상 케이블(CABLE Input 등)인 경우:
            // CABLE 자체는 스피커로 소리가 안 나오므로, CABLE 제외 첫 번째 활성 장치에 라우팅합니다.
            var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            var realSpeaker = allDevices.FirstOrDefault(d => !d.FriendlyName.Contains("CABLE"));
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
                    Console.WriteLine($"🔊 라우팅 시작: [{realSpeaker.FriendlyName}] (캡처: {_captureChannels}ch → 출력: {_outputChannels}ch, latency: {latency}ms)");
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
                byte[] converted = ConvertChannels(rawAudioData, _captureChannels, _captureBytesPerSample, _outputChannels);
                _bufferedWaveProvider.AddSamples(converted, 0, converted.Length);
            }
            else
            {
                _bufferedWaveProvider.AddSamples(rawAudioData, 0, rawAudioData.Length);
            }
        }

        /// <summary>
        /// 채널 수 변환. 입력 채널이 더 많으면 앞쪽 채널 추출, 적으면 첫 채널 복제.
        /// </summary>
        private static byte[] ConvertChannels(byte[] input, int inCh, int bytesPerSample, int outCh)
        {
            int frames = input.Length / (inCh * bytesPerSample);
            byte[] output = new byte[frames * outCh * bytesPerSample];

            for (int i = 0; i < frames; i++)
            {
                for (int ch = 0; ch < outCh; ch++)
                {
                    int srcCh = ch < inCh ? ch : 0;
                    Buffer.BlockCopy(
                        input, (i * inCh + srcCh) * bytesPerSample,
                        output, (i * outCh + ch) * bytesPerSample,
                        bytesPerSample);
                }
            }
            return output;
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