
using Microsoft.AspNetCore.Mvc;
using SwissEphNet;


namespace PanchangaMvc.Controllers
{
    public class SunriseSunsetMoonController : Controller
    {
        public class SunMoonData
        {
            public DateTime Date { get; set; }
            public DateTime Sunrise { get; set; }
            public DateTime Sunset { get; set; }
            public DateTime Moonrise { get; set; }
            public DateTime Moonset { get; set; }
        }

        public IActionResult FullYearSunMoonData()
        {
            string ephePath = "ephe";  // Make sure your se1 files are inside wwwroot/ephe
            double lat = 27.88145;
            double lon = 78.07464;
         //   double lat = 27.88145, lon = 78.07464, tz = 5.5;
            double tz = 5.5;  // IST

            var swe = new SwissEph();
            swe.swe_set_ephe_path(ephePath);

            var geopos = new double[] { lon, lat, 0 };
            var results = new List<SunMoonData>();

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    DateTime date = new DateTime(2025, month, day);

                    DateTime sunrise = GetRiseSetTime(swe, date, SwissEph.SE_SUN, SwissEph.SE_CALC_RISE, geopos, tz);
                    DateTime sunset = GetRiseSetTime(swe, date, SwissEph.SE_SUN, SwissEph.SE_CALC_SET, geopos, tz);
                    DateTime moonrise = GetRiseSetTime(swe, date, SwissEph.SE_MOON, SwissEph.SE_CALC_RISE, geopos, tz);
                    DateTime moonset = GetRiseSetTime(swe, date, SwissEph.SE_MOON, SwissEph.SE_CALC_SET, geopos, tz);

                    results.Add(new SunMoonData
                    {
                        Date = date,
                        Sunrise = sunrise,
                        Sunset = sunset,
                        Moonrise = moonrise,
                        Moonset = moonset
                    });
                }
            }

            return View(results);
        }

        private DateTime GetRiseSetTime(SwissEph swe, DateTime date, int planet, int eventFlag, double[] geopos, double tz)
        {
            double jd = swe.swe_julday(date.Year, date.Month, date.Day, 0.0, SwissEph.SE_GREG_CAL);
            double tret = 0;
            string serr = "";

            int result = swe.swe_rise_trans(
                jd,
                planet,
                null,
                SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL,
                eventFlag,
                geopos,
                1013.25,
                23.0,
                ref tret,
                ref serr
            );

            if (result >= 0)
                return JulianToDateTime(tret, tz, swe);
            else
                return DateTime.MinValue;  // Means planet did not rise/set on that day
        }

        private DateTime JulianToDateTime(double jd, double tz, SwissEph swe)
        {
            int y = 0, m = 0, d = 0;
            double h = 0;
            swe.swe_revjul(jd, SwissEph.SE_GREG_CAL, ref y, ref m, ref d, ref h);

            int hr = (int)h;
            int min = (int)((h - hr) * 60);
            int sec = (int)(((h - hr) * 60 - min) * 60);

            return new DateTime(y, m, d, hr, min, sec, DateTimeKind.Utc).AddHours(tz);
        }
    }
}


