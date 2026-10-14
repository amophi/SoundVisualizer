// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using SoundVisualizer.Ai;

namespace SoundVisualizer.Diagnostics
{
    /// <summary>
    /// AI 판정을 받아 CSV 줄로 쌓는다(모바일 AiDebugLogWriter.kt, #193). 어디에 쌓느냐는 sink 가 정한다. 파일이든 테스트의 문자열이든.
    ///
    /// 세 가지를 여기서 지킨다.
    /// - 머리글은 한 번만. 첫 줄을 쓸 때 같이 나간다. 결과가 한 번도 없으면 아무것도 쓰지 않는다.
    /// - 같은 결과는 한 번만. HUD 는 추론(250ms)보다 자주 읽어 같은 결과를 여러 번 본다.
    ///   <see cref="AiClassificationResult.TimestampMs"/> 가 앞서 쓴 것과 같으면 건너뛴다.
    /// - 상한에 닿으면 멈춘다. 초당 네 줄이 하루면 수십 MB 가 된다. 켜 둔 것을 잊은 PC 의 디스크를 먹지 않게 한다.
    ///   멈춘 뒤에는 조용히 아무것도 쓰지 않는다(<see cref="Stopped"/> 로 알 수 있다).
    ///
    /// 한 스레드에서만 쓴다(<see cref="AiDebugRecorder"/> 의 쓰는 스레드).
    /// </summary>
    public sealed class AiDebugLogWriter
    {
        /// <summary>기본 상한 20MB. 초당 네 줄(한 줄 약 350바이트)이면 하루쯤 담긴다.</summary>
        public const long DefaultMaxBytes = 20L * 1024 * 1024;

        private readonly TextWriter _sink;
        private readonly long _maxBytes;
        private bool _wroteHeader;
        private long _written;
        private long? _lastResultMs;

        public AiDebugLogWriter(TextWriter sink, long maxBytes = DefaultMaxBytes)
        {
            ArgumentNullException.ThrowIfNull(sink);
            _sink = sink;
            _maxBytes = maxBytes;
        }

        /// <summary>쓴 줄 수(머리글은 세지 않는다).</summary>
        public int Rows { get; private set; }

        /// <summary>상한에 닿아 그만 쓰기로 했는지.</summary>
        public bool Stopped { get; private set; }

        /// <summary>결과 하나를 쌓는다. 실제로 한 줄을 썼으면 true, 같은 결과라 건너뛰었거나 상한에 닿았으면 false.</summary>
        /// <remarks>상한은 쓰고 나서 본다. 상한을 넘기는 그 줄까지는 쓰고 멈춘다(모바일과 같다).</remarks>
        public bool Write(AiClassificationResult result, long nowMs, float level, bool shown)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (Stopped) return false;
            if (result.TimestampMs == _lastResultMs) return false;

            if (!_wroteHeader)
            {
                Append(AiDebugCsv.Header);
                _wroteHeader = true;
            }
            Append(AiDebugCsv.Row(result, nowMs, level, shown));
            _lastResultMs = result.TimestampMs;
            Rows++;
            if (_written >= _maxBytes) Stopped = true;
            return true;
        }

        private void Append(string line)
        {
            // 줄 끝은 모바일 파일과 같은 \n 하나다. WriteLine 은 Windows 에서 \r\n 을 쓴다.
            _sink.Write(line);
            _sink.Write('\n');
            // 한 글자를 한 바이트로 센다. 이름이 아스키라 거의 맞고, 상한은 정확할 필요가 없다.
            _written += line.Length + 1;
        }
    }
}
