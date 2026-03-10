namespace LuminaLex.Models;

public enum DiffType { Unchanged, Added, Removed }

public sealed record DiffSegment(string Text, DiffType Type);
