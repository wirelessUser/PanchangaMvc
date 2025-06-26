using Microsoft.AspNetCore.Mvc;
using SwissEphNet;
using System;
using System.Collections.Generic;

namespace PanchangaMvc.Controllers
{
    public class DailyPeriodController : Controller
    {
        public class DailyPeriodResult
        {
            public DateTime Date { get; set; }
            public string Weekday { get; set; }
            public string SunSign { get; set; }
            public string MoonSign { get; set; }
            public string RahuKalam { get; set; }
            public string GulikaKalam { get; set; }
            public string Yamaganda { get; set; }
            public string Abhijit { get; set; }
            public List<string> DayChogadiya { get; set; }
            public List<string> NightChogadiya { get; set; }
        }

        private string[] signNames = {
            "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
            "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces"
        };

        public IActionResult FullYearDailyPeriods()
        {
            string ephePath = "ephe";
            double lat = 27.88145, lon = 78.07464, tz = 5.5;
            var swe = new SwissEph();
            swe.swe_set_ephe_path(ephePath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

            var results = new List<DailyPeriodResult>();
            var geopos = new double[] { lon, lat, 0 };

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    DateTime date = new DateTime(2025, month, day);
                    double jd = swe.swe_julday(date.Year, date.Month, date.Day, 0, SwissEph.SE_GREG_CAL);
                    string serr = "";

                    double sunriseJD = GetRiseSetJD(swe, jd, SwissEph.SE_SUN, SwissEph.SE_CALC_RISE, geopos);
                    double sunsetJD = GetRiseSetJD(swe, jd, SwissEph.SE_SUN, SwissEph.SE_CALC_SET, geopos);

                    DateTime sunrise = JulianToDateTime(sunriseJD, tz, swe);
                    DateTime sunset = JulianToDateTime(sunsetJD, tz, swe);

                    double sunriseDec = TimeToDecimal(sunrise);
                    double sunsetDec = TimeToDecimal(sunset);
                    double daySpan = sunsetDec - sunriseDec;

                    string rahu = GetKalamSpan(sunriseDec, sunsetDec, date.DayOfWeek, "Rahu");
                    string gulika = GetKalamSpan(sunriseDec, sunsetDec, date.DayOfWeek, "Gulika");
                    string yama = GetKalamSpan(sunriseDec, sunsetDec, date.DayOfWeek, "Yamaganda");

                 
                    double dayLength = sunsetDec - sunriseDec;

                    // Abhijit span (1/15th of day length)
                    double abhijitSpan = dayLength / 15.0;

                    // Local noon time (in decimal hours)
                    double middayDec = (sunriseDec + sunsetDec) / 2.0;

                    // Start and End of Abhijit in decimal hours
                    double abhijitStart = middayDec - (abhijitSpan / 2.0);
                    double abhijitEnd = middayDec + (abhijitSpan / 2.0);

                    // Threshold: Skip if day length < 8 hours (~0.33 day length in decimal)
                    bool isTooShortDay = dayLength < 8.0;

                    // Additionally skip if AbhijitStart < Sunrise OR AbhijitEnd > Sunset (invalid window)
                    bool outOfBounds = (abhijitStart < sunriseDec) || (abhijitEnd > sunsetDec);

                    // Final: If day too short or Abhijit spans outside daylight, set to "None"
                    string abhijit;

                    if (isTooShortDay || outOfBounds)
                        abhijit = "None";
                    else
                        abhijit = $"{DecimalToTime(abhijitStart)} to {DecimalToTime(abhijitEnd)}";


                    string sunSign = GetSignName(swe, jd, SwissEph.SE_SUN);
                    string moonSign = GetPreciseMoonSignTransition(swe, sunriseJD, sunsetJD, tz);

                    var dayChoga = GetChogadiyaList(sunriseDec, daySpan, date.DayOfWeek, true);
                    var nightChoga = GetChogadiyaList(sunsetDec, 24 - daySpan, date.DayOfWeek, false);

                    results.Add(new DailyPeriodResult
                    {
                        Date = date,
                        Weekday = date.DayOfWeek.ToString(),
                        SunSign = sunSign,
                        MoonSign = moonSign,
                        RahuKalam = rahu,
                        GulikaKalam = gulika,
                        Yamaganda = yama,
                        Abhijit = abhijit,
                        DayChogadiya = dayChoga,
                        NightChogadiya = nightChoga
                    });
                }
            }

            return View(results);
        }

        private double GetRiseSetJD(SwissEph swe, double jd, int planet, int eventFlag, double[] geopos)
        {
            double tret = 0;
            string serr = "";
            swe.swe_rise_trans(jd, planet, null, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL,
                eventFlag, geopos, 1013.25, 23.0, ref tret, ref serr);
            return tret;
        }

        private string GetSignName(SwissEph swe, double jd, int planet)
        {
            string serr = "";
            double[] pos = new double[6];
            swe.swe_calc_ut(jd, planet, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, pos, ref serr);
            int sign = (int)(pos[0] / 30);
            return signNames[sign];
        }

        private string GetPreciseMoonSignTransition(SwissEph swe, double sunriseJD, double sunsetJD, double tz)
        {
            int startSign = GetPlanetSignIndex(sunriseJD, swe, SwissEph.SE_MOON);
            int endSign = GetPlanetSignIndex(sunsetJD, swe, SwissEph.SE_MOON);

            if (startSign == endSign)
            {
                return signNames[startSign];
            }
            else
            {
                double transitionJD = FindMoonSignChangeJD(swe, sunriseJD, sunsetJD, startSign);
                DateTime changeTime = JulianToDateTime(transitionJD, tz, swe);
                return $"{signNames[startSign]} till {changeTime:HH:mm:ss}, {signNames[endSign]} after {changeTime:HH:mm:ss}";
            }
        }

        private int GetPlanetSignIndex(double jd, SwissEph swe, int planet)
        {
            string serr = "";
            double[] pos = new double[6];
            swe.swe_calc_ut(jd, planet, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, pos, ref serr);
            return (int)(pos[0] / 30);
        }

        private double FindMoonSignChangeJD(SwissEph swe, double jdStart, double jdEnd, int startSign)
        {
            string serr = "";
            double[] moon = new double[6];

            for (int i = 0; i < 30; i++) // 30 iterations for high precision
            {
                double mid = (jdStart + jdEnd) / 2;
                swe.swe_calc_ut(mid, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
                int midSign = (int)(moon[0] / 30);
                if (midSign == startSign)
                    jdStart = mid;
                else
                    jdEnd = mid;
            }
            return (jdStart + jdEnd) / 2;
        }

        private string GetKalamSpan(double sunrise, double sunset, DayOfWeek day, string type)
        {
            double span = (sunset - sunrise) / 8;
            int index = type switch
            {
                "Rahu" => day switch
                {
                    DayOfWeek.Sunday => 8,
                    DayOfWeek.Monday => 2,
                    DayOfWeek.Tuesday => 7,
                    DayOfWeek.Wednesday => 5,
                    DayOfWeek.Thursday => 6,
                    DayOfWeek.Friday => 4,
                    DayOfWeek.Saturday => 3,
                    _ => 8
                },
                "Gulika" => day switch
                {
                    DayOfWeek.Sunday => 7,
                    DayOfWeek.Monday => 6,
                    DayOfWeek.Tuesday => 5,
                    DayOfWeek.Wednesday => 4,
                    DayOfWeek.Thursday => 3,
                    DayOfWeek.Friday => 2,
                    DayOfWeek.Saturday => 1,
                    _ => 7
                },
                "Yamaganda" => day switch
                {
                    DayOfWeek.Sunday => 5,
                    DayOfWeek.Monday => 4,
                    DayOfWeek.Tuesday => 3,
                    DayOfWeek.Wednesday => 2,
                    DayOfWeek.Thursday => 1,
                    DayOfWeek.Friday => 7,
                    DayOfWeek.Saturday => 6,
                    _ => 5
                },
                _ => 1
            };
            double start = sunrise + span * (index - 1);
            double end = start + span;
            return $"{DecimalToTime(start)} to {DecimalToTime(end)}";
        }

        private List<string> GetChogadiyaList(double start, double span, DayOfWeek day, bool isDay)
        {
            var namesDay = new[] { "Udveg", "Chara", "Laabh", "Amrit", "Kaala", "Shubh", "Rog", "Udveg" };
            var namesNight = new[] { "Shubh", "Chara", "Kaala", "Udveg", "Amrit", "Rog", "Laabh", "Shubh" };
            var result = new List<string>();
            double unit = span / 8;
            double blockStart = start;

            string[] chogNames = isDay ? namesDay : namesNight;
            for (int i = 0; i < 8; i++)
            {
                string startStr = DecimalToTime(blockStart);
                string endStr = DecimalToTime(blockStart + unit);
                result.Add($"{chogNames[i]}: {startStr} to {endStr}");
                blockStart += unit;
            }
            return result;
        }

        private double TimeToDecimal(DateTime dt)
        {
            return dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0;
        }

        private string DecimalToTime(double dec)
        {
            int hr = (int)dec;
            int min = (int)((dec - hr) * 60);
            int sec = (int)(((dec - hr) * 60 - min) * 60);
            if (hr >= 24) hr -= 24;
            return $"{hr:D2}:{min:D2}:{sec:D2}";
        }

        private DateTime JulianToDateTime(double jd, double tz, SwissEph swe)
        {
            int y = 0, m = 0, d = 0; double h = 0;
            swe.swe_revjul(jd, SwissEph.SE_GREG_CAL, ref y, ref m, ref d, ref h);
            int hr = (int)h;
            int min = (int)((h - hr) * 60);
            int sec = (int)(((h - hr) * 60 - min) * 60);
            return new DateTime(y, m, d, hr, min, sec).AddHours(tz);
        }
    }
}
