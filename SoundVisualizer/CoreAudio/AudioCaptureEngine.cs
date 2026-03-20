using System;
using System.Linq;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    public class AudioCaptureEngine
    {
        private WasapiLoopbackCapture _captureDevice;

        // 🚀 가로챈 오디오 데이터를 외부(UI, AI, DSP)로 쏴줄 이벤트
        public event EventHandler<byte[]> OnAudioDataAvailable;

        public void StartCapture()
        {
            // 1. 현재 PC에 꽂혀있는 스피커/이어폰 목록 싹 다 스캔 (Device Enumeration)
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            // 2. 타겟 장치 찾기: "CABLE Input" (가상 7.1 케이블)
            var targetDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input"));

            if (targetDevice == null)
            {
                // [루트 2 예외처리] 만약 유저 PC에 가상 케이블이 없으면? 일단 기본 스피커를 훔친다!
                Console.WriteLine("⚠️ 가상 케이블이 없습니다. 기본 출력 장치로 대체합니다.");
                targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            // 3. 타겟 장치에 '도청기(Loopback Capture)' 달기
            _captureDevice = new WasapiLoopbackCapture(targetDevice);

            // 4. 0.01초 단위로 오디오 버퍼(소리 데이터)가 쏟아져 들어오는 댐의 수문!
            _captureDevice.DataAvailable += (sender, args) =>
            {
                if (args.BytesRecorded == 0) return;

                // args.Buffer가 바로 우리가 훔친 8채널 원본 데이터입니다.
                // 이 데이터를 구독하고 있는 애들(가람, 성진, 라우팅 스레드)한테 뿌려줍니다.
                OnAudioDataAvailable?.Invoke(this, args.Buffer);
            };

            // 5. 엔진 가동!
            _captureDevice.StartRecording();
            Console.WriteLine($"🔥 오디오 후킹 시작: {targetDevice.FriendlyName}");
            Console.WriteLine($"📊 감지된 채널 수: {_captureDevice.WaveFormat.Channels}채널");
        }

        public void StopCapture()
        {
            if (_captureDevice != null)
            {
                _captureDevice.StopRecording();
                _captureDevice.Dispose();
                Console.WriteLine("🛑 오디오 후킹 중지됨.");
            }
        }
    }
}