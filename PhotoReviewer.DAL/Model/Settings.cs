﻿using JetBrains.Annotations;
using PhotoReviewer.DAL.Contracts.Data;
using Scar.Common.DAL.Model;

namespace PhotoReviewer.DAL.Model
{
    internal sealed class Settings : Entity<int>, ISettings
    {
        [CanBeNull]
        public string LastUsedDirectoryPath { get; set; }
    }
}