using Xunit;
using CUAVsCodingChallenge.App;

namespace CUAVsCodingChallenge.Tests;

// Tests related to geographic utility functions.
// These focus on correctness at the boundaries and sanity checks.
public class GeoTests
{
    [Fact]
    public void NormalizeLongitude_WrapsIntoRange()
    {
        // Longitudes can come in wildly out of range depending on the sensor.
        // These assertions make sure everything is normalized into [-180, 180).
        Assert.Equal(-10.0, Geo.NormalizeLongitude(350.0), 6);
        Assert.Equal(140.0, Geo.NormalizeLongitude(500.0), 6);
        Assert.Equal(170.0, Geo.NormalizeLongitude(-190.0), 6);
    }

    [Fact]
    public void HaversineMeters_SamePoint_IsZero()
    {
        // Distance between identical points should be ~0.
        // Using a small tolerance instead of exact equality avoids
        // floating-point precision issues.
        Assert.True(Geo.HaversineMeters(10, 10, 10, 10) < 0.001);
    }
}

// Tests for the matching logic that correlates sensor readings.
// These verify correctness and enforce one-to-one matching behaviour.
public class MatcherTests
{
    [Fact]
    public void MatchOneToOneClosest_FindsAMatchWithinThreshold()
    {
        // Two points that are extremely close together
        // should be matched when using a 100m threshold.
        var s1 = Geo.Preprocess(new[]
        {
            new SensorReading(1, 51.0, -114.0)
        });

        var s2 = Geo.Preprocess(new[]
        {
            new SensorReading(2, 51.0001, -114.0001)
        });

        var matches = Matcher.MatchOneToOneClosest(s1, s2, 100.0);

        // Exactly one match should be found, and it should map the correct IDs.
        Assert.Single(matches);
        Assert.Equal(1, matches[0].Sensor1Id);
        Assert.Equal(2, matches[0].Sensor2Id);
    }

    [Fact]
    public void MatchOneToOneClosest_UsesEachSensor2AtMostOnce()
    {
        // Two sensor1 readings compete for a single sensor2 reading.
        // Only one should win — this enforces one-to-one matching.
        var s1 = Geo.Preprocess(new[]
        {
            new SensorReading(1, 51.0, -114.0),
            new SensorReading(2, 51.0, -114.0)
        });

        var s2 = Geo.Preprocess(new[]
        {
            new SensorReading(10, 51.0, -114.0)
        });

        var matches = Matcher.MatchOneToOneClosest(s1, s2, 100.0);

        // Even though both are within range, only one match is allowed.
        Assert.Single(matches);
    }
}

