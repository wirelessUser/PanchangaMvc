using Microsoft.AspNetCore.Mvc;
using SwissEphNet;

namespace PanchangaMvc.Controllers
{
    public class YogaController : Controller
    {
        public class YogaResult
        {
            public DateTime Date => StartTime.Date;
            public string YogaName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        private readonly string[] yogaNames = {
            "Vishkambha", "Priti", "Ayushman", "Saubhagya", "Shobhana",
            "Atiganda", "Sukarma", "Dhriti", "Shoola", "Ganda",
            "Vriddhi", "Dhruva", "Vyaghata", "Harshana", "Vajra",
            "Siddhi", "Vyatipata", "Variyana", "Parigha", "Shiva",
            "Siddha", "Sadhya", "Shubha", "Shukla", "Brahma",
            "Indra", "Vaidhriti"
        };

        public IActionResult FullYearYoga()
        {
            string ephePath = "ephe";
            double lat = 28.63576, lon = 77.22445, tz = 5.5;

            var swe = new SwissEph();
            swe.swe_set_ephe_path(ephePath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

            var results = new List<YogaResult>();
            var sunCalc = new SunriseSunsetCalculator(swe, lon, lat);

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    var date = new DateTime(2025, month, day);
                    DateTime sunrise = sunCalc.GetSunrise(date, tz);
                    DateTime nextSunrise = sunCalc.GetSunrise(date.AddDays(1), tz);

                    double jdStart = ToJulian(sunrise.AddHours(-tz), swe);
                    double jdEnd = ToJulian(nextSunrise.AddHours(-tz), swe);
                    double currentJD = jdStart;
                    int currentYoga = GetYogaIndex(currentJD, swe);

                    while (currentJD < jdEnd)
                    {
                        double changeJD = BinarySearchYogaChange(currentJD, jdEnd, currentYoga, swe);
                        results.Add(new YogaResult
                        {
                            StartTime = JulianToDateTime(currentJD, tz, swe),
                            EndTime = JulianToDateTime(changeJD, tz, swe),
                            YogaName = yogaNames[currentYoga]
                        });

                        currentJD = changeJD + 1.0 / (24 * 60 * 60);
                        currentYoga = GetYogaIndex(currentJD, swe);
                    }
                }
            }

            return View(results);
        }

        private int GetYogaIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] sun = new double[6];
            double[] moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            double sum = (sun[0] + moon[0]) % 360;
            return Math.Min(26, (int)(sum / (360.0 / 27)));
        }

        private double BinarySearchYogaChange(double low, double high, int currentIndex, SwissEph swe)
        {
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (GetYogaIndex(mid, swe) == currentIndex)
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
