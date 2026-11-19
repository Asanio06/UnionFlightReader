namespace UnionFlight.TestKit;

/// <summary>
/// Values served by <see cref="FakeXPlaneServer"/>. Numeric datarefs and
/// string datarefs are addressed by their simulator dataref names.
/// </summary>
public sealed class FakeXPlaneState
{
    public string Version { get; init; } = "12.4.0";

    public Dictionary<string, double> Numeric { get; } = new();

    public Dictionary<string, string> Strings { get; } = new();

    public Dictionary<string, long> Ids { get; } = new();

    public void AssignIds()
    {
        Ids.Clear();
        long id = 1;

        foreach (var name in Numeric.Keys)
        {
            Ids[name] = id++;
        }

        foreach (var name in Strings.Keys)
        {
            Ids[name] = id++;
        }
    }

    /// <summary>A Cessna 172 snapshot that exercises every mapped field.</summary>
    public static FakeXPlaneState CreateDefault()
    {
        var state = new FakeXPlaneState { Version = "12.4.0" };

        state.Numeric["sim/time/paused"] = 0;
        state.Numeric["sim/flightmodel/position/latitude"] = 48.8566;
        state.Numeric["sim/flightmodel/position/longitude"] = 2.3522;
        state.Numeric["sim/flightmodel/position/elevation"] = 1000;
        state.Numeric["sim/flightmodel/position/y_agl"] = 500;
        state.Numeric["sim/flightmodel/position/indicated_airspeed"] = 120;
        state.Numeric["sim/flightmodel/position/true_airspeed"] = 130;
        state.Numeric["sim/flightmodel/position/groundspeed"] = 140;
        state.Numeric["sim/flightmodel/position/vh_ind_fpm"] = -500;
        state.Numeric["sim/flightmodel/position/mag_psi"] = 180;
        state.Numeric["sim/time/total_flight_time_sec"] = 42;
        state.Numeric["sim/version/xplane_internal_version"] = 120400;

        state.Strings["sim/aircraft/view/acf_tailnum"] = "N12345";
        state.Strings["sim/aircraft/view/acf_ui_name"] = "Cessna 172";
        state.Strings["sim/aircraft/view/acf_descrip"] = "Cessna 172 (description)";

        return state;
    }

    public bool TryGetNumeric(long id, out double value)
    {
        foreach (var (name, candidate) in Ids)
        {
            if (candidate == id && Numeric.TryGetValue(name, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    public bool TryGetString(long id, out string value)
    {
        foreach (var (name, candidate) in Ids)
        {
            if (candidate == id && Strings.TryGetValue(name, out var found))
            {
                value = found;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
