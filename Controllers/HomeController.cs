using Microsoft.AspNetCore.Mvc;
using PanchangaMvc.Models; // Ensure this namespace is correctly used
using SwissEphNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static PanchangaMvc.Controllers.SunriseSunsetMoonController;
using static PanchangaMvc.Controllers.TithiController;
using static PanchangaMvc.Controllers.YogaController;

namespace PanchangaMvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger; // Standard logger for MVC controllers

        // Constants for location and timezone
        private const double Latitude = 27.88145;
        private const double Longitude = 78.07464;
        private const double TimeZoneOffset = 5.5; // IST (UTC+5:30)
        private const string EphemerisPath = "ephe"; // Path to your SwissEph ephemeris files (e.g., se1 files)

        // Arrays for astrological names
        private readonly string[] _nakshatraNames =
        {
            "Ashwini", "Bharani", "Krittika", "Rohini", "Mrigashirsha",
            "Ardra", "Punarvasu", "Pushya", "Ashlesha", "Magha",
            "Purva Phalguni", "Uttara Phalguni", "Hasta", "Chitra", "Swati",
            "Vishakha", "Anuradha", "Jyeshtha", "Mula", "Purva Ashadha",
            "Uttara Ashadha", "Shravana", "Dhanishta", "Shatabhisha", "Purva Bhadrapada",
            "Uttara Bhadrapada", "Revati", "Abhijit" // Abhijit is 28th, but often calculated separately or as part of Shravana
        };

        private readonly string[] _tithiNames =
        {
            "Shukla Pratipada", "Shukla Dvitiya", "Shukla Tritiya", "Shukla Chaturthi", "Shukla Panchami",
            "Shukla Shashthi", "Shukla Saptami", "Shukla Ashtami", "Shukla Navami", "Shukla Dashami",
            "Shukla Ekadashi", "Shukla Dwadashi", "Shukla Trayodashi", "Shukla Chaturdashi", "Purnima",
            "Krishna Pratipada", "Krishna Dvitiya", "Krishna Tritiya", "Krishna Chaturthi", "Krishna Panchami",
            "Krishna Shashthi", "Krishna Saptami", "Krishna Ashtami", "Krishna Navami", "Krishna Dashami",
            "Krishna Ekadashi", "Krishna Dwadashi", "Krishna Trayodashi", "Krishna Chaturdashi", "Amavasya"
        };

        private readonly string[] _yogaNames = {
            "Vishkambha", "Priti", "Ayushman", "Saubhagya", "Shobhana",
            "Atiganda", "Sukarma", "Dhriti", "Shoola", "Ganda",
            "Vriddhi", "Dhruva", "Vyaghata", "Harshana", "Vajra",
            "Siddhi", "Vyatipata", "Variyana", "Parigha", "Shiva",
            "Siddha", "Sadhya", "Shubha", "Shukla", "Brahma",
            "Indra", "Vaidhriti"
        };

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Default Index Action
        public IActionResult Index()
        {
            return View();
        }

        #region Hora Calculations

        /// <summary>
        /// Calculates Hora timings for a full year (2025).
        /// Hora is a Vedic time division, approximately 1 hour long, ruled by a specific planet.
        /// </summary>
        /// <returns>A view with a list of Hora spans.</returns>
        /// 
        public class HoraSpan
        {
            public DateTime Date { get; set; }
            public string Planet { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
        }
        public IActionResult HoraTimings()
        {
            // Initialize SwissEph library
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphemerisPath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0); // Set sidereal mode to Lahiri

            // Initialize SunriseSunsetCalculator for the specified location
            var sunCalc = new SunriseSunsetCalculator(swe, Longitude, Latitude);
            var results = new List<HoraSpan>();

            // The fixed sequence of planets for Hora calculation
            string[] horaSequence = { "Saturn", "Jupiter", "Mars", "Sun", "Venus", "Mercury", "Moon" };

            // Loop through each day of the year 2025
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime date = new DateTime(2025, month, day);
                    var weekday = (int)date.DayOfWeek; // 0 = Sunday, 1 = Monday, etc.

                    // Get sunrise and sunset times for the current date
                    DateTime sunrise = sunCalc.GetSunrise(date, TimeZoneOffset);
                    DateTime sunset = sunCalc.GetSunset(date, TimeZoneOffset);

                    // Ensure valid sunrise/sunset times before proceeding
                    if (sunrise == DateTime.MinValue || sunset == DateTime.MinValue)
                    {
                        // Log or handle cases where sunrise/sunset cannot be calculated (e.g., polar regions)
                        _logger.LogWarning($"Sunrise or Sunset not found for {date.ToShortDateString()}");
                        continue;
                    }

                    // Calculate duration of day (sunrise to sunset) and night (sunset to next sunrise)
                    double totalDayMinutes = (sunset - sunrise).TotalMinutes;
                    double dayHoraMinutes = totalDayMinutes / 12.0; // 12 horas in a day

                    // Calculate next day's sunrise to get full night duration
                    DateTime nextDaySunrise = sunCalc.GetSunrise(date.AddDays(1), TimeZoneOffset);
                    if (nextDaySunrise == DateTime.MinValue)
                    {
                        _logger.LogWarning($"Next day sunrise not found for {date.ToShortDateString()}");
                        continue;
                    }
                    double totalNightMinutes = (nextDaySunrise - sunset).TotalMinutes;
                    double nightHoraMinutes = totalNightMinutes / 12.0; // 12 horas in a night

                    // Determine the starting Hora Lord for the day based on the weekday
                    string startPlanet = GetStartingPlanet(weekday);
                    int startIndex = Array.IndexOf(horaSequence, startPlanet);

                    // Calculate and add Day Horas
                    for (int i = 0; i < 12; i++)
                    {
                        var horaPlanet = horaSequence[(startIndex + i) % 7]; // Cycle through the 7 planets
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

                    // Calculate and add Night Horas
                    // The night Hora sequence starts from the 5th planet from the day's starting planet.
                    // (startIndex + 12) effectively brings us back to the starting planet for the 13th hora
                    // and then we continue the sequence.
                    for (int i = 0; i < 12; i++)
                    {
                        // The sequence wraps around. After Sun (0), the next in sequence is Moon (6) relative to sequence start.
                        // (startIndex + 12 + i) ensures continuation of the planetary order from the last day hora.
                        // For example, if day starts with Sun (index 3), 12th day hora is Mercury (index 5).
                        // The 1st night hora starts from the planet of the 5th hora of the day.
                        // A simpler rule: The lord of the first night hora is the planet ruling the 5th day hora (from the day's ruling planet).
                        // However, the provided formula (startIndex + 12 + i) % 7 implies a continuous cycle.
                        // Let's stick to the original code's logic for now unless explicitly asked to change the astrological rule.
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

        /// <summary>
        /// Determines the ruling planet for the first Hora of the day based on the weekday.
        /// </summary>
        /// <param name="dayOfWeek">Integer representing the day of the week (0=Sunday, 1=Monday, etc.).</param>
        /// <returns>The name of the ruling planet.</returns>
        private string GetStartingPlanet(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                // Sunday => Sun, Monday => Moon, Tuesday => Mars, etc.
                0 => "Sun",
                1 => "Moon",
                2 => "Mars",
                3 => "Mercury",
                4 => "Jupiter",
                5 => "Venus",
                6 => "Saturn",
                _ => "Sun" // Default to Sun for safety, though dayOfWeek should be 0-6
            };
        }

        #endregion

        #region Nakshatra Calculations

        /// <summary>
        /// Calculates Nakshatra timings for a full year (2025).
        /// Nakshatras are lunar mansions, determined by the Moon's position.
        /// </summary>
        /// <returns>A view with a list of Nakshatra results.</returns>
        /// 
        public class NakshatraResult
        {
            public DateTime Date => StartTime.Date;
            public string NakshatraName { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
        public IActionResult NakshatraTimings()
        {
            // Initialize SwissEph library
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphemerisPath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0); // Set sidereal mode

            // Initialize SunriseSunsetCalculator (though not directly used for nakshatra angles, used for daily bounds)
            var sunCalc = new SunriseSunsetCalculator(swe, Longitude, Latitude);
            var results = new List<NakshatraResult>();

            // Loop through each day of the year 2025
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(2025, month); // Changed to 2025 for consistency
                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime date = new DateTime(2025, month, day);

                    // Calculate Julian Day for sunrise of the current day and next day's sunrise
                    // These define the 24-hour period for which nakshatras are calculated.
                    DateTime sunrise = sunCalc.GetSunrise(date, TimeZoneOffset);
                    DateTime nextSunrise = sunCalc.GetSunrise(date.AddDays(1), TimeZoneOffset);

                    if (sunrise == DateTime.MinValue || nextSunrise == DateTime.MinValue)
                    {
                        _logger.LogWarning($"Sunrise not found for {date.ToShortDateString()} or {date.AddDays(1).ToShortDateString()} for Nakshatra calculation.");
                        continue;
                    }

                    // Convert local times to Julian Days (UTC for SwissEph calculations)
                    double jdStart = ToJulian(sunrise.AddHours(-TimeZoneOffset), swe);
                    double jdEnd = ToJulian(nextSunrise.AddHours(-TimeZoneOffset), swe);
                    double currentJD = jdStart;

                    // Get the initial Nakshatra for the start of the period
                    int currentIndex = GetNakshatraIndex(currentJD, swe);

                    // Iterate through the 24-hour period, identifying Nakshatra changes
                    while (currentJD < jdEnd)
                    {
                        // Find the Julian Day when the current Nakshatra changes
                        double changeJD = BinarySearchNakshatraChange(currentJD, jdEnd, currentIndex, swe);

                        // Add the current Nakshatra span to the results
                        results.Add(new NakshatraResult
                        {
                            NakshatraName = _nakshatraNames[currentIndex],
                            StartTime = JulianToDateTime(currentJD, TimeZoneOffset, swe),
                            EndTime = JulianToDateTime(changeJD, TimeZoneOffset, swe)
                        });

                        // Set the current Julian Day to the point just after the change
                        // Add a small epsilon to avoid re-evaluating the same boundary.
                        currentJD = changeJD + 1.0 / (24 * 60 * 60 * 1000); // Step by a millisecond (or smallest relevant unit)
                        currentIndex = GetNakshatraIndex(currentJD, swe); // Get the new Nakshatra
                    }
                }
            }
            return View(results);
        }

        /// <summary>
        /// Gets the Nakshatra index for a given Julian Day.
        /// Nakshatra is based on the Moon's sidereal longitude.
        /// </summary>
        /// <param name="jd">Julian Day (UT) for calculation.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The index of the Nakshatra (0-27).</returns>
        private int GetNakshatraIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] moon = new double[6]; // Position and speed array for Moon
            // Calculate Moon's position (longitude) in sidereal zodiac
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);
            double angle = moon[0]; // Moon's sidereal longitude

            // Each Nakshatra spans 360/27 = 13.333 degrees.
            // Abhijit Nakshatra (28th) is a special case, a small portion of Uttara Ashadha and Shravana.
            // Its calculation range might need adjustment based on specific astrological texts.
            // The provided range 276.25 and 280.5 degrees is a common approximation.
            if (angle >= 276.25 && angle <= 280.5)
            {
                return 27; // Abhijit
            }

            // Calculate index (0-26) for the 27 regular Nakshatras
            int index = (int)(angle / (360.0 / 27));
            return Math.Min(index, 26); // Ensure index stays within 0-26 for the main 27 nakshatras
        }

        /// <summary>
        /// Performs a binary search to find the precise Julian Day when a Nakshatra changes.
        /// </summary>
        /// <param name="low">Lower bound Julian Day.</param>
        /// <param name="high">Upper bound Julian Day.</param>
        /// <param name="currentIndex">The Nakshatra index we are searching to change from.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The Julian Day of the Nakshatra change.</returns>
        private double BinarySearchNakshatraChange(double low, double high, int currentIndex, SwissEph swe)
        {
            // Iterate a fixed number of times for precision (e.g., 30 iterations for high accuracy)
            for (int i = 0; i < 30; i++)
            {
                double mid = (low + high) / 2;
                if (GetNakshatraIndex(mid, swe) == currentIndex)
                    low = mid; // If mid is still in the same Nakshatra, move the lower bound up
                else
                    high = mid; // If mid is in the next Nakshatra, move the upper bound down
            }
            return (low + high) / 2; // Return the approximate change point
        }

        #endregion

        #region SunriseSunsetMoon Calculations

        /// <summary>
        /// Calculates Sunrise, Sunset, Moonrise, and Moonset times for a full year (2025).
        /// </summary>
        /// <returns>A view with a list of SunMoonData.</returns>
        public IActionResult FullYearSunMoonData()
        {
            // Initialize SwissEph library
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphemerisPath);

            // Geographical position array: [longitude, latitude, altitude]
            var geopos = new double[] { Longitude, Latitude, 0 };
            var results = new List<SunMoonData>();

            // Loop through each day of the year 2025
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime date = new DateTime(2025, month, day);

                    // Calculate rise/set times for Sun and Moon
                    DateTime sunrise = GetRiseSetTime(swe, date, SwissEph.SE_SUN, SwissEph.SE_CALC_RISE, geopos, TimeZoneOffset);
                    DateTime sunset = GetRiseSetTime(swe, date, SwissEph.SE_SUN, SwissEph.SE_CALC_SET, geopos, TimeZoneOffset);
                    DateTime moonrise = GetRiseSetTime(swe, date, SwissEph.SE_MOON, SwissEph.SE_CALC_RISE, geopos, TimeZoneOffset);
                    DateTime moonset = GetRiseSetTime(swe, date, SwissEph.SE_MOON, SwissEph.SE_CALC_SET, geopos, TimeZoneOffset);

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

        /// <summary>
        /// Helper method to get rise or set time for a celestial body.
        /// </summary>
        /// <param name="swe">SwissEph instance.</param>
        /// <param name="date">The date for the calculation.</param>
        /// <param name="planet">The planet ID (e.g., SwissEph.SE_SUN, SwissEph.SE_MOON).</param>
        /// <param name="eventFlag">The event flag (SwissEph.SE_CALC_RISE or SwissEph.SE_CALC_SET).</param>
        /// <param name="geopos">Geographical position [longitude, latitude, altitude].</param>
        /// <param name="tz">Timezone offset.</param>
        /// <returns>The DateTime of the event, or DateTime.MinValue if no event occurs.</returns>
        private DateTime GetRiseSetTime(SwissEph swe, DateTime date, int planet, int eventFlag, double[] geopos, double tz)
        {
            // Julian Day for the beginning of the day (00:00 UT)
            double jd = swe.swe_julday(date.Year, date.Month, date.Day, 0.0, SwissEph.SE_GREG_CAL);
            double tret = 0; // Output Julian Day of the event
            string serr = ""; // Error string

            // Call SwissEph rise/transit function
            int result = swe.swe_rise_trans(
                jd,                 // Julian Day for the calculation period start
                planet,             // ID of the celestial body
                null,               // House cusps (not used for rise/set)
                SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, // Calculation flags
                eventFlag,          // Event type (rise or set)
                geopos,             // Geographical position
                1013.25,            // Atmospheric pressure in hPa
                23.0,               // Temperature in Celsius
                ref tret,           // Output: Julian Day of the event
                ref serr            // Output: Error message
            );

            // If result is non-negative, an event was found
            if (result >= 0)
                return JulianToDateTime(tret, tz, swe);
            else
                return DateTime.MinValue; // Indicates planet did not rise/set on that day/location
        }

        #endregion

        #region Tithi Calculations

        /// <summary>
        /// Calculates Tithi timings for a full year (2025).
        /// Tithi is a lunar day, based on the angular distance between the Sun and Moon.
        /// </summary>
        /// <returns>A view with a list of Tithi results.</returns>
        public IActionResult FullYearTithi()
        {
            // Initialize SwissEph library
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphemerisPath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0); // Set sidereal mode

            // Initialize SunriseSunsetCalculator for obtaining daily period bounds (sunrise to next sunrise)
            var sunCalc = new SunriseSunsetCalculator(swe, Longitude, Latitude);
            var tithis = new List<TithiResult>();

            // Loop through each day of the year 2025
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    DateTime localDate = new DateTime(2025, month, day);

                    // Define the 24-hour period from sunrise to next sunrise for calculations
                    DateTime sunrise = sunCalc.GetSunrise(localDate, TimeZoneOffset);
                    DateTime nextSunrise = sunCalc.GetSunrise(localDate.AddDays(1), TimeZoneOffset);

                    if (sunrise == DateTime.MinValue || nextSunrise == DateTime.MinValue)
                    {
                        _logger.LogWarning($"Sunrise not found for {localDate.ToShortDateString()} or {localDate.AddDays(1).ToShortDateString()} for Tithi calculation.");
                        continue;
                    }

                    // Convert local times to Julian Days (UTC for SwissEph calculations)
                    double jdStart = ToJulian(sunrise.AddHours(-TimeZoneOffset), swe);
                    double jdEnd = ToJulian(nextSunrise.AddHours(-TimeZoneOffset), swe);
                    double currentJD = jdStart;

                    // Get the initial Tithi for the start of the period
                    int currentIndex = GetTithiIndex(currentJD, swe);

                    // Iterate through the 24-hour period, identifying Tithi changes
                    while (currentJD < jdEnd)
                    {
                        // Find the Julian Day when the current Tithi changes
                        double changeJD = BinarySearchTithiChange(currentJD, jdEnd, currentIndex, swe);

                        // Add the current Tithi span to the results
                        tithis.Add(new TithiResult
                        {
                            TithiName = _tithiNames[currentIndex],
                            StartTime = JulianToDateTime(currentJD, TimeZoneOffset, swe),
                            EndTime = JulianToDateTime(changeJD, TimeZoneOffset, swe)
                        });

                        // Set the current Julian Day to the point just after the change
                        currentJD = changeJD + 1.0 / (24 * 60 * 60 * 1000); // Step by a millisecond
                        currentIndex = GetTithiIndex(currentJD, swe); // Get the new Tithi
                    }
                }
            }
            return View(tithis);
        }

        /// <summary>
        /// Gets the Tithi index for a given Julian Day.
        /// Tithi is calculated based on the angular difference between Moon and Sun (sidereal longitudes).
        /// </summary>
        /// <param name="jd">Julian Day (UT) for calculation.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The index of the Tithi (0-29).</returns>
        private int GetTithiIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] sun = new double[6], moon = new double[6]; // Position and speed arrays

            // Calculate Sun's and Moon's sidereal longitudes
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);

            // Calculate the angular difference (Moon - Sun), ensuring it's positive
            double diff = (moon[0] - sun[0] + 360) % 360;

            // Each Tithi spans 12 degrees (360 degrees / 30 tithis).
            return Math.Min(29, (int)(diff / 12.0)); // Index 0-29
        }

        /// <summary>
        /// Performs a binary search to find the precise Julian Day when a Tithi changes.
        /// </summary>
        /// <param name="low">Lower bound Julian Day.</param>
        /// <param name="high">Upper bound Julian Day.</param>
        /// <param name="currentIndex">The Tithi index we are searching to change from.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The Julian Day of the Tithi change.</returns>
        private double BinarySearchTithiChange(double low, double high, int currentIndex, SwissEph swe)
        {
            for (int i = 0; i < 30; i++) // 30 iterations for high precision
            {
                double mid = (low + high) / 2;
                if (GetTithiIndex(mid, swe) == currentIndex)
                    low = mid;
                else
                    high = mid;
            }
            return (low + high) / 2;
        }

        #endregion

        #region Yoga Calculations

        /// <summary>
        /// Calculates Yoga timings for a full year (2025).
        /// Yoga is based on the sum of the Sun's and Moon's sidereal longitudes.
        /// </summary>
        /// <returns>A view with a list of Yoga results.</returns>
        public IActionResult FullYearYoga()
        {
            // Initialize SwissEph library
            var swe = new SwissEph();
            swe.swe_set_ephe_path(EphemerisPath);
            swe.swe_set_sid_mode(SwissEph.SE_SIDM_LAHIRI, 0, 0); // Set sidereal mode

            // Initialize SunriseSunsetCalculator for obtaining daily period bounds (sunrise to next sunrise)
            var results = new List<YogaResult>();
            var sunCalc = new SunriseSunsetCalculator(swe, Longitude, Latitude);

            // Loop through each day of the year 2025
            for (int month = 1; month <= 12; month++)
            {
                int daysInMonth = DateTime.DaysInMonth(2025, month);
                for (int day = 1; day <= daysInMonth; day++)
                {
                    var date = new DateTime(2025, month, day);

                    // Define the 24-hour period from sunrise to next sunrise for calculations
                    DateTime sunrise = sunCalc.GetSunrise(date, TimeZoneOffset);
                    DateTime nextSunrise = sunCalc.GetSunrise(date.AddDays(1), TimeZoneOffset);

                    if (sunrise == DateTime.MinValue || nextSunrise == DateTime.MinValue)
                    {
                        _logger.LogWarning($"Sunrise not found for {date.ToShortDateString()} or {date.AddDays(1).ToShortDateString()} for Yoga calculation.");
                        continue;
                    }

                    // Convert local times to Julian Days (UTC for SwissEph calculations)
                    double jdStart = ToJulian(sunrise.AddHours(-TimeZoneOffset), swe);
                    double jdEnd = ToJulian(nextSunrise.AddHours(-TimeZoneOffset), swe);
                    double currentJD = jdStart;

                    // Get the initial Yoga for the start of the period
                    int currentYoga = GetYogaIndex(currentJD, swe);

                    // Iterate through the 24-hour period, identifying Yoga changes
                    while (currentJD < jdEnd)
                    {
                        // Find the Julian Day when the current Yoga changes
                        double changeJD = BinarySearchYogaChange(currentJD, jdEnd, currentYoga, swe);
                        results.Add(new YogaResult
                        {
                            StartTime = JulianToDateTime(currentJD, TimeZoneOffset, swe),
                            EndTime = JulianToDateTime(changeJD, TimeZoneOffset, swe),
                            YogaName = _yogaNames[currentYoga]
                        });

                        // Set the current Julian Day to the point just after the change
                        currentJD = changeJD + 1.0 / (24 * 60 * 60 * 1000); // Step by a millisecond
                        currentYoga = GetYogaIndex(currentJD, swe); // Get the new Yoga
                    }
                }
            }
            return View(results);
        }

        /// <summary>
        /// Gets the Yoga index for a given Julian Day.
        /// Yoga is calculated based on the sum of the Sun's and Moon's sidereal longitudes.
        /// </summary>
        /// <param name="jd">Julian Day (UT) for calculation.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The index of the Yoga (0-26).</returns>
        private int GetYogaIndex(double jd, SwissEph swe)
        {
            string serr = "";
            double[] sun = new double[6];
            double[] moon = new double[6];

            // Calculate Sun's and Moon's sidereal longitudes
            swe.swe_calc_ut(jd, SwissEph.SE_SUN, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, sun, ref serr);
            swe.swe_calc_ut(jd, SwissEph.SE_MOON, SwissEph.SEFLG_SWIEPH | SwissEph.SEFLG_SIDEREAL, moon, ref serr);

            // Sum of longitudes modulo 360 degrees
            double sum = (sun[0] + moon[0]) % 360;

            // Each Yoga spans 360/27 = 13.333 degrees.
            return Math.Min(26, (int)(sum / (360.0 / 27))); // Index 0-26
        }

        /// <summary>
        /// Performs a binary search to find the precise Julian Day when a Yoga changes.
        /// </summary>
        /// <param name="low">Lower bound Julian Day.</param>
        /// <param name="high">Upper bound Julian Day.</param>
        /// <param name="currentIndex">The Yoga index we are searching to change from.</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The Julian Day of the Yoga change.</returns>
        private double BinarySearchYogaChange(double low, double high, int currentIndex, SwissEph swe)
        {
            for (int i = 0; i < 30; i++) // 30 iterations for high precision
            {
                double mid = (low + high) / 2;
                if (GetYogaIndex(mid, swe) == currentIndex)
                    low = mid;
                else
                    high = mid;
            }
            return (low + high) / 2;
        }

        #endregion

        #region Common Helper Methods (Moved here to avoid duplication)

        /// <summary>
        /// Converts a DateTime object to a Julian Day number.
        /// </summary>
        /// <param name="dt">The DateTime object (expected to be in UTC for SwissEph compatibility).</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The Julian Day number.</returns>
        private double ToJulian(DateTime dt, SwissEph swe)
        {
            return swe.swe_julday(dt.Year, dt.Month, dt.Day,
                dt.Hour + dt.Minute / 60.0 + dt.Second / 3600.0 + dt.Millisecond / 3600000.0, // Include milliseconds for precision
                SwissEph.SE_GREG_CAL);
        }

        /// <summary>
        /// Converts a Julian Day number to a DateTime object, applying a timezone offset.
        /// </summary>
        /// <param name="jd">The Julian Day number (expected to be in UT).</param>
        /// <param name="tz">The timezone offset to apply after conversion to UT (e.g., 5.5 for IST).</param>
        /// <param name="swe">SwissEph instance.</param>
        /// <returns>The DateTime object in the specified timezone.</returns>
        private DateTime JulianToDateTime(double jd, double tz, SwissEph swe)
        {
            int y = 0, m = 0, d = 0;
            double h = 0;
            // Convert Julian Day to Gregorian calendar date and Universal Time (UT)
            swe.swe_revjul(jd, SwissEph.SE_GREG_CAL, ref y, ref m, ref d, ref h);

            int hr = (int)h;
            int min = (int)((h - hr) * 60);
            int sec = (int)(((h - hr) * 60 - min) * 60);
            int ms = (int)((((h - hr) * 60 - min) * 60 - sec) * 1000); // Get milliseconds for precision

            // Create a DateTime object in UTC and then add the timezone offset
            return new DateTime(y, m, d, hr, min, sec, ms, DateTimeKind.Utc).AddHours(tz);
        }

        #endregion

        #region Standard MVC Actions

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        #endregion
    }
}
