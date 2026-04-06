using System;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    public class AudioRouter
    {
        private WasapiOut? _realSpeakerOut;
        private BufferedWaveProvider? _bufferedWaveProvider;

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

            Console.WriteLine($"🔊 라우팅 시작: 오디오 데이터를 [{realSpeaker.FriendlyName}]로 출력합니다.");
        }

        // 오디오 데이터가 수신될 때마다 호출되는 이벤트 핸들러
        public void OnDataReceived(object sender, byte[] rawAudioData)
        {
            if (_bufferedWaveProvider != null)
            {
                // [참고] 다채널 데이터를 스테레오 환경에 맞게 다운믹싱하는 로직이 향후 필요할 수 있습니다.
                // 현재는 수신된 데이터를 버퍼에 직접 추가하여 출력을 테스트합니다.
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
                Console.WriteLine("🛑 오디오 출력 라우팅 중지.");
            }
        }
    }
}