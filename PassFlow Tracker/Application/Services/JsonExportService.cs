using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PassFlow_Tracker.Application.Services
{
    public static class JsonExportService
    {
        public static List<RootRecord> ExportTripStops(ObservableCollection<TripStopRowViewModel> TripStops)
        {
            var record = new RootRecord
            {
                Unit = "Exported",
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Count = new CountDto
                {
                    Entered = TripStops.Sum(s => s.Entered),
                    Exited = TripStops.Sum(s => s.Exited),
                    Transported = TripStops.Sum(s => s.Transported)
                },
                Rounds = new List<RoundDto>
        {
            new RoundDto
            {
                Start = "All",
                End = "All",
                TimeFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                TimeTo = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Count = new CountDto
                {
                    Entered = TripStops.Sum(s => s.Entered),
                    Exited = TripStops.Sum(s => s.Exited),
                    Transported = TripStops.Sum(s => s.Transported)
                },
                Trips = new List<TripDto>
                {
                    new TripDto
                    {
                        Start = "All",
                        End = "All",
                        TimeFrom = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        TimeTo = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        Count = new CountDto
                        {
                            Entered = TripStops.Sum(s => s.Entered),
                            Exited = TripStops.Sum(s => s.Exited),
                            Transported = TripStops.Sum(s => s.Transported)
                        },
                        Stops = TripStops.Select(s => new StopDto
                        {
                            Id = s.StopNumber,
                            Name = s.StopName,
                            Duplicate = false,
                            Skipped = false,
                            TimeFrom = s.TimeFrom,
                            TimeTo = s.TimeTo,
                            Count = new CountDto
                            {
                                Entered = s.Entered,
                                Exited = s.Exited,
                                Transported = s.Transported
                            }
                        }).ToList()
                    }
                }
            }
        }
            };

            return new List<RootRecord> { record };
        }

        public static List<RootRecord> ExportTrips(ObservableCollection<TripRowViewModel> Trips)
        {
            var record = new RootRecord
            {
                Unit = "Exported",
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Count = new CountDto
                {
                    Entered = Trips.Sum(t => t.Entered),
                    Exited = Trips.Sum(t => t.Exited),
                    Transported = Trips.Sum(t => t.Transported)
                },
                Rounds = new List<RoundDto>
        {
            new RoundDto
            {
                Start = "All",
                End = "All",
                TimeFrom = Trips.FirstOrDefault()?.TimeFrom ?? "",
                TimeTo = Trips.LastOrDefault()?.TimeTo ?? "",
                Count = new CountDto
                {
                    Entered = Trips.Sum(t => t.Entered),
                    Exited = Trips.Sum(t => t.Exited),
                    Transported = Trips.Sum(t => t.Transported)
                },
                Trips = Trips.Select(t => new TripDto
                {
                    Start = t.StartPoint,
                    End = t.EndPoint,
                    TimeFrom = t.TimeFrom,
                    TimeTo = t.TimeTo,
                    Count = new CountDto
                    {
                        Entered = t.Entered,
                        Exited = t.Exited,
                        Transported = t.Transported
                    },
                    Stops = new List<StopDto>()
                }).ToList()
            }
        }
            };

            return new List<RootRecord> { record };
        }

        public static List<RootRecord> ExportRounds(ObservableCollection<RoundRowViewModel> Rounds)
        {
            var record = new RootRecord
            {
                Unit = "Exported",
                Date = DateTime.Now.ToString("yyyy-MM-dd"),
                Count = new CountDto
                {
                    Entered = Rounds.Sum(r => r.Entered),
                    Exited = Rounds.Sum(r => r.Exited),
                    Transported = Rounds.Sum(r => r.Transported)
                },
                Rounds = Rounds.Select(r => new RoundDto
                {
                    Start = r.StartPoint,
                    End = r.EndPoint,
                    TimeFrom = r.TimeFrom,
                    TimeTo = r.TimeTo,
                    Count = new CountDto
                    {
                        Entered = r.Entered,
                        Exited = r.Exited,
                        Transported = r.Transported
                    },
                    Trips = new List<TripDto>()
                }).ToList()
            };

            return new List<RootRecord> { record };
        }

        public static List<RootRecord> ExportDailyRecords(ObservableCollection<DailyRecordRowViewModel> DailyRecords)
        {
            return DailyRecords.Select(dr => new RootRecord
            {
                Unit = dr.UnitName,
                Date = dr.RecordDate,
                Count = new CountDto
                {
                    Entered = dr.Entered,
                    Exited = dr.Exited,
                    Transported = dr.Transported
                },
                Rounds = new List<RoundDto>()
            }).ToList();
        }

        public static List<RootRecord> ExportAllData(ObservableCollection<DayNodeViewModel> AllDataTree)
        {
            return AllDataTree.Select(day => new RootRecord
            {
                Unit = day.UnitName,
                Date = day.RecordDate,
                Count = new CountDto
                {
                    Entered = day.Entered,
                    Exited = day.Exited,
                    Transported = day.Transported
                },
                Rounds = day.Rounds.Select(round => new RoundDto
                {
                    Start = round.StartPoint,
                    End = round.EndPoint,
                    TimeFrom = round.TimeFrom,
                    TimeTo = round.TimeTo,
                    Count = new CountDto
                    {
                        Entered = round.Entered,
                        Exited = round.Exited,
                        Transported = round.Transported
                    },
                    Trips = round.Trips.Select(trip => new TripDto
                    {
                        Start = trip.StartPoint,
                        End = trip.EndPoint,
                        TimeFrom = trip.TimeFrom,
                        TimeTo = trip.TimeTo,
                        Count = new CountDto
                        {
                            Entered = trip.Entered,
                            Exited = trip.Exited,
                            Transported = trip.Transported
                        },
                        Stops = trip.Stops.Select(stop => new StopDto
                        {
                            Id = stop.StopNumber,
                            Name = stop.StopName,
                            Duplicate = stop.IsDuplicate,
                            Skipped = stop.IsSkipped,
                            TimeFrom = stop.TimeFrom,
                            TimeTo = stop.TimeTo,
                            Count = new CountDto
                            {
                                Entered = stop.Entered,
                                Exited = stop.Exited,
                                Transported = stop.Transported
                            }
                        }).ToList()
                    }).ToList()
                }).ToList()
            }).ToList();
        }
    }
}
