using System;
using System.Linq;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace SoundVisualizer.CoreAudio
{
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
        private WasapiLoopbackCapture? _captureDevice;
        private bool _isCapturing;

        public event EventHandler<AudioDataAvailableEventArgs>? OnAudioDataAvailable;
        public event EventHandler<string>? OnCaptureError;

        public WaveFormat? CaptureFormat => _captureDevice?.WaveFormat;
        public bool IsCapturing => _isCapturing;

        public void StartCapture()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                if (devices.Count == 0)
                {
                    string msg = "🚨 출력 장치를 찾을 수 없습니다.";
                    Console.WriteLine(msg);
                    OnCaptureError?.Invoke(this, msg);
                    return;
                }

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

                    byte[] validData = new byte[args.BytesRecorded];
                    Array.Copy(args.Buffer, validData, args.BytesRecorded);

                    var eventArgs = new AudioDataAvailableEventArgs(validData, _captureDevice.WaveFormat.Channels);
                    OnAudioDataAvailable?.Invoke(this, eventArgs);
                };

                _captureDevice.RecordingStopped += (sender, args) =>
                {
                    _isCapturing = false;

                    if (args.Exception != null)
                    {
                        string msg = $"🚨 캡처 비정상 종료: {args.Exception.Message}";
                        Console.WriteLine(msg);
                        OnCaptureError?.Invoke(this, msg);
                    }
                    else
                    {
                        Console.WriteLine("🛑 오디오 캡처 정상 종료.");
                    }
                };

                _captureDevice.StartRecording();
                _isCapturing = true;
                Console.WriteLine($"🔥 오디오 후킹 시작: {targetDevice.FriendlyName}");
                Console.WriteLine($"📊 감지된 채널 수: {_captureDevice.WaveFormat.Channels}채널");
            }
            catch (Exception ex)
            {
                _isCapturing = false;
                string msg = $"🚨 캡처 엔진 시작 실패: {ex.Message}";
                Console.WriteLine(msg);
                OnCaptureError?.Invoke(this, msg);
            }
        }

        public void StopCapture()
        {
            try
            {
                if (_captureDevice != null)
                {
                    _captureDevice.StopRecording();
                    _captureDevice.Dispose();
                    _captureDevice = null;
                    Console.WriteLine("🛑 오디오 후킹 중지됨.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 캡처 중지 중 오류: {ex.Message}");
            }
            finally
            {
                _isCapturing = false;
            }
        }
    }
}