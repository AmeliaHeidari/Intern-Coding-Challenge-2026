# CUAVs Intern Coding Challenge (2026)

This repository contains my solution to the Canadian UAVs Intern Coding Challenge.  
The goal is to correlate geospatial anomaly detections from two sensors (CSV + JSON) and identify likely shared detections based on proximity.

---

## Approach

1. **Input parsing**
   - Sensor 1 data is read from CSV
   - Sensor 2 data is read from JSON

2. **Preprocessing**
   - Discard invalid latitude values outside `[-90, 90]`
   - Normalize longitudes into the range `[-180, 180)`
   - Keep IDs unchanged (IDs are sensor-specific and not assumed to be unique across sensors)

3. **Matching logic**
   - Use the Haversine formula to compute distance between coordinates
   - Match readings **one-to-one**, selecting the closest Sensor 2 reading within **100 metres**
   - Each Sensor 2 reading can only be matched once
   - Output is sorted by `sensor1_id` for deterministic results

4. **Output**
   - CSV format:
     ```
     sensor1_id,sensor2_id
     ```

---

## How to Run

From the repository root:

```bash
# Run unit tests
dotnet test tests/CUAVsCodingChallenge.Tests/CUAVsCodingChallenge.Tests.csproj

# Run the application
dotnet run --project src/CUAVsCodingChallenge.App -- SensorData1.csv SensorData2.json output.csv

This produces an output file in the format:

sensor1_id,sensor2_id

Project Structure
src/
  CUAVsCodingChallenge.App/
    Program.cs

tests/
  CUAVsCodingChallenge.Tests/
    GeoTests.cs

Testing

Unit tests verify:
- Longitude normalization behaviour
- Correctness of Haversine distance calculations
- One-to-one matching constraints
All tests pass successfully.

Notes
- Implemented in C# / .NET
- Emphasis on correctness, clarity, and reproducibility
- Code is intentionally straightforward and well-documented

Author: Amelia Heidari
