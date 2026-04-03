using System;
using System.Linq;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
    // 수정됨: 오디오 데이터와 채널 정보를 함께 담을 이벤트 인수 클래스 추가
    public class AudioDataAvailableEventArgs : EventArgs
    {
        public byte[] Buffer { get; set; }
        public int Channels { get; set; }

        public AudioDataAvailableEventArgs(byte[] buffer, int channels)
        {
            Buffer = buffer;
            Channels = channels;
        }
    }

    public class AudioCaptureEngine
    {
        private WasapiLoopbackCapture _captureDevice;

        // 수정됨: 이벤트 타입을 새로 만든 AudioDataAvailableEventArgs 로 변경
        public event EventHandler<AudioDataAvailableEventArgs> OnAudioDataAvailable;

        public void StartCapture()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var targetDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains("CABLE Input"));

            if (targetDevice == null)
            {
                Console.WriteLine("⚠ 가상 케이블이 없습니다. 기본 출력 장치로 대체합니다.");
                targetDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            _captureDevice = new WasapiLoopbackCapture(targetDevice);

            _captureDevice.DataAvailable += (sender, args) =>
            {
                if (args.BytesRecorded == 0) return;

                // 수정됨: 바이트 배열과 채널 수를 묶어서 이벤트 발생
                var eventArgs = new AudioDataAvailableEventArgs(args.Buffer, _captureDevice.WaveFormat.Channels);
                OnAudioDataAvailable?.Invoke(this, eventArgs);
            };

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