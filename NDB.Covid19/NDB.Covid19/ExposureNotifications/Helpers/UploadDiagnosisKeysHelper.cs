using System;
using System.Collections.Generic;
using System.Linq;
using NDB.Covid19.Configuration;
using NDB.Covid19.Models;
using Xamarin.ExposureNotifications;

namespace NDB.Covid19.ExposureNotifications.Helpers
{
    public abstract class UploadDiagnosisKeysHelper
    {
        /// <summary>
        /// This method does a simple validation of keys that might be obtained from the operating system.
        /// Some actions by user, may cause wrond TEKs being generated and obtained from OS, e.g., manually changing the date.
        /// Xamarin representation of TEKs does not take care of such things so we do it in this method.
        /// The method will return a list with only valid keys.
        ///
        /// These are the factors that make a key upload invalid according to Apple at
        /// https://developer.apple.com/documentation/exposurenotification/setting_up_an_exposure_notification_server:
        ///  1. The period of time covered exceeds 14 days.
        ///  2. RollingPeriod must be a positive value no greater than 144.
        /// </summary>
        /// <param name="temporaryExposureKeys"></param>
        /// <returns></returns>
        public static List<ExposureKeyModel> CreateAValidListOfTemporaryExposureKeys(IEnumerable<ExposureKeyModel> temporaryExposureKeys)
        {
            List<ExposureKeyModel> validListOfTeks = new List<ExposureKeyModel>();
            List<ExposureKeyModel> temporaryExposureKeysList = temporaryExposureKeys.ToList();
            foreach (ExposureKeyModel tek in temporaryExposureKeysList)
            {
                // Ignores keys that exceeds the 14 day period, to satisfy criterion 1
                if (TekHasInvalidRollingStart(tek))
                {
                    continue;
                }

                // Sets RollingDuration to default if invalid, to satisfy criterion 2
                if (TekHasInvalidRollingDuration(tek))
                {
                    tek.RollingDuration = new TimeSpan(1, 0, 0, 0);
                }

                validListOfTeks.Add(tek);
            }
            return validListOfTeks;
        }

        private static bool TekHasInvalidRollingStart(ExposureKeyModel tek) 
        {
            return tek.RollingStart.Date < SystemTime.Now().AddDays(-14).Date || tek.RollingStart.Date > SystemTime.Now().Date;
        }

        private static bool TekHasInvalidRollingDuration(ExposureKeyModel tek)
        {
            return tek.RollingDuration.TotalMinutes <= 0 || tek.RollingDuration.TotalMinutes > 1440;
        }

        public static List<ExposureKeyModel> RemoveKeysOlderThanMaxNumberOfDays(IEnumerable<ExposureKeyModel> keys, int maxNumberOfDays)
        {
            DateTime maxNumberOfDaysAgoDate = SystemTime.Now().AddDays(-maxNumberOfDays).Date;
            return keys.Where(key => key.RollingStart.Date > maxNumberOfDaysAgoDate).ToList();
        }

        /// <summary>
        /// This method is used to set TransmissionRiskLevel before keys are sent to server.
        /// It operates on intervals that are set in configuration file, these intervals are defined strictly, without any overlaps.
        /// The method does convertion from UTC time representation of symptomOnset date to DateTimeOffset, then gets
        /// a positive or negative integer representing difference between key's DateTimeOffset and the symptoms DateTimeOffset.
        /// Based on configuration, TransmissionRiskLevel for the key is then set to the natural index in the table TransmissionRiskLevel table.
        /// NOTE: By natural we mean that the first index is 1, not 0, the second is 2, not 1 etc.
        /// NOTE: The key risk will always be assigned to 1 (lowest risk value) at the end, if none other higher value is assigned. 
        /// Example: Key date: 26.05.2020, Symptoms onset: 24.05.2020.
        ///  The difference in dates is +2 days.
        ///  In Configuration file in the tuples array it corresponds to the item where this difference is set
        ///  has index 3. See Tuple.Create(-1, 2) in Conf.cs This means, that the natural number index equals 4.
        ///  The key's TransmissionRiskLevel would be set to 4.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="symptomsDate"></param>
        /// <returns></returns>
        public static List<ExposureKeyModel> SetTransmissionRiskLevel(List<ExposureKeyModel> keys, DateTime symptomsDate)
        {
            DateTimeOffset dateTimeOffsetForSymptomDate = new DateTimeOffset(symptomsDate);
            foreach (ExposureKeyModel key in keys)
            {
                TimeSpan timeSpanBetweenSymptomOnsetAndKey = key.RollingStart - dateTimeOffsetForSymptomDate;
                int daysBetweenSymptomOnsetAndKey = timeSpanBetweenSymptomOnsetAndKey.Days;
                key.DaysSinceOnsetOfSymptoms = daysBetweenSymptomOnsetAndKey;
                for (int i = Conf.DAYS_SINCE_ONSET_FOR_TRANSMISSION_RISK_CALCULATION.Length - 1; i >= 0; i--)
                {
                    if (daysBetweenSymptomOnsetAndKey >= Conf.DAYS_SINCE_ONSET_FOR_TRANSMISSION_RISK_CALCULATION[i].Item1
                        && daysBetweenSymptomOnsetAndKey <= Conf.DAYS_SINCE_ONSET_FOR_TRANSMISSION_RISK_CALCULATION[i].Item2)
                    {
                        key.TransmissionRiskLevel = (RiskLevel)i + 1; // Setting to natural index number since 0 represents Invalid
                        break;
                    }
                }
            }
            return keys;
        }
    }
}
