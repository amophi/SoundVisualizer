// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Help
{
    /// <summary>
    /// 제보용 GitHub 이슈 주소, 메일(mailto) 주소, 환경 정보 문구를 만든다. 안드로이드 ReportLink.kt 를 옮겼다.
    ///
    /// 화면이나 운영체제 API 없이 문자열만 다루므로 단위 테스트로 고정한다. 넣는 값은 <see cref="EnvironmentBlock"/> 의 인자뿐이라
    /// 소리, 장치 이름, 사용자 경로가 섞일 수 없다. 환경 정보의 이름표는 번역하지 않는다. 제보를 읽는 쪽이 어느 언어로 온 제보든
    /// 같은 꼴로 읽을 수 있어야 한다.
    /// </summary>
    public static class ReportLink
    {
        /// <summary>새 이슈 화면. PC 판 저장소다(안드로이드 판과 다르다).</summary>
        public const string IssuesUrl = "https://github.com/amophi/SoundVisualizer/issues/new";

        /// <summary>메일 제보를 받는 곳. 안드로이드 판과 같은 주소다.</summary>
        public const string ReportEmail = "nawwhod@gmail.com";

        /// <summary>제보 본문 끝에 붙는 네 줄. 줄은 \n 으로 나눈다.</summary>
        public static string EnvironmentBlock(string appVersion, string windows, string dotnet, string language) =>
            string.Join("\n",
                "App version: " + appVersion,
                "Windows: " + windows,
                ".NET: " + dotnet,
                "App language: " + language);

        /// <summary>
        /// 제목과 본문을 미리 채운 새 이슈 주소. GitHub 은 title·body 쿼리로 새 이슈 화면을 채워 준다.
        /// 값은 모두 인코딩하므로 사용자가 쓸 자리에 줄바꿈이나 한글이 있어도 주소가 깨지지 않는다.
        /// </summary>
        public static string IssueUrl(string title, string bodyPrompt, string environment) =>
            IssuesUrl + "?title=" + Encode(title) + "&body=" + Encode(Body(bodyPrompt, environment));

        /// <summary>사용자가 쓸 자리와 환경 정보를 합친 제보 본문. 이슈와 메일이 같은 본문을 쓴다.</summary>
        public static string Body(string bodyPrompt, string environment) =>
            bodyPrompt.Length == 0 ? environment : bodyPrompt + "\n\n" + environment;

        /// <summary>
        /// 제목과 본문을 채운 mailto 주소. 메일 본문의 줄바꿈은 RFC 6068 대로 CRLF(%0D%0A)로 적는다.
        /// 안드로이드는 Gmail 이 받는 \n 그대로 넣었지만, Windows 의 메일 앱은 여러 가지라 규격을 따른다.
        /// </summary>
        public static string MailtoUri(string address, string subject, string bodyPrompt, string environment) =>
            "mailto:" + address + "?subject=" + Encode(subject) + "&body=" + Encode(MailBody(bodyPrompt, environment));

        /// <summary>메일에 들어가는 본문. <see cref="Body"/> 와 같고 줄바꿈만 CRLF 다.</summary>
        public static string MailBody(string bodyPrompt, string environment) =>
            Body(bodyPrompt, environment).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        // RFC 3986 의 비예약 문자만 남기고 모두 %XX 로 적는다. 공백은 %20 이다. 안드로이드 URLEncoder 는 공백을 + 로 적었는데,
        // mailto 쿼리에서 + 는 공백이 아니라 글자 그대로라 그쪽은 따로 %20 으로 바꿨다. 여기서는 처음부터 %20 이라 둘이 같다.
        private static string Encode(string value) => Uri.EscapeDataString(value);
    }
}
