using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    public class AudioRouter
    {
        private WasapiOut _realSpeakerOut;
        private BufferedWaveProvider _bufferedWaveProvider;

        public void StartRouting(WaveFormat captureFormat)
        {
            // 1. 유저가 진짜로 듣고 있는 '기본 스피커/이어폰' 찾기
            var enumerator = new MMDeviceEnumerator();
            var realSpeaker = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            // ⚠️ 여기서 주의! 만약 가상 케이블과 진짜 스피커가 같으면 무한 루프(하울링) 돕니다.
            if (realSpeaker.FriendlyName.Contains("CABLE Input"))
            {
                Console.WriteLine("🚨 에러: 진짜 스피커를 찾아야 하는데 가상 케이블이 잡혔습니다!");
                return;
            }

            // 2. 소리를 담아둘 '물탱크(Buffer)' 만들기
            // 캡처한 원본(7.1채널) 포맷을 그대로 받아올 수 있게 세팅합니다.
            _bufferedWaveProvider = new BufferedWaveProvider(captureFormat)
            {
                DiscardOnBufferOverflow = true // 물탱크가 넘치면 옛날 소리는 버림 (딜레이 방지 핵심!)
            };

            // 3. 진짜 스피커로 출력할 WasapiOut 엔진 가동
            // Latency 파라미터(예: 50ms)를 최대한 짧게 줘야 총 쏘자마자 소리가 들립니다.
            _realSpeakerOut = new WasapiOut(realSpeaker, AudioClientShareMode.Shared, false, 50);
            _realSpeakerOut.Init(_bufferedWaveProvider);
            _realSpeakerOut.Play();

            Console.WriteLine($"🔊 라우팅 시작: 훔친 소리를 [{realSpeaker.FriendlyName}]로 쏴줍니다!");
        }

        // 🚀 AudioCaptureEngine에서 소리가 쏟아질 때마다 이 함수가 호출됨
        public void OnDataReceived(object sender, byte[] rawAudioData)
        {
            if (_bufferedWaveProvider != null)
            {
                // [도전 과제] 사실 여기서 8채널(rawAudioData)을 2채널로 섞어주는(Downmix) 수학적 작업이 필요합니다.
                // 일단은 물탱크에 냅다 부어버려서 소리가 이어폰으로 나가는지부터 테스트!
                _bufferedWaveProvider.AddSamples(rawAudioData, 0, rawAudioData.Length);
            }
        }

        public void StopRouting()
        {
            if (_realSpeakerOut != null)
            {
                _realSpeakerOut.Stop();
                _realSpeakerOut.Dispose();
                _bufferedWaveProvider = null;
                Console.WriteLine("🛑 라우팅 중지됨.");
            }
        }
    }
}