// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Ai
{
    /// <summary>
    /// AI 가 확정하는 소리 종류. 화면 색·표시 여부·진동 설정이 이 값으로 갈린다.
    ///
    /// <see cref="Ambient"/> 를 0 으로 둔다. 모바일 판은 결과가 없을 때와 모르는 라벨을 모두 환경음으로 그리므로,
    /// 초기화하지 않은 값(default)도 같은 뜻이 되게 한다.
    /// </summary>
    public enum SoundType
    {
        /// <summary>환경음.</summary>
        Ambient = 0,

        /// <summary>대화음. 알아듣는 말만이 아니라 사람이 내는 소리 전반(웃음, 울음, 고함 등)이다.</summary>
        Speech = 1,

        /// <summary>위협음.</summary>
        Danger = 2,
    }

    /// <summary>
    /// <see cref="SoundType"/> 과 저장용 문자열 사이의 변환.
    ///
    /// 문자열은 모바일 판(AiClassification.AMBIENT·SPEECH·DANGER, 설정의 sound_types JSON)과 글자까지 같다.
    /// 사용자가 바꾼 소리 종류를 "영어 클래스 이름 → 이 문자열" 맵으로 저장하므로, 두 판이 같은 설정을 읽고 쓸 수 있게 맞춘다.
    /// </summary>
    public static class SoundTypeLabels
    {
        public const string Ambient = "ambient";
        public const string Speech = "speech";
        public const string Danger = "danger";

        /// <summary>저장할 문자열. 정의되지 않은 값은 프로그램 오류라 예외를 던진다.</summary>
        public static string ToLabel(this SoundType type) => type switch
        {
            SoundType.Ambient => Ambient,
            SoundType.Speech => Speech,
            SoundType.Danger => Danger,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "알 수 없는 소리 종류"),
        };

        /// <summary>
        /// 저장된 문자열을 읽는다. 세 문자열과 대소문자까지 같을 때만 true 다. 모바일 판도 "Danger" 처럼
        /// 조금이라도 다른 값은 사용자 지정에서 버리므로 여기서 너그럽게 읽지 않는다.
        /// </summary>
        public static bool TryParse(string? label, out SoundType type)
        {
            switch (label)
            {
                case Ambient:
                    type = SoundType.Ambient;
                    return true;
                case Speech:
                    type = SoundType.Speech;
                    return true;
                case Danger:
                    type = SoundType.Danger;
                    return true;
                default:
                    type = SoundType.Ambient;
                    return false;
            }
        }
    }
}
