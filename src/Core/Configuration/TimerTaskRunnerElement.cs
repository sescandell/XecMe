﻿#region GNU GPL Version 3 License

/// Copyright 2013 Shailesh Lolam
/// 
/// This file TimerTaskRunnerElement.cs is part of XecMe.
/// 
/// XecMe is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
/// 
/// XecMe is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License along with XecMe. If not, see http://www.gnu.org/licenses/.
/// 
/// History:
/// ______________________________________________________________
/// Created         01-2013             Shailesh Lolam

#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XecMe.Core.Tasks;
using System.Configuration;

namespace XecMe.Core.Configuration
{
    public class TimerTaskRunnerElement : TaskRunnerElement
    {
        #region Constants
        private static readonly TimeSpan TS_MIN;
        private static readonly TimeSpan TS_MAX;
        private const string INTERVAL = "interval";
        private const string RECURRENCE = "recurrence";
        private const string START = "startDateTime";
        private const string END = "endDateTime";
        private const string DAY_START_TIME = "dayStartTime";
        private const string DAY_END_TIME = "dayEndTime";
        #endregion

        static TimerTaskRunnerElement()
        {
            TS_MIN = TimeSpan.FromSeconds(0.0);
            TS_MAX = TimeSpan.FromSeconds(86399.0);
        }

        public TimerTaskRunnerElement()
        {
            base[START] = DateTime.MinValue;
            base[END] = DateTime.MaxValue;
            base[DAY_START_TIME] = TimeSpan.FromSeconds(0.0);//Midnight
            base[DAY_END_TIME] = TimeSpan.FromSeconds(86399.0);//23:59:59
        }

        [ConfigurationProperty(INTERVAL, IsRequired = true)]
        public long Interval
        {
            get { return (long)base[INTERVAL]; }
            set { base[INTERVAL] = value; }
        }

        [ConfigurationProperty(RECURRENCE, IsRequired = false, DefaultValue = -1)]
        [LongValidator(MinValue = -1, MaxValue = long.MaxValue)]
        public long Recurrence
        {
            get { return (long)base[RECURRENCE]; }
            set { base[RECURRENCE] = value; }
        }

        [ConfigurationProperty(START, IsRequired = false)]
        public DateTime StartDateTime
        {
            get { return (DateTime)base[START]; }
            set
            {
                if (EndDateTime < value)
                    throw new ConfigurationErrorsException(string.Concat(START, " should be less than ", END));

                base[START] = value;
            }
        }

        [ConfigurationProperty(END, IsRequired = false)]
        public DateTime EndDateTime
        {
            get { return (DateTime)base[END]; }
            set
            {
                if (StartDateTime > value)
                    throw new ConfigurationErrorsException(string.Concat(END, " should be greater than ", START));

                base[END] = value;
            }
        }

        [ConfigurationProperty(DAY_START_TIME, IsRequired = false)]
        public TimeSpan DayStartTime
        {
            get { return (TimeSpan)base[DAY_START_TIME]; }
            set
            {
                if (TS_MIN > value)
                    throw new ConfigurationErrorsException(string.Concat(DAY_START_TIME, " should be greater than 0 seconds"));

                if (DayEndTime < value)
                    throw new ConfigurationErrorsException(string.Concat(DAY_START_TIME," is greater than ", DAY_END_TIME));

                base[DAY_START_TIME] = value;
            }
        }

        [ConfigurationProperty(DAY_END_TIME, IsRequired = false)]
        public TimeSpan DayEndTime
        {
            get { return (TimeSpan)base[DAY_END_TIME]; }
            set
            {
                if (TS_MAX < value)
                    throw new ConfigurationErrorsException(string.Concat(DAY_END_TIME,"{0} should be less than 23:59:59"));

                if (DayStartTime > value)
                    throw new ConfigurationErrorsException(string.Concat(DAY_END_TIME, " is less than ", DAY_START_TIME));

                base[DAY_END_TIME] = value;
            }
        }


        public override TaskRunner GetRunner()
        {
            return new TimerTaskRunner(this.GetTaskType(), InternalParameters(), Interval, Recurrence) 
                    { StartDateTime = this.StartDateTime, EndDateTime = this.EndDateTime, DayStartTime = this.DayStartTime, DayEndTime = this.DayEndTime  };
        }
    }
}
