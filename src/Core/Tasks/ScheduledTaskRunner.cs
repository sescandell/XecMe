﻿#region GNU GPL Version 3 License

/// Copyright 2013 Shailesh Lolam
/// 
/// This file ScheduledTaskRunner.cs is part of XecMe.
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
using System.Text;
using XecMe.Core.Tasks;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Diagnostics;
using XecMe.Core.Utils;
using XecMe.Common;

namespace XecMe.Core.Tasks
{
    public enum Recursion { Daily, Weekly, Monthly }

    public enum Week
    {
        None = 0,
        First = 1,
        Second = First * 2,
        Third = Second * 2,
        Fourth = Third * 2,
        Last = Fourth * 2,
        All = (Last * 2) - 1
    }

    public enum Weekdays
    {
        None = 0,
        Sunday = 1,
        Monday = Sunday * 2,
        Tuesday = Monday * 2,
        Wednesday = Tuesday * 2,
        Thursday = Wednesday * 2,
        Friday = Thursday * 2,
        Saturday = Friday * 2,
        All = (Saturday * 2) - 1
    }

    public enum Months : short
    {
        None = 0,
        January = 1,
        February = January * 2,
        March = February * 2,
        April = March * 2,
        May = April * 2,
        June = May * 2,
        July = June * 2,
        August = July * 2,
        September = August * 2,
        October = September * 2,
        November = October * 2,
        December = November * 2,
        All = December * 2 - 1
    }

    public class ScheduledTaskRunner : TaskRunner
    {
        private string _schedule;
        private DateTime _startDate, _lastDateTime;
        private TimeSpan _taskTime;
        private Recursion _recursion;
        private IRecur _recur;
        private int _repeat;
        private Timer _timer;
        private TaskWrapper _task;
        private bool _skip;

        private TimeZoneInfo _timeZoneInfo;

        
        public ScheduledTaskRunner(string name, Type taskType, Dictionary<string, object> parameters, int repeat, Recursion recursion, string schedule,
            DateTime startDate, TimeSpan taskTime, TimeZoneInfo timeZoneInfo, TraceType traceType) :
            base(name, taskType, parameters, traceType)
        {

            if (repeat < 1)
                throw new ArgumentOutOfRangeException("repeat", "repeat should be positive number for number of times to be repeated or 0 for infinite number of times");
            if (taskTime < Time.DayMinTime || taskTime > Time.DayMaxTime)
                throw new ArgumentOutOfRangeException("taskTime", "Task time should be between 00:00:00 and 23:59:59");
            if (schedule == null)
                throw new ArgumentNullException("schedule", "schedule cannot be null");
            if (startDate > DateTime.Now)
                throw new ArgumentNullException("startDate should be less than now");
            _schedule = schedule.ToUpper();
            _recursion = recursion;
            ///If the start time is not configured set it to Sunday of this week
            _startDate = startDate == DateTime.MinValue ? DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek) : startDate;
            _repeat = repeat;
            _taskTime = taskTime;
            _timeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Local;

            switch (recursion)
            {
                case Recursion.Daily:
                    _recur = new Daily(repeat);
                    break;
                case Recursion.Weekly:
                    //WD:Tuesday, Friday, Monday
                    Weekdays weekdays = Weekdays.None;
                    if (schedule.Length < 3 || schedule.Substring(0, 3) != "WD:")
                        throw new ArgumentOutOfRangeException("schedule", "schedule does not have the weekdays defined for the Weekly tasks");

                    if (!Enum.TryParse<Weekdays>(schedule.Substring(3), true, out weekdays) || weekdays == Weekdays.None)
                        throw new ArgumentOutOfRangeException("schedule", "schedule does not have the weekdays defined for the Weekly tasks");

                    _recur = new Weekly(repeat, weekdays);
                    break;
                case Recursion.Monthly:
                    //"MN:January,March,December|WK:First,Last|WD:THursday,Friday"
                    //"MN:January,March,December|DY:1,2,3,Last"
                    Months months = Months.None;
                    weekdays = Weekdays.None;
                    Week week = Week.None;
                    uint days = 0;
                    foreach (var item in schedule.Split('|'))
                    {
                        if (item.Length < 4)
                            throw new ArgumentOutOfRangeException("schedule", "schedule does not conform to the format");
                        switch (item.Substring(0, 3))
                        {
                            case "MN:":
                                if (!Enum.TryParse<Months>(item.Substring(3), true, out months) || months == Months.None)
                                    throw new ArgumentOutOfRangeException("schedule", "schedule does not have the months defined for the Monthly tasks");
                                break;
                            case "WK:":
                                if (!Enum.TryParse<Week>(item.Substring(3), true, out week) || week == Week.None)
                                    throw new ArgumentOutOfRangeException("schedule", "schedule does not have the weeks defined for the Monthly tasks");
                                break;
                            case "WD:":
                                if (!Enum.TryParse<Weekdays>(item.Substring(3), true, out weekdays) || weekdays == Weekdays.None)
                                    throw new ArgumentOutOfRangeException("schedule", "schedule does not have the weekdays defined for the Monthly tasks");
                                break;
                            case "DY:":
                                foreach (var d in item.Substring(3).Split(','))
                                {
                                    int x = 0;
                                    uint day = 1;

                                    if (d == "LAST")
                                        days |= 0x80000000;//Last day of the month
                                    else if (d == "ALL")
                                        days |= 0x7FFFFFFF;//This is for all 1-31 days
                                    else if (!int.TryParse(d, out x) || x < 1 || x > 31)
                                        throw new ArgumentOutOfRangeException("schedule", "schedule does not have the day defined for the Monthly tasks in a valid range");
                                    else
                                        days |= day << (x - 1);
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException("schedule", "schedule does not conform to the format");
                        }
                    }

                    if (week != Week.None && weekdays != Weekdays.None && months != Months.None)
                        _recur = new MonthlyByWeekdays(months, weekdays, week);
                    else if (days > 1 && months != Months.None)
                        _recur = new MonthlyByDay(months, days);
                    else
                        throw new ArgumentOutOfRangeException("schedule", "schedule does not conform to the format");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("schedule", "schedule does not conform to the format");
            }

            _lastDateTime = new DateTime(_startDate.Year, _startDate.Month, _startDate.Day, _taskTime.Hours, _taskTime.Minutes, _taskTime.Seconds, DateTimeKind.Unspecified);

            ///If not daily task, initialize to correct start marker i.e. next schedule to align with the configuration
            ///If the task is configured for every Monday and this process starts on Thursday 
            //if (recursion != Recursion.Daily)
            //    _lastDateTime = Next(_lastDateTime);

        }

        public ScheduledTaskRunner(string name, Type taskType, Dictionary<string, object> parameters, int repeat, IRecur recur,
            DateTime startDate, TimeSpan taskTime, TimeZoneInfo timeZoneInfo, TraceType traceType) :
            base(name, taskType, parameters, traceType)
        {
            recur.NotNull(nameof(recur));
            if (repeat < 1)
                throw new ArgumentOutOfRangeException("repeat", "repeat should be positive number for number of times to be repeated or 0 for infinite number of times");
            if (taskTime < Time.DayMinTime || taskTime > Time.DayMaxTime)
                throw new ArgumentOutOfRangeException("taskTime", "Task time should be between 00:00:00 and 23:59:59");
            if (startDate > DateTime.Now)
                throw new ArgumentNullException("startDate should be less than now");
            ///If the start time is not configured set it to Sunday of this week
            _startDate = startDate == DateTime.MinValue ? DateTime.Now.AddDays(-(int)DateTime.Now.DayOfWeek) : startDate;
            _recur = recur;
            _taskTime = taskTime;
            _timeZoneInfo = timeZoneInfo ?? TimeZoneInfo.Local;
        }

        public int Repeat
        {
            get
            {
                return _repeat;
            }
        }

        public string Schedule
        {
            get
            {
                return _schedule;
            }
        }

        #region TaskRunner Members

        public override void Start()
        {
            lock (this)
            {
                if (_timer == null)
                {
                    _task = new TaskWrapper(this.GetTaskInstance(), new ExecutionContext(Parameters, this));

                    _timer = new Timer(new TimerCallback(RunTask), null, Timeout.Infinite, Timeout.Infinite);

                    ScheduleNextRun();

                    base.Start();
                    TraceInformation("Started", this.Name);
                }
            }
        }

        public override void Stop()
        {
            lock (this)
            {
                if (_timer != null)
                {
                    using (_timer) ;
                    _timer = null;
                    _task.Release();
                    _task = null;
                    base.Stop();
                    TraceInformation("Stopped", this.Name);
                }
            }
        }

        #endregion
        private void RunTask(object state)
        {
            ///Task not started, call should never come here
            if (_timer == null)
                return;

            if (!_skip)
            {
                ExecutionState executionState = _task.RunTask();
                TraceInformation("Executed with return value {0}", executionState);

                switch (executionState)
                {
                    case ExecutionState.Executed:
                        RaiseComplete(_task.Context);
                        break;
                    case ExecutionState.Stop:
                        Stop();
                        return;
                    case ExecutionState.Recycle:
                        _task.Release();
                        _task = new TaskWrapper(this.GetTaskInstance(), new ExecutionContext(Parameters, this));
                        break;
                }
            }

            ScheduleNextRun();
        }

        private DateTime Now
        {
            get
            {
                DateTime now;
                if (_timeZoneInfo == null
                    || _timeZoneInfo == TimeZoneInfo.Local)
                    now = DateTime.Now;
                else
                    now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo);
                TraceInformation("Now: {0}", now);
                return now;
            }
        }
        private void ScheduleNextRun()
        {
            DateTime now = Now;
            while (_lastDateTime < now)
            {
                _lastDateTime = Next(_lastDateTime);

                ///If stuck in the past date advance the date
                //_lastDateTime = dt == _lastDateTime ? _lastDateTime.AddDays(1) : dt;
            }

            TimeSpan delay = TimeZoneInfo.ConvertTimeToUtc(_lastDateTime, _timeZoneInfo) - TimeZoneInfo.ConvertTimeToUtc(now, _timeZoneInfo);

            if (delay.TotalMilliseconds > int.MaxValue)
            {
                _skip = true;
                _timer = new Timer(new TimerCallback(RunTask), null, int.MaxValue, Timeout.Infinite);
            }
            else
            {
                _skip = false;
                _timer = new Timer(new TimerCallback(RunTask), null, delay, TimeSpan.FromMilliseconds(-1));
            }
            TraceInformation("Scheduled to run next at {0}", _lastDateTime);
        }

        private DateTime Next(DateTime from)
        {
            return _recur.Next(from);
        }
    }



    public interface IRecur
    {
        DateTime Next(DateTime from);
    }

    #region Daily
    internal class Daily : IRecur
    {
        int _repeat;

        public Daily(int repeat)
        {
            if (repeat < 1)
                throw new ArgumentOutOfRangeException("repeat");
            _repeat = repeat;
        }
        DateTime IRecur.Next(DateTime from)
        {
            return from.AddDays(_repeat);
        }
    }
    #endregion


    #region Weekly
    internal class Weekly : IRecur
    {
        int _repeat;
        Weekdays _weekdays;
        public Weekly(int repeat, Weekdays weekdays)
        {
            if (repeat < 1)
                throw new ArgumentOutOfRangeException("repeat");
            _repeat = repeat - 1;//Multiplying with 7(week) becomes easy when we do it here
            _weekdays = weekdays;
        }


        DateTime IRecur.Next(DateTime from)
        {
            DateTime original = from;
            while (true)
            {
                Weekdays thisDay = Utils.GetWeekday(from.DayOfWeek);
                ///If its all weekdays, just add the next day
                if (_weekdays == Weekdays.All)
                {
                    if (thisDay == Weekdays.Saturday)
                        from = from.AddDays((_repeat * 7) + 1);
                    else
                        from = from.AddDays(1);
                }
                else
                {
                    int d, i = (int)from.DayOfWeek;
                    ///Find number of days for the next valid weekday
                    for (d = 0; !Utils.HasWeekday(_weekdays, Utils.GetWeekday(i)); i++)
                    {
                        d++;
                    }
                    ///calculate next valid weekday to run the job
                    i = (d + (int)from.DayOfWeek) % 7;

                    ///if the next valid day is >= the from weekday simple add the day else going back to next week
                    if (i >= (int)from.DayOfWeek)
                    {
                        from = from.AddDays(d);
                    }
                    else
                    {
                        from = from.AddDays(d + (_repeat * 7));
                    }
                }
                ///If the date is same the one passed in the add a day
                if (original == from)
                {
                    from = from.AddDays(1);
                    continue;
                }
                return from;
            }
        }
    }
    #endregion

    #region Monthly by weekdays
    internal class MonthlyByWeekdays : IRecur
    {
        Months _months;
        Weekdays _weekdays;
        Week _weeks;

        public MonthlyByWeekdays(Months months, Weekdays weekdays, Week weeks)
        {
            _months = months;
            _weekdays = weekdays;
            _weeks = weeks;
        }

        DateTime IRecur.Next(DateTime from)
        {
            DateTime original = from;
            while (true)
            {
                ///Set the valid month
                from = Utils.GetValidMonth(_months, from);

                //First week
                if ((_weeks & Week.First) == Week.First)
                {
                    while (from.Day < 8)
                    {
                        if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                            && from != original)
                        {
                            return from;
                        }
                        from = from.AddDays(1);
                    }
                }
                //Second week
                if ((_weeks & Week.Second) == Week.Second)
                {
                    while (from.Day < 15 && from.Day > 7)
                    {
                        if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                            && from != original)
                        {
                            return from;
                        }
                        from = from.AddDays(1);
                    }
                }
                //Third week
                if ((_weeks & Week.Third) == Week.Third)
                {
                    while (from.Day < 22 && from.Day > 14)
                    {
                        if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                            && from != original)
                        {
                            return from;
                        }
                        from = from.AddDays(1);
                    }
                }
                //Both Fourth & Last week
                if ((_weeks & (Week.Fourth | Week.Last)) == (Week.Fourth | Week.Last))
                {
                    while (from.Day > 21)
                    {
                        if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                            && from != original)
                        {
                            return from;
                        }
                        from = from.AddDays(1);
                    }
                }
                else
                {
                    //Fourth week
                    if ((_weeks & Week.Fourth) == Week.Fourth)
                    {
                        while (from.Day < 29 && from.Day > 21)
                        {
                            if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                                && from != original)
                            {
                                return from;
                            }
                            from = from.AddDays(1);
                        }
                    }
                    //Last
                    if ((_weeks & Week.Last) == Week.Last)
                    {
                        int lastWeekDay = GetLastWeekStartDay(from);
                        while (from.Day >= lastWeekDay)
                        {
                            if (Utils.HasWeekday(_weekdays, Utils.GetWeekday(from.DayOfWeek))
                                && from != original)
                            {
                                return from;
                            }
                            from = from.AddDays(1);
                        }
                    }
                }
                from = from.AddDays(1);
            }
        }

        private int GetLastWeekStartDay(DateTime date)
        {
            switch (date.Month)
            {
                case 1:
                case 3:
                case 5:
                case 7:
                case 8:
                case 10:
                case 12:
                    return 25;
                case 4:
                case 6:
                case 9:
                case 11:
                    return 24;
                case 2:
                    return DateTime.IsLeapYear(date.Year) ? 23 : 22;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    #endregion


    #region Monthly by day
    internal class MonthlyByDay : IRecur
    {
        const uint LAST = 0x80000000;
        Months _months;
        uint _days;

        public MonthlyByDay(Months months, uint days)
        {
            if (days < 1 && days > 961)
                throw new ArgumentOutOfRangeException("days");
            _days = days;
            _months = months;
        }

        DateTime IRecur.Next(DateTime from)
        {
            DateTime original = from;
            while (true)
            {
                from = Utils.GetValidMonth(_months, from);

                ///Save the month
                int month = from.Month;
                ///Iterate while the month is same
                while (month == from.Month)
                {
                    ///if its a valid day return and not same date
                    if (HasDay(from.Day) && from != original)
                        return from;

                    ///Check the next day
                    from = from.AddDays(1);
                }
                //Month changed, so check the last day now, else go for the next valid month
                if ((_days & LAST) > 0)
                {
                    return from.AddDays(-1);
                }
            }
        }

        private bool HasDay(int day)
        {
            uint x = 1;
            x <<= (day - 1);
            return (x & _days) > 0;
        }
    }
    #endregion
    static class Utils
    {
        /// <summary>
        /// Returns the input date time if current month is valid month else set to the 1st of the next valid month
        /// </summary>
        /// <param name="months">Valid months</param>
        /// <param name="from">DateTime to be checked for valid months</param>
        /// <returns></returns>
        public static DateTime GetValidMonth(Months months, DateTime from)
        {
            ///Check if this is a valid month else loop until you get to the valid month
            while (!Utils.HasMonth(months, Utils.GetMonth(from.Month)))
            {
                ///If its not the first day of the month, the set the day to the first day of the month
                if (from.Day > 1)
                    from = from.AddDays(-from.Day + 1);//Get to the first of this month

                ///add a month until you get to the valid month
                from = from.AddMonths(1);
            }
            return from;
        }
        /// <summary>
        /// Maps the month number to the month enum value of XecMe framework
        /// </summary>
        /// <param name="month">Enum value of the month</param>
        /// <returns></returns>
        public static Months GetMonth(int month)
        {
            if (month < 1 || month > 12)
                throw new ArgumentOutOfRangeException("month");
            month--;
            
            return (Months)(1 << month);
        }

        /// <summary>
        /// Maps the numeric weekday value to the XecMe framework weekday
        /// </summary>
        /// <param name="dayOfWeek">numeric value of the weekday</param>
        /// <returns>Weekday enum value</returns>
        public static Weekdays GetWeekday(int dayOfWeek)
        {
            return (Weekdays)(1 << (dayOfWeek % 7));
        }

        /// <summary>
        /// Maps the .NET weekday to the XecMe framework weekday
        /// </summary>
        /// <param name="dayOfWeek">.NET enum weekday value</param>
        /// <returns>Weekday enum value</returns>
        public static Weekdays GetWeekday(DayOfWeek dayOfWeek)
        {
            return (Weekdays)Enum.Parse(typeof(Weekdays), dayOfWeek.ToString());
        }

        /// <summary>
        /// Returns true if the given month is in the months
        /// </summary>
        /// <param name="months">Configured months</param>
        /// <param name="month">Specific month</param>
        /// <returns>True, if the specific month is in the months</returns>
        public static bool HasMonth(Months months, Months month)
        {
            return month == (months & month);
        }

        /// <summary>
        /// Returns true if the given weekday is in the weekdays
        /// </summary>
        /// <param name="months">Configured weekdays</param>
        /// <param name="month">Specific weekday</param>
        /// <returns>True, if the specific weekday is in the weekdays</returns>
        public static bool HasWeekday(Weekdays weekdays, Weekdays weekday)
        {
            return weekday == (weekdays & weekday);
        }
    }

}
