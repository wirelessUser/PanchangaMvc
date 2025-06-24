// File: SunriseSunsetCalculator.cs
using SwissEphNet;
using System;

public class SunriseSunsetCalculator
{
    private readonly SwissEph _swe;
    private readonly double _longitude;
    private readonly double _latitude;

    public SunriseSunsetCalculator(SwissEph swe, double longitude, double latitude)
    {
        _swe = swe;
        _longitude = longitude;
        _latitude = latitude;
    }

    public DateTime GetSunrise(DateTime date, double timezone)
    {
        return GetSunEvent(date, SwissEph.SE_CALC_RISE, timezone);
    }

    public DateTime GetSunset(DateTime date, double timezone)
    {
        return GetSunEvent(date, SwissEph.SE_CALC_SET, timezone);
    }

    private DateTime GetSunEvent(DateTime date, int flag, double timezone)
    {
        double jd = _swe.swe_julday(date.Year, date.Month, date.Day, 0, SwissEph.SE_GREG_CAL);
        double tret = 0;
        string serr = "";

        _swe.swe_rise_trans(jd, SwissEph.SE_SUN, null,
            SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL,
            flag, new double[] { _longitude, _latitude, 0 },
            1013.25, 23.0, ref tret, ref serr);

        return JulianToDateTime(tret, timezone);
    }

    private DateTime JulianToDateTime(double jd, double tz)
    {
        int y = 0, m = 0, d = 0;
        double h = 0;
        _swe.swe_revjul(jd, SwissEph.SE_GREG_CAL, ref y, ref m, ref d, ref h);
        int hr = (int)h;
        int min = (int)((h - hr) * 60);
        int sec = (int)(((h - hr) * 60 - min) * 60);
        return new DateTime(y, m, d, hr, min, sec).AddHours(tz);
    }
}
