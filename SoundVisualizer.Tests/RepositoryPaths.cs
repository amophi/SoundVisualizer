// Copyright (C) 2026 amophi (SoundVisualizer Contributors)
// This file is part of SoundVisualizer.
// SoundVisualizer is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, version 3.
namespace SoundVisualizer.Tests;

/// <summary>앱 소스(XAML 등)를 읽는 테스트가 저장소 뿌리를 찾는 곳.</summary>
internal static class RepositoryPaths
{
    /// <summary>테스트 실행 폴더에서 위로 올라가며 SoundVisualizer.slnx 가 있는 폴더를 찾는다.</summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SoundVisualizer.slnx"))) return directory.FullName;
        }
        throw new InvalidOperationException("SoundVisualizer.slnx 를 찾지 못했습니다: " + AppContext.BaseDirectory);
    }
}
