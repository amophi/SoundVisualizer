// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Numerics;

namespace SoundVisualizer.Ai
{
    /// <summary>
    /// AI 만 쓰는 캡처 레이트 모노 링 버퍼(Android 의 AiAudioBuffer.kt). 시각화 경로와 따로 돈다.
    ///
    /// 캡처 스레드가 <see cref="IngestInterleaved"/> 로 넣고, 추론 스레드가 <see cref="CopyTailRightPadded"/> 로 최근 한 창을
    /// 가져간다. 링은 잠금으로 지킨다. 섞을 때 쓰는 임시 버퍼는 할당을 피하려고 하나를 다시 쓰므로
    /// <see cref="IngestInterleaved"/> 를 부르는 스레드는 하나여야 한다. NAudio 는 캡처 버퍼를 재사용하므로 넣는 즉시 링으로 복사한다.
    /// 레이트와 채널 수는 만들 때 정해진다. 장치 형식이 바뀌면 새로 만든다.
    /// </summary>
    public sealed class AiAudioBuffer
    {
        public const int DefaultCaptureSampleRate = 44100;
        public const int RingSeconds = 2;

        /// <summary>Android 가 받는 레이트(16·44.1·48kHz)의 링 크기. 인덱스를 비트 마스크로 돌리려고 2의 거듭제곱이다.</summary>
        public const int MaxRingSize = 131072;

        private readonly object _lock = new();
        private readonly int _captureSampleRate;
        private readonly int _channels;
        private readonly float[] _ring;
        private readonly int _ringMask;
        private readonly int _retainedSamples;
        private readonly int _windowSamples;
        private int _tail;
        private int _count;

        /// <summary>섞은 모노를 링에 넣기 전에 잠깐 두는 곳. 버퍼마다 할당하지 않으려고 다시 쓴다.</summary>
        private readonly float[] _monoScratch = new float[MaxRingSize / 2];

        /// <exception cref="ArgumentOutOfRangeException">AI 가 받지 않는 레이트(<see cref="AiCaptureSampleRatePolicy.IsSupportedForAi"/>).</exception>
        public AiAudioBuffer(int captureSampleRate = DefaultCaptureSampleRate, int channels = 2)
        {
            if (!AiCaptureSampleRatePolicy.IsSupportedForAi(captureSampleRate))
                throw new ArgumentOutOfRangeException(nameof(captureSampleRate), captureSampleRate, "Unsupported AI capture sample rate");

            _captureSampleRate = captureSampleRate;
            _channels = channels;
            // 2초를 남긴다. 44.1kHz 보다 낮은 레이트도 44.1kHz 의 2초만큼 둔다(Android 와 같다).
            _retainedSamples = Math.Max(DefaultCaptureSampleRate * RingSeconds, captureSampleRate * RingSeconds);
            // PC 에서 더한 것: 88.2kHz 부터는 2초가 131,072 를 넘고, 176.4·192kHz 는 한 창(171,990·187,200샘플)조차 넘는다.
            // 2초를 담는 가장 작은 2의 거듭제곱으로 늘린다(88.2·96kHz 262,144, 176.4·192kHz 524,288). Android 레이트는 그대로다.
            int capacity = Math.Max(MaxRingSize, (int)BitOperations.RoundUpToPowerOf2((uint)_retainedSamples));
            _ring = new float[capacity];
            _ringMask = capacity - 1;
            _windowSamples = CaptureAudioMath.CaptureSamplesForOneYamnetWindow(captureSampleRate);
        }

        public int SampleRate => _captureSampleRate;

        public int ChannelCount => _channels;

        /// <summary>링에 담을 수 있는 모노 샘플 수(2의 거듭제곱). 한 창과 2초가 모두 들어간다.</summary>
        public int RingCapacity => _ring.Length;

        public int AvailableSamples
        {
            get
            {
                lock (_lock) return _count;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _tail = 0;
                _count = 0;
            }
        }

        /// <summary>
        /// 이번 캡처 버퍼에서 실제로 녹음된 인터리브 float PCM(채널 수 × 프레임)을 모노로 섞어 링에 넣는다. 캡처 스레드에서 부른다.
        /// NAudio 의 바이트 버퍼는 MemoryMarshal.Cast&lt;byte, float&gt;(buffer.AsSpan(0, bytesRecorded)) 로 넘긴다.
        /// </summary>
        public void IngestInterleaved(ReadOnlySpan<float> pcm)
        {
            if (pcm.IsEmpty || _channels <= 0) return;

            // PC 에서 더한 것: 임시 버퍼(65,536프레임)보다 긴 버퍼는 프레임 단위로 나눠 넣는다. Android 는 여기서 예외를
            // 던졌는데, 캡처 스레드에서 던지면 캡처가 멈춘다. 나눠 넣어도 링에 남는 소리는 같다.
            long maxChunkFloats = (long)_monoScratch.Length * _channels;
            while (!pcm.IsEmpty)
            {
                ReadOnlySpan<float> chunk = pcm.Length > maxChunkFloats ? pcm[..(int)maxChunkFloats] : pcm;
                int frames = CaptureAudioMath.DownmixInterleavedToMono(chunk, _channels, _monoScratch);
                if (frames <= 0) return;
                AppendMono(_monoScratch.AsSpan(0, frames));
                pcm = pcm[chunk.Length..];
            }
        }

        /// <summary>이미 모노인 캡처 레이트 샘플을 넣는다(테스트·도구용). 어느 스레드에서 불러도 된다.</summary>
        public void IngestMono(ReadOnlySpan<float> mono)
        {
            if (mono.IsEmpty) return;
            AppendMono(mono);
        }

        private void AppendMono(ReadOnlySpan<float> monoChunk)
        {
            lock (_lock)
            {
                for (int i = 0; i < monoChunk.Length; i++)
                {
                    _ring[_tail] = monoChunk[i];
                    _tail = (_tail + 1) & _ringMask;
                }
                // 링이 넘치면 가장 오래된 샘플부터 밀려나고, 2초를 넘는 앞부분은 버린다.
                // (Android 의 head 포인터는 읽는 쪽이 쓰지 않아 남은 수만 센다. 결과는 같다.)
                _count = (int)Math.Min((long)_count + monoChunk.Length, _retainedSamples);
            }
        }

        /// <summary>
        /// 최근 <paramref name="destination"/>.Length 개를 오른쪽에 맞춰 복사하고, 모자라면 앞을 0 으로 채운다.
        /// 복사 시점에 링에 있던 샘플 수를 돌려준다. 추론 스레드에서 부른다.
        /// </summary>
        public int CopyTailRightPadded(Span<float> destination)
        {
            lock (_lock)
            {
                int take = Math.Min(_count, destination.Length);
                int destinationStart = destination.Length - take;
                destination[..destinationStart].Clear();
                if (take > 0)
                {
                    int ringStart = (_tail - take) & _ringMask;
                    int firstPart = Math.Min(take, _ring.Length - ringStart);
                    _ring.AsSpan(ringStart, firstPart).CopyTo(destination[destinationStart..]);
                    _ring.AsSpan(0, take - firstPart).CopyTo(destination[(destinationStart + firstPart)..]);
                }
                return _count;
            }
        }

        /// <summary>16kHz 한 창(<see cref="CaptureAudioMath.RequiredMono16kSamples"/>)을 만들 만큼 쌓였는지.</summary>
        public bool HasEnoughForYamnetWindow()
        {
            lock (_lock) return _count >= _windowSamples;
        }
    }
}
