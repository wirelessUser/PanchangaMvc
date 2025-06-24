using Microsoft.AspNetCore.Mvc;
using SwissEphNet;

namespace PanchangaMvc.Controllers
{
    public class KaranaController : Controller
    {
        public class KaranaResult
        {
            public DateTime Date => StartTime.Date;
            public string KaranaName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        private readonly string[] karanaCycle = {
            "Bava", "Balava", "Kaulava", "Taitila", "Garaja", "Vanija", "Vishti"
        };

        private readonly string[] fixedKaranas = {
            "Shakuni", "Chatushpada", "Naga", "Kimstughna"
        };

        public IActionResult FullYearKarana()
        {
            string ephePath = "ephe";
            double lat = 27.88145, lon = 78.07464, tz = 5.5;
            var swe = new SwissEph();

            swe.swe_set_ephe_path(ephePath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);

            var sunCalc = new SunriseSunsetCalculator(swe, lon, lat);
            var results = new List<KaranaResult>();

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

                    while (currentJD < jdEnd)
                    {
                        int karanaIndex = GetKaranaIndex(currentJD, swe);
                        double changeJD = BinarySearchKaranaChange(currentJD, jdEnd, karanaIndex, swe);

                        string name = karanaIndex >= 56
                            ? fixedKaranas[(karanaIndex - 56) % 4]
                            : karanaCycle[karanaIndex % 7];

                        results.Add(new KaranaResult
                        {
                            KaranaName = name,
                            StartTime = JulianToDateTime(currentJD, tz, swe),
                            EndTime = JulianToDateTime(changeJD, tz, swe)
                        });

                        currentJD = changeJD + 1.0 / (24 * 60 * 60);
                    }
                }
            }

            return View(results);
        }

        private int GetKaranaIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] sun = new double[6], moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);

            double angle = (moon[0] - sun[0] + 360) % 360;

            if (angle <= 6.0) return 60;                     // Kimstughna
            if (angle > 354.0) return 59;                    // Naga
            if (angle > 348.0) return 58;                    // Chatushpada
            if (angle > 342.0) return 57;                    // Shakuni

            angle -= 6.0;
            return (int)(angle / 6.0);
        }

        private double BinarySearchKaranaChange(double low, double high, int currentIndex, SwissEph swe)
        {
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (GetKaranaIndex(mid, swe) == currentIndex)
                    low = mid;
                else
                    high = mid;
            }
            return (low + high) / 2;
        }

        private double ToJulian(DateTime dt, SwissEph swe)
        {
            return swe.swe_julday(dt.Year, dt.Month, dt.Day,
                dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0, SwissEph.SE_GREG_CAL);
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
