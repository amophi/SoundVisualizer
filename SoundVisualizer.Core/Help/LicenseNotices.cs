// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
using System.Reflection;
using System.Text;

namespace SoundVisualizer.Help
{
    /// <summary>
    /// 도움말의 "오픈소스 라이선스" 창에 보이는 고지. 안드로이드 LicenseAssets.kt 를 옮겼다.
    ///
    /// 저장소의 고지 파일을 복사하지 않고 제자리에서 Core 에 넣는다(csproj 의 EmbeddedResource). 안드로이드는 assets 에 복사본을
    /// 두고 원본과 같은지 테스트로 지켰는데, 여기서는 복사본이 없어 원본을 고치면 다음 빌드에 그대로 들어간다.
    /// </summary>
    public static class LicenseNotices
    {
        /// <summary>창에 보일 고지 하나. <paramref name="Path"/> 는 저장소 뿌리에서의 경로이고 창에서 고지의 머리가 된다.</summary>
        public sealed record Document(string Path, string ResourceName);

        /// <summary>
        /// 창에 보이는 순서. 무엇을 넣었는지(라이브러리·모델·글꼴 목록) → 앱에 넣은 글꼴의 라이선스(OFL) → 분류 탭 소리 이름의 고지 →
        /// 이 앱의 라이선스(AGPL). csproj 의 LogicalName 과 맞춘다.
        /// </summary>
        public static IReadOnlyList<Document> Documents { get; } = new[]
        {
            new Document("THIRD_PARTY.md", "SoundVisualizer.Licenses.THIRD_PARTY.md"),
            new Document("SoundVisualizer/Fonts/Pretendard-LICENSE.txt", "SoundVisualizer.Licenses.Pretendard-LICENSE.txt"),
            new Document("SoundVisualizer.Core/Sounds/Data/NOTICE.txt", "SoundVisualizer.Licenses.NOTICE.txt"),
            new Document("LICENSE", "SoundVisualizer.Licenses.LICENSE"),
        };

        /// <summary>이보다 짧은 '=' '-' 줄은 구분선이 아니라 내용으로 본다.</summary>
        private const int MinRulerLength = 10;

        private static readonly Assembly Owner = typeof(LicenseNotices).Assembly;

        /// <summary>고지 하나의 전문. 넣어 둔 리소스가 없으면 <see cref="InvalidOperationException"/>(빌드 설정이 틀렸다).</summary>
        public static string Read(Document document)
        {
            using var stream = Owner.GetManifestResourceStream(document.ResourceName)
                ?? throw new InvalidOperationException("고지 리소스가 없습니다: " + document.ResourceName);
            using var reader = new StreamReader(stream, Encoding.UTF8); // BOM 이 있어도 떼고 읽는다
            return reader.ReadToEnd();
        }

        /// <summary>
        /// 창에 보일 전문. 고지마다 저장소 경로를 머리로 달고 빈 줄 둘로 띄운다. 줄 끝은 \n 이다.
        /// Pretendard 의 OFL 처럼 첫 줄만 봐서는 무엇인지 모르는 고지가 있어 머리를 단다.
        /// </summary>
        public static string ReadAll()
        {
            var text = new StringBuilder();
            foreach (var document in Documents)
            {
                if (text.Length > 0) text.Append("\n\n\n");
                text.Append("==== ").Append(document.Path).Append(" ====\n\n");
                text.Append(Read(document).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
            }
            return text.Append('\n').ToString();
        }

        /// <summary>
        /// 고지를 창에 보일 글로 바꾼다. 줄 끝을 \n 으로 맞추고 구분선을 창 폭에 맞춘다.
        /// 저장소에는 LF 로 들어 있지만 Windows 에서 받으면(core.autocrlf) 작업 폴더가 CRLF 가 되고, 그 폴더에서 빌드하면
        /// 리소스에도 \r 이 들어간다.
        /// </summary>
        public static string DisplayText(string raw, int columns) =>
            FitRulers(raw.Replace("\r\n", "\n", StringComparison.Ordinal), columns);

        /// <summary>
        /// 줄 전체가 한 글자('=' 또는 '-')로 된 구분선을 <paramref name="columns"/> 글자까지 줄인다. 다른 줄은 손대지 않는다.
        /// 줄 끝은 \n 이어야 한다.
        ///
        /// 고지는 80칸에 맞춰 적혀 있어, 창이 그보다 좁으면 구분선 하나가 '=' 로 가득 찬 두세 줄로 꺾인다(안드로이드 #167).
        /// 창이 더 넓으면 원래 길이 그대로 둔다. 파일 자체는 고치지 않는다. 저장소에서 읽는 사람에게는 80칸이 맞다.
        /// 안드로이드는 '=' 만 보았지만 PC 판 고지에는 '-' 구분선(NOTICE 80칸, OFL 59칸)도 있어 함께 맞춘다.
        /// </summary>
        public static string FitRulers(string text, int columns)
        {
            if (columns < 1) return text;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (IsRuler(lines[i]) && lines[i].Length > columns) lines[i] = new string(lines[i][0], columns);
            }
            return string.Join("\n", lines);
        }

        private static bool IsRuler(string line)
        {
            if (line.Length < MinRulerLength || (line[0] != '=' && line[0] != '-')) return false;
            foreach (char c in line)
            {
                if (c != line[0]) return false;
            }
            return true;
        }
    }
}
