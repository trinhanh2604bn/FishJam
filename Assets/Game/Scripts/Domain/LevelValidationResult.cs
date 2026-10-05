using System;
using System.Collections.Generic;

namespace FishPuzzle.Domain
{
    /// <summary>
    /// Every issue found for one validation pass. An empty error list means the level may start later.
    /// </summary>
    public sealed class LevelValidationResult
    {
        private readonly LevelValidationIssue[] _issues;

        public LevelValidationResult(IReadOnlyList<LevelValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0)
            {
                _issues = Array.Empty<LevelValidationIssue>();
                return;
            }

            _issues = new LevelValidationIssue[issues.Count];
            for (var i = 0; i < issues.Count; i++)
            {
                _issues[i] = issues[i];
            }
        }

        public IReadOnlyList<LevelValidationIssue> Issues => _issues;

        public bool IsValid
        {
            get
            {
                for (var i = 0; i < _issues.Length; i++)
                {
                    if (_issues[i] != null && _issues[i].Severity == LevelValidationSeverity.Error)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
