using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class XyzTrackParser
{
    public const float InchesToMeters = 1f / 39.37f;

    public static List<Vector3> Parse(string content)
    {
        var positions = new List<Vector3>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return positions;
        }

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Debug.LogWarning($"Skipping malformed XYZ line {i + 1}: {line}");
                continue;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                Debug.LogWarning($"Skipping malformed XYZ line {i + 1}: {line}");
                continue;
            }

            positions.Add(new Vector3(x, y, z) * InchesToMeters);
        }

        return positions;
    }
}
