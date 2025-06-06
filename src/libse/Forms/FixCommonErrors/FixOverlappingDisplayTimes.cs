﻿using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Interfaces;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using System;

namespace Nikse.SubtitleEdit.Core.Forms.FixCommonErrors
{
    public class FixOverlappingDisplayTimes : IFixCommonError
    {
        public static class Language
        {
            public static string FixOverlappingDisplayTime { get; set; } = "Fix overlapping display times";
            public static string StartTimeLaterThanEndTime { get; set; } = "Text number {0}: Start time is later than end time: {4}{1} -> {2} {3}";
            public static string UnableToFixStartTimeLaterThanEndTime { get; set; } = "Unable to fix text number {0}: Start time is later than end time: {1}";
            public static string FixOverlappingDisplayTimes { get; set; } = "Fix overlapping display times";
            public static string XFixedToYZ { get; set; } = "{0} fixed to: {1}{2}";
            public static string UnableToFixTextXY { get; set; } = "Unable to fix text number {0}: {1}";
        }

        public void Fix(Subtitle subtitle, IFixCallbacks callbacks)
        {
            // negative display time
            string fixAction = Language.FixOverlappingDisplayTime;
            int noOfOverlappingDisplayTimesFixed = 0;
            for (int i = 0; i < subtitle.Paragraphs.Count; i++)
            {
                var p = subtitle.Paragraphs[i];
                var oldP = new Paragraph(p);
                if (p.DurationTotalMilliseconds < 0) // negative display time...
                {
                    bool isFixed = false;
                    string status = string.Format(Language.StartTimeLaterThanEndTime, i + 1, p.StartTime, p.EndTime, p.Text, Environment.NewLine);

                    var prev = subtitle.GetParagraphOrDefault(i - 1);
                    var next = subtitle.GetParagraphOrDefault(i + 1);

                    double wantedDisplayTime = Utilities.GetOptimalDisplayMilliseconds(p.Text) * 0.9;

                    if (next == null || next.StartTime.TotalMilliseconds > p.StartTime.TotalMilliseconds + wantedDisplayTime)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            p.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds + wantedDisplayTime;
                            isFixed = true;
                        }
                    }
                    else if (next.StartTime.TotalMilliseconds > p.StartTime.TotalMilliseconds + 500.0)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            p.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds + 500.0;
                            isFixed = true;
                        }
                    }
                    else if (prev == null || next.StartTime.TotalMilliseconds - wantedDisplayTime > prev.EndTime.TotalMilliseconds)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            p.StartTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - wantedDisplayTime;
                            p.EndTime.TotalMilliseconds = next.StartTime.TotalMilliseconds - 1;
                            isFixed = true;
                        }
                    }
                    else
                    {
                        callbacks.LogStatus(Language.FixOverlappingDisplayTimes, string.Format(Language.UnableToFixStartTimeLaterThanEndTime, i + 1, p), true);
                        callbacks.AddToTotalErrors(1);
                    }

                    if (isFixed)
                    {
                        noOfOverlappingDisplayTimesFixed++;
                        status = string.Format(Language.XFixedToYZ, status, Environment.NewLine, p);
                        callbacks.LogStatus(Language.FixOverlappingDisplayTimes, status);
                        callbacks.AddFixToListView(p, fixAction, oldP.ToString(), p.ToString());
                    }
                }
            }

            // overlapping display time
            for (int i = 1; i < subtitle.Paragraphs.Count; i++)
            {
                Paragraph p = subtitle.Paragraphs[i];
                Paragraph prev = subtitle.GetParagraphOrDefault(i - 1);
                Paragraph target = prev;
                string oldCurrent = p.ToString();
                string oldPrevious = prev.ToString();
                double prevWantedDisplayTime = Utilities.GetOptimalDisplayMilliseconds(prev.Text, Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds);
                double currentWantedDisplayTime = Utilities.GetOptimalDisplayMilliseconds(p.Text, Configuration.Settings.General.SubtitleMaximumCharactersPerSeconds);
                double prevOptimalDisplayTime = Utilities.GetOptimalDisplayMilliseconds(prev.Text);
                double currentOptimalDisplayTime = Utilities.GetOptimalDisplayMilliseconds(p.Text);
                bool canBeEqual = callbacks.Format != null && (callbacks.Format.GetType() == typeof(AdvancedSubStationAlpha) || callbacks.Format.GetType() == typeof(SubStationAlpha));
                if (!canBeEqual)
                {
                    canBeEqual = Configuration.Settings.Tools.FixCommonErrorsFixOverlapAllowEqualEndStart;
                }

                double diff = prev.EndTime.TotalMilliseconds - p.StartTime.TotalMilliseconds;
                if (!prev.StartTime.IsMaxTime && !p.StartTime.IsMaxTime && diff >= 0 && !(canBeEqual && Math.Abs(diff) < 0.001))
                {
                    int diffHalf = (int)(diff / 2);
                    if (!Configuration.Settings.Tools.FixCommonErrorsFixOverlapAllowEqualEndStart && Math.Abs(p.StartTime.TotalMilliseconds - prev.EndTime.TotalMilliseconds) < 0.001 &&
                        prev.DurationTotalMilliseconds > 100)
                    {
                        if (callbacks.AllowFix(target, fixAction))
                        {
                            if (!canBeEqual)
                            {
                                bool okEqual = true;
                                if (prev.DurationTotalMilliseconds > Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds)
                                {
                                    prev.EndTime.TotalMilliseconds--;
                                }
                                else if (p.DurationTotalMilliseconds > Configuration.Settings.General.SubtitleMinimumDisplayMilliseconds)
                                {
                                    p.StartTime.TotalMilliseconds++;
                                }
                                else
                                {
                                    okEqual = false;
                                }

                                if (okEqual)
                                {
                                    noOfOverlappingDisplayTimesFixed++;
                                    callbacks.AddFixToListView(target, fixAction, oldPrevious, prev.ToString());
                                }
                            }
                        }
                    }
                    else if (prevOptimalDisplayTime <= (p.StartTime.TotalMilliseconds - prev.StartTime.TotalMilliseconds))
                    {
                        if (callbacks.AllowFix(target, fixAction))
                        {
                            prev.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds - 1;
                            if (canBeEqual)
                            {
                                prev.EndTime.TotalMilliseconds++;
                            }

                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(target, fixAction, oldPrevious, prev.ToString());
                        }
                    }
                    else if (diff > 0 && currentOptimalDisplayTime <= p.DurationTotalMilliseconds - diffHalf &&
                             prevOptimalDisplayTime <= prev.DurationTotalMilliseconds - diffHalf)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            prev.EndTime.TotalMilliseconds -= diffHalf;
                            p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(p, fixAction, oldCurrent, p.ToString());
                        }
                    }
                    else if (currentOptimalDisplayTime <= p.EndTime.TotalMilliseconds - prev.EndTime.TotalMilliseconds)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                            if (canBeEqual)
                            {
                                p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds;
                            }

                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(p, fixAction, oldCurrent, p.ToString());
                        }
                    }
                    else if (diff > 0 && currentWantedDisplayTime <= p.DurationTotalMilliseconds - diffHalf &&
                             prevWantedDisplayTime <= prev.DurationTotalMilliseconds - diffHalf)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            prev.EndTime.TotalMilliseconds -= diffHalf;
                            p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(p, fixAction, oldCurrent, p.ToString());
                        }
                    }
                    else if (prevWantedDisplayTime <= (p.StartTime.TotalMilliseconds - prev.StartTime.TotalMilliseconds))
                    {
                        if (callbacks.AllowFix(target, fixAction))
                        {
                            prev.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds - 1;
                            if (canBeEqual)
                            {
                                prev.EndTime.TotalMilliseconds++;
                            }

                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(target, fixAction, oldPrevious, prev.ToString());
                        }
                    }
                    else if (currentWantedDisplayTime <= p.EndTime.TotalMilliseconds - prev.EndTime.TotalMilliseconds)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                            if (canBeEqual)
                            {
                                p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds;
                            }

                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(p, fixAction, oldCurrent, p.ToString());
                        }
                    }
                    else if (Math.Abs(p.StartTime.TotalMilliseconds - prev.EndTime.TotalMilliseconds) < 10 && p.DurationTotalMilliseconds > 1)
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            prev.EndTime.TotalMilliseconds -= 2;
                            p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds + 1;
                            if (canBeEqual)
                            {
                                p.StartTime.TotalMilliseconds = prev.EndTime.TotalMilliseconds;
                            }

                            noOfOverlappingDisplayTimesFixed++;
                            callbacks.AddFixToListView(p, fixAction, oldCurrent, p.ToString());
                        }
                    }
                    else
                    {
                        if (callbacks.AllowFix(p, fixAction))
                        {
                            callbacks.LogStatus(Language.FixOverlappingDisplayTimes, string.Format(Language.UnableToFixTextXY, i + 1, Environment.NewLine + prev.Number + "  " + prev + Environment.NewLine + p.Number + "  " + p), true);
                            callbacks.AddToTotalErrors(1);
                        }
                    }
                }
            }

            callbacks.UpdateFixStatus(noOfOverlappingDisplayTimesFixed, fixAction);
        }

    }
}
