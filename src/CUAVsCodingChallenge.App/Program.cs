using System.Globalization;
using System.Text.Json;

namespace CUAVsCodingChallenge.App;

// Entry point for the application.
// The goal here is to correlate sensor readings from two different sources
// and identify which ones likely represent the same real-world anomaly.
public static class Program
{
    // Sensors have an accuracy of ~100m, so anything within this distance
    // is considered a potential match.
    private const double ThresholdMeters = 100.0;

    public static int Main(string[] args)
    {
        // Basic argument validation — we expect:
        // 1) CSV input
        // 2) JSON input
        // 3) optional output file
        if (args.Length < 2 || args.Length > 3)
        {
            Console.Error.WriteLine("Usage: dotnet run -- <sensor1.csv> <sensor2.json> [output.csv]");
            Console.Error.WriteLine("Example: dotnet run -- SensorData1.csv SensorData2.json output.csv");
            return 2;
        }

        string csvPath = args[0];
        string jsonPath = args[1];
        string? outputPath = args.Length == 3 ? args[2] : null;

        // Read raw data from both sensors.
        // At this stage, we’re not assuming the data is clean or valid.
        var sensor1 = DataIO.ReadSensor1Csv(csvPath);
        var sensor2 = DataIO.ReadSensor2Json(jsonPath);

        // Preprocess the data:
        // - drop invalid latitudes
        // - normalize longitudes into a consistent range
        var clean1 = Geo.Preprocess(sensor1);
        var clean2 = Geo.Preprocess(sensor2);

        // Print counts so it’s obvious what got filtered out.
        // This helps with debugging and shows data awareness.
        Console.WriteLine($"Sensor1 total: {sensor1.Count}, valid: {clean1.Count}");
        Console.WriteLine($"Sensor2 total: {sensor2.Count}, valid: {clean2.Count}");

        // Perform one-to-one matching:
        // each sensor2 reading can only be matched once,
        // and we choose the closest valid match within the threshold.
        var matches = Matcher.MatchOneToOneClosest(clean1, clean2, ThresholdMeters);
        Console.WriteLine($"Matches found: {matches.Count}");

        // Prepare CSV-style output (simple and easy to consume).
        var lines = new List<string> { "sensor1_id,sensor2_id" };
        lines.AddRange(matches.Select(m => $"{m.Sensor1Id},{m.Sensor2Id}"));

        // Either write to a file or print to stdout, depending on input args.
        if (!string.IsNullOrWhiteSpace(outputPath))
            File.WriteAllLines(outputPath, lines);
        else
            lines.ForEach(Console.WriteLine);

        return 0;
    }
}

// Simple immutable data model for a single sensor reading.
// Using a record keeps things lightweight and readable.
public record SensorReading(int Id, double Latitude, double Longitude);

public static class DataIO
{
    // Reads sensor 1 data from CSV.
    // The file format isn’t guaranteed to be clean, so parsing is defensive.
    public static List<SensorReading> ReadSensor1Csv(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"CSV file not found: {path}");

        var list = new List<SensorReading>();
        using var reader = new StreamReader(path);

        // Skip header line (id, latitude, longitude)
        reader.ReadLine();

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Handles tab-, comma-, or space-separated values.
            // This makes the parser resilient to formatting quirks.
            var parts = line.Split(new[] { '\t', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            // Parse using invariant culture to avoid locale issues
            // (e.g., commas vs periods in decimals).
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                continue;

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
                continue;

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                continue;

            list.Add(new SensorReading(id, lat, lon));
        }

        return list;
    }

    // Reads sensor 2 data from JSON.
    // Assumes a top-level array of objects with id/latitude/longitude fields.
    public static List<SensorReading> ReadSensor2Json(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"JSON file not found: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("JSON root is not an array.");

        var list = new List<SensorReading>();

        foreach (var e in doc.RootElement.EnumerateArray())
        {
            int id = e.GetProperty("id").GetInt32();
            double lat = e.GetProperty("latitude").GetDouble();
            double lon = e.GetProperty("longitude").GetDouble();
            list.Add(new SensorReading(id, lat, lon));
        }

        return list;
    }
}

public static class Geo
{
    // Cleans up raw sensor readings so downstream logic can assume consistency.
    public static List<SensorReading> Preprocess(IEnumerable<SensorReading> data)
    {
        var result = new List<SensorReading>();

        foreach (var d in data)
        {
            // Latitude must be physically valid.
            // Values outside [-90, 90] are discarded entirely.
            if (d.Latitude < -90.0 || d.Latitude > 90.0)
                continue;

            // Longitude is normalized rather than discarded,
            // since values may be represented in different ranges.
            double lon = NormalizeLongitude(d.Longitude);
            result.Add(new SensorReading(d.Id, d.Latitude, lon));
        }

        return result;
    }

    // Normalizes longitude into the range [-180, 180).
    // This avoids mismatches caused by different coordinate conventions.
    public static double NormalizeLongitude(double lon)
        => ((lon + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

    // Computes great-circle distance between two points using the Haversine formula.
    // Accurate enough for small distances like our 100m threshold.
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // Earth radius in metres

        static double ToRad(double deg) => deg * Math.PI / 180.0;

        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}

// Represents a successful correlation between two sensors,
// including the actual computed distance for transparency/debugging.
public record Match(int Sensor1Id, int Sensor2Id, double DistanceMeters);

public static class Matcher
{
    // Matches sensor readings one-to-one by choosing the closest valid pairing
    // within the specified distance threshold.
    public static List<Match> MatchOneToOneClosest(
        List<SensorReading> a,
        List<SensorReading> b,
        double thresholdMeters)
    {
        // Track which sensor2 readings have already been used
        // so we don’t double-count the same anomaly.
        var usedB = new HashSet<int>();
        var result = new List<Match>();

        for (int i = 0; i < a.Count; i++)
        {
            var r1 = a[i];

            int bestJ = -1;
            double bestDist = double.MaxValue;

            for (int j = 0; j < b.Count; j++)
            {
                if (usedB.Contains(j))
                    continue;

                var r2 = b[j];
                double dist = Geo.HaversineMeters(
                    r1.Latitude, r1.Longitude,
                    r2.Latitude, r2.Longitude
                );

                if (dist <= thresholdMeters && dist < bestDist)
                {
                    bestDist = dist;
                    bestJ = j;
                }
            }

            if (bestJ != -1)
            {
                usedB.Add(bestJ);
                result.Add(new Match(r1.Id, b[bestJ].Id, bestDist));
            }
        }

        // Sort for deterministic, readable output.
        result.Sort((x, y) => x.Sensor1Id.CompareTo(y.Sensor1Id));
        return result;
    }
}

