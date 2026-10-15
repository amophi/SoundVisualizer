// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Localization
{
    /// <summary>
    /// 화면 언어 하나. <see cref="Code"/> 는 BCP-47 꼴 코드로, 설정 파일의 <c>Language</c> 값과 문자열 표 파일 이름
    /// (<c>Localization/Strings/{Code}.json</c>)에 그대로 쓴다. <see cref="NativeName"/> 은 그 언어로 쓴 이름이라,
    /// 지금 화면 언어를 읽지 못하는 사람도 언어 목록에서 자기 언어를 찾을 수 있다.
    /// </summary>
    public sealed record UiLanguage(string Code, string NativeName, bool IsRightToLeft = false);
}
