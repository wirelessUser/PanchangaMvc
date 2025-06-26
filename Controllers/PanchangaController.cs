using Microsoft.AspNetCore.Mvc;
using SwissEphNet;

namespace PanchangaMvc.Controllers
{
    public class AllInOnePanchangaController : Controller
    {
        private const string EphePath = "ephe";
        private const double Lat = 27.88145;
        private const double Lon = 78.07464;
        private const double Tz = 5.5;

        // ================== Model ==================
        public class PanchangaDayResult
        {
            public DateTime Date { get; set; }
            public string Weekday { get; set; }
            public string Sunrise { get; set; }
            public string Sunset { get; set; }
            public string Moonrise { get; set; }
            public string Moonset { get; set; }
            public string Tithi { get; set; }
            public string Nakshatra { get; set; }
            public string Yoga { get; set; }
            public string RahuKalam { get; set; }
            public string GulikaKalam { get; set; }
            public string Yamaganda { get; set; }
            public string Abhijit { get; set; }
        }

        // ================== Main Action ==================
        public IActionResult FullYearPanchanga()
        {
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphePath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0);
            var geopos = new double[] { Lon, Lat, 0 };

            var sunCalc = new SunriseSunsetCalculator(swe, Lon, Lat);
            var results = new List<PanchangaDayResult>();

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    DateTime date = new DateTime(2025, month, day);

                    DateTime sunrise = GetSunTime(swe, date, SwissEph.SE_CALC_RISE, geopos, Tz);
                    DateTime sunset = GetSunTime(swe, date, SwissEph.SE_CALC_SET, geopos, Tz);
                    DateTime moonrise = GetSunTime(swe, date, SwissEph.SE_CALC_RISE, geopos, Tz, SwissEph.SE_MOON);
                    DateTime moonset = GetSunTime(swe, date, SwissEph.SE_CALC_SET, geopos, Tz, SwissEph.SE_MOON);

                    string tithi = GetTithi(swe, sunrise);
                    string nakshatra = GetNakshatra(swe, sunrise);
                    string yoga = GetYoga(swe, sunrise);

                    string rahu = GetRahuKalam(sunrise, sunset, date.DayOfWeek);
                    string gulika = GetGulikaKalam(sunrise, sunset, date.DayOfWeek);
                    string yamaganda = GetYamagandaKalam(sunrise, sunset, date.DayOfWeek);
                    string abhijit = GetAbhijitMuhurtam(sunrise, sunset);

                    results.Add(new PanchangaDayResult
                    {
                        Date = date,
                        Weekday = date.DayOfWeek.ToString(),
                        Sunrise = sunrise.ToString("HH:mm:ss"),
                        Sunset = sunset.ToString("HH:mm:ss"),
                        Moonrise = moonrise != DateTime.MinValue ? moonrise.ToString("HH:mm:ss") : "Not Risen",
                        Moonset = moonset != DateTime.MinValue ? moonset.ToString("HH:mm:ss") : "Not Set",
                        Tithi = tithi,
                        Nakshatra = nakshatra,
                        Yoga = yoga,
                        RahuKalam = rahu,
                        GulikaKalam = gulika,
                        Yamaganda = yamaganda,
                        Abhijit = abhijit
                    });
                }
            }

            return View(results);
        }

        // ================== Helper Functions ==================

        private DateTime GetSunTime(SwissEph swe, DateTime date, int flag, double[] geopos, double tz, int planet = SwissEph.SE_SUN)
        {
            double jd = swe.swe_julday(date.Year, date.Month, date.Day, 0, SwissEph.SE_GREG_CAL);
            double tret = 0;
            string serr = "";
            int result = swe.swe_rise_trans(jd, planet, null, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, flag, geopos, 1013.25, 23.0, ref tret, ref serr);

            if (result >= 0)
                return JulianToDateTime(tret, tz, swe);
            else
                return DateTime.MinValue;
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

        private string GetTithi(SwissEph swe, DateTime dt)
        {
            double jd = swe.swe_julday(dt.Year, dt.Month, dt.Day, dt.TimeOfDay.TotalHours, SwissEph.SE_GREG_CAL);
            string serr = "";
            double[] sun = new double[6], moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            double diff = (moon[0] - sun[0] + 360) % 360;
            int index = (int)(diff / 12.0);
            string[] tithiNames = { "Shukla Pratipada", "Shukla Dvitiya", "Shukla Tritiya", /*...*/ "Amavasya" };
            return tithiNames[Math.Min(index, 29)];
        }

        private string GetNakshatra(SwissEph swe, DateTime dt)
        {
            double jd = swe.swe_julday(dt.Year, dt.Month, dt.Day, dt.TimeOfDay.TotalHours, SwissEph.SE_GREG_CAL);
            string serr = "";
            double[] moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            int index = (int)(moon[0] / (360.0 / 27));
            string[] nakshatras = { "Ashwini", "Bharani", "Krittika", /*...*/ "Revati" };
            return nakshatras[Math.Min(index, 26)];
        }

        private string GetYoga(SwissEph swe, DateTime dt)
        {
            double jd = swe.swe_julday(dt.Year, dt.Month, dt.Day, dt.TimeOfDay.TotalHours, SwissEph.SE_GREG_CAL);
            string serr = "";
            double[] sun = new double[6], moon = new double[6];
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            double sum = (sun[0] + moon[0]) % 360;
            int index = (int)(sum / (360.0 / 27));
            string[] yogas = { "Vishkambha", "Priti", "Ayushman", /*...*/ "Vaidhriti" };
            return yogas[Math.Min(index, 26)];
        }

        private string GetRahuKalam(DateTime sunrise, DateTime sunset, DayOfWeek day)
        {
            double duration = (sunset - sunrise).TotalMinutes / 8.0;
            int rahuSegment = day switch
            {
                DayOfWeek.Sunday => 8,
                DayOfWeek.Monday => 2,
                DayOfWeek.Tuesday => 7,
                DayOfWeek.Wednesday => 5,
                DayOfWeek.Thursday => 6,
                DayOfWeek.Friday => 4,
                DayOfWeek.Saturday => 3,
                _ => 8
            };
            var start = sunrise.AddMinutes((rahuSegment - 1) * duration);
            var end = start.AddMinutes(duration);
            return $"{start:HH:mm}-{end:HH:mm}";
        }

        private string GetGulikaKalam(DateTime sunrise, DateTime sunset, DayOfWeek day)
        {
            double duration = (sunset - sunrise).TotalMinutes / 8.0;
            int gulikaSegment = day switch
            {
                DayOfWeek.Sunday => 7,
                DayOfWeek.Monday => 6,
                DayOfWeek.Tuesday => 5,
                DayOfWeek.Wednesday => 4,
                DayOfWeek.Thursday => 3,
                DayOfWeek.Friday => 2,
                DayOfWeek.Saturday => 1,
                _ => 7
            };
            var start = sunrise.AddMinutes((gulikaSegment - 1) * duration);
            var end = start.AddMinutes(duration);
            return $"{start:HH:mm}-{end:HH:mm}";
        }

        private string GetYamagandaKalam(DateTime sunrise, DateTime sunset, DayOfWeek day)
        {
            double duration = (sunset - sunrise).TotalMinutes / 8.0;
            int yamaSegment = day switch
            {
                DayOfWeek.Sunday => 5,
                DayOfWeek.Monday => 4,
                DayOfWeek.Tuesday => 3,
                DayOfWeek.Wednesday => 2,
                DayOfWeek.Thursday => 1,
                DayOfWeek.Friday => 8,
                DayOfWeek.Saturday => 7,
                _ => 5
            };
            var start = sunrise.AddMinutes((yamaSegment - 1) * duration);
            var end = start.AddMinutes(duration);
            return $"{start:HH:mm}-{end:HH:mm}";
        }

        private string GetAbhijitMuhurtam(DateTime sunrise, DateTime sunset)
        {
            double totalDay = (sunset - sunrise).TotalMinutes;
            double midDay = sunrise.AddMinutes(totalDay / 2).TimeOfDay.TotalMinutes;

            var start = midDay - (24 / 60.0); // ~24 minutes before mid
            var end = midDay + (24 / 60.0);   // ~24 minutes after mid

            var startTime = sunrise.AddMinutes(start - sunrise.TimeOfDay.TotalMinutes);
            var endTime = sunrise.AddMinutes(end - sunrise.TimeOfDay.TotalMinutes);

            if (startTime < sunrise || endTime > sunset)
                return "None";

            return $"{startTime:HH:mm}-{endTime:HH:mm}";
        }
    }
}
