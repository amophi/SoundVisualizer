using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundVisualizer.AIModel
{
    /// <summary>
    /// WAV(또는 AudioFileReader가 읽을 수 있는 형식) → 모노 IEEE float, 목표 샘플레이트.
    /// </summary>
    public static class WavAudioLoader
    {
        public const int TargetSampleRate = 16000;

        /// <summary>
        /// 파일에서 모노 16kHz float 샘플을 읽습니다.
        /// </summary>
        public static float[] LoadMono16kHz(string filePath)
        {
            using var reader = new AudioFileReader(filePath);
            ISampleProvider sampleProvider = reader.ToSampleProvider();

            if (sampleProvider.WaveFormat.Channels > 1)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }

            if (sampleProvider.WaveFormat.SampleRate != TargetSampleRate)
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate);

            var buffer = new float[4096];
            var samples = new List<float>();
            int read;
            while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    samples.Add(buffer[i]);
            }

            return samples.ToArray();
        }
    }
}
