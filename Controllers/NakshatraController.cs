using Microsoft.AspNetCore.Mvc;
using SwissEphNet;

namespace PanchangaMvc.Controllers
{
    
        public class NakshatraController : Controller
        {
            public class NakshatraResult
            {
                public DateTime Date => StartTime.Date;
                public string NakshatraName { get; set; }
                public DateTime StartTime { get; set; }
                public DateTime EndTime { get; set; }
            }

            private readonly string[] nakshatraNames =
            {
            "Ashwini", "Bharani", "Krittika", "Rohini", "Mrigashirsha",
            "Ardra", "Punarvasu", "Pushya", "Ashlesha", "Magha",
            "Purva Phalguni", "Uttara Phalguni", "Hasta", "Chitra", "Swati",
            "Vishakha", "Anuradha", "Jyeshtha", "Mula", "Purva Ashadha",
            "Uttara Ashadha", "Shravana", "Dhanishta", "Shatabhisha", "Purva Bhadrapada",
            "Uttara Bhadrapada", "Revati", "Abhijit"
        };

            public IActionResult FullYearNakshatra()
            {
                string ephePath = "ephe";
            double lat = 27.88145, lon = 78.07464, tz = 5.5;
            var swe = new SwissEph();

                swe.swe_set_ephe_path(ephePath);
                swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

                var sunCalc = new SunriseSunsetCalculator(swe, lon, lat);
                var results = new List<NakshatraResult>();

                for (int month = 1; month <= 12; month++)
                {
                    int days = DateTime.DaysInMonth(2025, month);
                    for (int day = 1; day <= days; day++)
                    {
                        DateTime date = new DateTime(2025, month, day);
                        DateTime sunrise = sunCalc.GetSunrise(date, tz);
                        DateTime nextSunrise = sunCalc.GetSunrise(date.AddDays(1), tz);

                        double jdStart = ToJulian(sunrise.AddHours(-tz), swe);
                        double jdEnd = ToJulian(nextSunrise.AddHours(-tz), swe);
                        double currentJD = jdStart;

                        int currentIndex = GetNakshatraIndex(currentJD, swe);
                        while (currentJD < jdEnd)
                        {
                            double changeJD = BinarySearchNakshatraChange(currentJD, jdEnd, currentIndex, swe);

                            results.Add(new NakshatraResult
                            {
                                NakshatraName = nakshatraNames[currentIndex],
                                StartTime = JulianToDateTime(currentJD, tz, swe),
                                EndTime = JulianToDateTime(changeJD, tz, swe)
                            });

                            currentJD = changeJD + 1.0 / (24 * 60 * 60);
                            currentIndex = GetNakshatraIndex(currentJD, swe);
                        }
                    }
                }

                return View(results);
            }

            private int GetNakshatraIndex(double jd, SwissEph swe)
            {
                string serr = "";
                double[] moon = new double[6];
                swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
                double angle = moon[0];

                // Handle Abhijit separately (optional)
                if (angle >= 276.25 && angle <= 280.5)
                    return 27; // Abhijit

                int index = (int)(angle / (360.0 / 27));
                return Math.Min(index, 26); // limit to 0–26 for regular nakshatras
            }

            private double BinarySearchNakshatraChange(double low, double high, int currentIndex, SwissEph swe)
            {
                for (int i = 0; i < 30; i++)
                {
                    double mid = (low + high) / 2;
                    if (GetNakshatraIndex(mid, swe) == currentIndex)
                        low = mid;
                    else
                        high = mid;
                }
                return (low + high) / 2;
            }

            private double ToJulian(DateTime dt, SwissEph swe)
            {
                return swe.swe_julday(dt.Year, dt.Month, dt.Day,
                    dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0,
                    SwissEph.SE_GREG_CAL);
            }

            private DateTime JulianToDateTime(double jd, double tz, SwissEph swe)
            {
                int y = 0, m = 0, d = 0;
                double h = 0;
                swe.swe_revjul(jd, SwissEph.SE_GREG_CAL, ref y, ref m, ref d, ref h);
                int hr = (int)h;
                int min = (int)((h - hr) * 60);
                int sec = (int)(((h - hr) * 60 - min) * 60);
                return new DateTime(y, m, d, hr, min, sec).AddHours(tz);
            }
        }
    }

