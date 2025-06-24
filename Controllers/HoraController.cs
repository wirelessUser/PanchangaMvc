using Microsoft.AspNetCore.Mvc;

namespace PanchangaMvc.Controllers
{
    public class HoraController : Controller
    {
        public class HoraSpan
        {
            public DateTime Date { get; set; }
            public string Planet { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }

        public IActionResult HoraTimings()
        {
            double lat = 27.88145, lon = 78.07464, tz = 5.5;
            string ephePath = "ephe";
            var swe = new SwissEphNet.SwissEph();
            swe.swe_set_ephe_path(ephePath);
            swe.swe_set_sid_mode(SwissEphNet.SwissEph.SE_SIDM_LAHIRI, 0, 0);

            var sunCalc = new SunriseSunsetCalculator(swe, lon, lat);
            var results = new List<HoraSpan>();

            string[] horaSequence = { "Saturn", "Jupiter", "Mars", "Sun", "Venus", "Mercury", "Moon" };

            for (int month = 1; month <= 12; month++)
            {
                int days = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= days; day++)
                {
                    DateTime date = new DateTime(2025, month, day);
                    var weekday = (int)date.DayOfWeek;

                    DateTime sunrise = sunCalc.GetSunrise(date, tz);
                    DateTime sunset = sunCalc.GetSunset(date, tz);

                    double totalDayMinutes = (sunset - sunrise).TotalMinutes;
                    double dayHoraMinutes = totalDayMinutes / 12.0;

                    double totalNightMinutes = (sunCalc.GetSunrise(date.AddDays(1), tz) - sunset).TotalMinutes;
                    double nightHoraMinutes = totalNightMinutes / 12.0;

                    // Get the starting Hora Lord for the day
                    string startPlanet = GetStartingPlanet(weekday);
                    int startIndex = Array.IndexOf(horaSequence, startPlanet);

                    // Day Hora
                    for (int i = 0; i < 12; i++)
                    {
                        var horaPlanet = horaSequence[(startIndex + i) % 7];
                        var start = sunrise.AddMinutes(i * dayHoraMinutes);
                        var end = sunrise.AddMinutes((i + 1) * dayHoraMinutes);

                        results.Add(new HoraSpan
                        {
                            Date = date,
                            Planet = horaPlanet,
                            StartTime = start.ToString("HH:mm:ss"),
                            EndTime = end.ToString("HH:mm:ss")
                        });
                    }

                    // Night Hora
                    for (int i = 0; i < 12; i++)
                    {
                        var horaPlanet = horaSequence[(startIndex + 12 + i) % 7];
                        var start = sunset.AddMinutes(i * nightHoraMinutes);
                        var end = sunset.AddMinutes((i + 1) * nightHoraMinutes);

                        results.Add(new HoraSpan
                        {
                            Date = date,
                            Planet = horaPlanet,
                            StartTime = start.ToString("HH:mm:ss"),
                            EndTime = end.ToString("HH:mm:ss")
                        });
                    }
                }
            }

            return View(results);
        }

        private string GetStartingPlanet(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                0 => "Sun",
                1 => "Moon",
                2 => "Mars",
                3 => "Mercury",
                4 => "Jupiter",
                5 => "Venus",
                6 => "Saturn",
                _ => "Sun"
            };
        }
    }
}
