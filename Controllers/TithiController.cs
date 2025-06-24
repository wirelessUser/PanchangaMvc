using Microsoft.AspNetCore.Mvc;
using PanchangaMvc.Models;
using SwissEphNet;
namespace PanchangaMvc.Controllers
{
    public class TithiController : Controller
    {
        public class TithiResult
        {
            public string TithiName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public DateTime Date => StartTime.Date;
        }

        private string[] GetTithiNames() => new[]
        {
            "Shukla Pratipada", "Shukla Dvitiya", "Shukla Tritiya", "Shukla Chaturthi", "Shukla Panchami",
            "Shukla Shashthi", "Shukla Saptami", "Shukla Ashtami", "Shukla Navami", "Shukla Dashami",
            "Shukla Ekadashi", "Shukla Dwadashi", "Shukla Trayodashi", "Shukla Chaturdashi", "Purnima",
            "Krishna Pratipada", "Krishna Dvitiya", "Krishna Tritiya", "Krishna Chaturthi", "Krishna Panchami",
            "Krishna Shashthi", "Krishna Saptami", "Krishna Ashtami", "Krishna Navami", "Krishna Dashami",
            "Krishna Ekadashi", "Krishna Dwadashi", "Krishna Trayodashi", "Krishna Chaturdashi", "Amavasya"
        };

        public IActionResult FullYearTithi()
        {
            string ephePath = "ephe";
            double lat = 27.88145, lon = 78.07464, tz = 5.5;

            var swe = new SwissEph();
            swe.swe_set_ephe_path(ephePath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

            var sunCalc = new SunriseSunsetCalculator(swe, lon, lat);
            var tithis = new List<TithiResult>();
            var names = GetTithiNames();

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    DateTime localDate = new DateTime(2025, month, day);
                    DateTime sunrise = sunCalc.GetSunrise(localDate, tz);
                    DateTime nextSunrise = sunCalc.GetSunrise(localDate.AddDays(1), tz);

                    double jdStart = ToJulian(sunrise.AddHours(-tz), swe);
                    double jdEnd = ToJulian(nextSunrise.AddHours(-tz), swe);
                    double currentJD = jdStart;
                    int currentIndex = GetTithiIndex(currentJD, swe);

                    while (currentJD < jdEnd)
                    {
                        double changeJD = BinarySearchTithiChange(currentJD, jdEnd, currentIndex, swe);

                        tithis.Add(new TithiResult
                        {
                            TithiName = names[currentIndex],
                            StartTime = JulianToDateTime(currentJD, tz, swe),
                            EndTime = JulianToDateTime(changeJD, tz, swe)
                        });

                        currentJD = changeJD + (1.0 / (24 * 60 * 60)); // step 1 second
                        currentIndex = GetTithiIndex(currentJD, swe);
                    }
                }
            }

            return View(tithis);
        }

        private double BinarySearchTithiChange(double low, double high, int currentIndex, SwissEph swe)
        {
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (GetTithiIndex(mid, swe) == currentIndex)
                    low = mid;
                else
                    high = mid;
            }
            return (low + high) / 2;
        }

        private int GetTithiIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] sun = new double[6], moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            double diff = (moon[0] - sun[0] + 360) % 360;
            return Math.Min(29, (int)(diff / 12.0));
        }

        private double ToJulian(DateTime dt, SwissEph swe)
        {
            return swe.swe_julday(dt.Year, dt.Month, dt.Day,
                dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0, SwissEph.SE_GREG_CAL);
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
